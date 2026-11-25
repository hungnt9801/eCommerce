using eCommerce.Kernel.Money;
using Volo.Abp;

namespace eCommerce.Pricing;

/// <summary>
/// **Kết quả tính tiền** cho một dòng hàng — mọi con số đều là `Money` (có loại tiền), **không** `decimal` trần.
/// </summary>
/// <param name="DonGiaSauLamTron">Đơn giá **sau R1** (đã làm tròn về số chữ số của tiền tệ).</param>
/// <param name="ThanhTienTruocGiam">`DonGiaSauLamTron × quantity`.</param>
/// <param name="GiamGiaDuocPhanBo">Phần **giảm giá cấp đơn** phân bổ cho dòng này (**sau R2**).</param>
/// <param name="PhiDuocPhanBo">Phần **phí cấp đơn** phân bổ cho dòng này (**sau R3**).</param>
/// <param name="Thue">Thuế của dòng (**sau R4**) — tính trên `ThanhTienTruocGiam − GiamGia + Phi`.</param>
public sealed record DongTienHang(
    string Sku,
    int Quantity,
    Money DonGiaSauLamTron,
    Money ThanhTienTruocGiam,
    Money GiamGiaDuocPhanBo,
    Money PhiDuocPhanBo,
    Money Thue)
{
    /// <summary>Thành tiền cuối của dòng (trước thuế): `ThànhTiền − GiảmGiá + Phí`.</summary>
    public Money ThanhTienSauGiam => ThanhTienTruocGiam - GiamGiaDuocPhanBo + PhiDuocPhanBo;
}

/// <summary>Kết quả tính tiền cho **cả đơn** — tổng là **Σ dòng ĐÃ làm tròn**, **không** làm tròn lại (ADR-06).</summary>
public sealed record KetQuaTinhTien(
    IReadOnlyList<DongTienHang> CacDong,
    Money TongTruocGiam,
    Money TongGiamGia,
    Money TongPhi,
    Money TongThue,
    Money TongThanhToan,
    string MaChinhSachLamTron)
{
    /// <summary>Số dòng — dùng để test/báo cáo nhanh.</summary>
    public int SoDong => CacDong.Count;

    /// <summary>
    /// 🆕 `PRC-002` `D-219` — `true` khi chính sách `Clamp` **đã kẹp** giảm giá cấp đơn về không vượt thành tiền.
    /// 📌 Đây là **tín hiệu** để chỗ gọi `LogWarning`; `Clamp` không ném nên dữ liệu đi qua kết quả, giữ hàm thuần.
    /// </summary>
    public bool BiKep { get; init; }

    /// <summary>Số lần kẹp (`0` hoặc `1`) — hiện chỉ có tối đa một khoản cấp đơn bị kẹp.</summary>
    public int SoKep { get; init; }
}

/// <summary>Đầu vào tính tiền cho một dòng (đơn giá đã chọn ở B5 — **chưa** làm tròn).</summary>
public sealed record DongCanTinh(string Sku, int Quantity, Money DonGiaChuaLamTron, decimal ThueSuat);

/// <summary>
/// 🚨 **TÍNH TIỀN ĐƠN — HÀM THUẦN** (`PRC-002` §4.2 bước **B6/B7/B8** + **4 điểm làm tròn R1–R4**).
///
/// **ĐÂY LÀ CHỖ "KHÔNG ĐƯỢC MẤT TIỀN"** — mọi sai số ở đây thành **lệch 1–2 đồng** trên hoá đơn, và **khách
/// khiếu nại / kế toán không khớp** (`XC-001` §10 ghi rõ đây là loại lệch rất dễ bị báo nhầm là bug).
///
/// 🚨 **BỐN LUẬT BẤT DI BẤT DỊCH (mỗi luật đều có test riêng):**
///
/// | # | Luật | Nếu vi phạm |
/// |---|---|---|
/// | **R1** | **Đơn giá** làm tròn về số chữ số của tiền tệ (VND = 0) **SAU** khi đã chọn (B5), **KHÔNG** trước | làm tròn **trước** khi so sánh ⇒ **chọn sai bậc/ứng viên** |
/// | **R2/R3** | Giảm giá / phí cấp đơn phân bổ **từng phần** bằng `MoneyAllocator` (phần dư lớn nhất, **bảo toàn tổng**), rồi mới làm tròn **từng phần** | làm tròn **tổng** rồi chia ⇒ **mất/thừa 1–2 đồng** |
/// | **R4** | Thuế tính **theo TỪNG DÒNG** rồi làm tròn **từng dòng** | làm tròn **tổng thuế** ⇒ hoá đơn **không khớp** `Σ` theo mức thuế suất (`XC-001` BR-010) |
/// | — | **Tổng đơn = Σ các dòng ĐÃ làm tròn** — 🚫 **KHÔNG** làm tròn tổng | lệch 1–2 đồng so với tổng hiển thị ⇒ khách khiếu nại |
///
/// 📌 **ADR-08:** chính sách làm tròn được **truyền vào một lần** (snapshot) và **mã chính sách được trả ra**
/// trong kết quả ⇒ giải trình được *"vì sao hôm đó ra số này"*.
///
/// ⚠️ **`quantity < 1` ⇒ NÉM** (`PRC-001` §12: `quantity` phải ≥ 1) — im lặng coi là 1 sẽ tạo dòng tiền **sai**
/// mà không ai biết.
/// </summary>
public static class TinhTienDon
{
    /// <summary>
    /// Tính tiền cho cả đơn.
    /// </summary>
    /// <param name="cacDong">Các dòng hàng (đơn giá đã chọn ở B5, **chưa** làm tròn).</param>
    /// <param name="giamGiaCapDon">Tổng giảm giá cấp đơn (phân bổ theo trọng số thành tiền từng dòng).</param>
    /// <param name="phiCapDon">Tổng phí cấp đơn (phân bổ cùng cách).</param>
    /// <param name="chinhSach">Chính sách làm tròn — **một** cho cả lần tính (ADR-08).</param>
    /// <remarks>
    /// 🆕 `PRC-002` `D-219` — chữ ký **CŨ giữ nguyên tuyệt đối**, uỷ quyền cho <see cref="ChinhSachTongDonAm.Reject"/>
    /// ⇒ hành vi trước WP-16 (ném `eCommerce:Pricing:0010` khi tổng âm thay vì trả số âm).
    /// </remarks>
    public static KetQuaTinhTien Tinh(
        IReadOnlyList<DongCanTinh> cacDong,
        Money giamGiaCapDon,
        Money phiCapDon,
        IRoundingPolicy chinhSach)
        => Tinh(cacDong, giamGiaCapDon, phiCapDon, chinhSach, ChinhSachTongDonAm.Reject);

    /// <summary>
    /// Tính tiền cho cả đơn với **chính sách tổng đơn âm** (`PRC-002` `D-219`).
    /// </summary>
    /// <param name="cacDong">Các dòng hàng (đơn giá đã chọn ở B5, **chưa** làm tròn).</param>
    /// <param name="giamGiaCapDon">Tổng giảm giá cấp đơn (phân bổ theo trọng số thành tiền từng dòng).</param>
    /// <param name="phiCapDon">Tổng phí cấp đơn (phân bổ cùng cách).</param>
    /// <param name="chinhSach">Chính sách làm tròn — **một** cho cả lần tính (ADR-08).</param>
    /// <param name="chinhSachTongDon">
    /// `Reject`: tổng `&lt; 0` ⇒ **NÉM** `PricingErrorCodes.TongDonAm` (tổng `= 0` hợp lệ).
    /// `Clamp`: giảm giá cấp đơn `&gt;` thành tiền ⇒ **kẹp** về thành tiền **trước** phân bổ R2 ⇒ tổng `≥ 0`,
    /// kết quả mang `BiKep = true`, `SoKep = 1`.
    /// </param>
    public static KetQuaTinhTien Tinh(
        IReadOnlyList<DongCanTinh> cacDong,
        Money giamGiaCapDon,
        Money phiCapDon,
        IRoundingPolicy chinhSach,
        ChinhSachTongDonAm chinhSachTongDon)
    {
        ArgumentNullException.ThrowIfNull(cacDong);
        ArgumentNullException.ThrowIfNull(chinhSach);

        if (cacDong.Count == 0)
        {
            throw new ArgumentException("Đơn phải có ít nhất một dòng.", nameof(cacDong));
        }

        var loaiTien = cacDong[0].DonGiaChuaLamTron.Currency;

        // Kiểm tra loại tiền của MỌI đầu vào trước khi cộng — cấm trộn tiền tệ (`PRC-001` BR-010).
        foreach (var d in cacDong)
        {
            YeuCauCungLoaiTien(d.DonGiaChuaLamTron, loaiTien, $"dòng {d.Sku}");

            if (d.Quantity < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cacDong), d.Quantity, $"`quantity` của dòng {d.Sku} phải ≥ 1.");
            }
        }

        YeuCauCungLoaiTien(giamGiaCapDon, loaiTien, "giảm giá cấp đơn");
        YeuCauCungLoaiTien(phiCapDon, loaiTien, "phí cấp đơn");

        // ── R1: làm tròn ĐƠN GIÁ (sau khi đã chọn ở B5) ─────────────────────────────────────────
        var donGiaDaTron = cacDong.Select(d => chinhSach.Round(d.DonGiaChuaLamTron)).ToList();

        // Thành tiền từng dòng = đơn giá đã tròn × số lượng (số nguyên × số đã tròn ⇒ vẫn "đã tròn").
        var thanhTien = donGiaDaTron
            .Select((g, i) => g * cacDong[i].Quantity)
            .ToList();

        var tongTruocGiam = thanhTien.Aggregate(Money.Zero(loaiTien), (a, b) => a + b);

        // ── 🆕 CLAMP (D-219): kẹp giảm giá cấp đơn ≤ thành tiền TRƯỚC khi phân bổ R2 ────────────
        // 📌 Kẹp SAU phân bổ sẽ làm Σ phần không còn khớp khoản gốc, và thuế R4 tính trên số CHƯA kẹp
        // ⇒ hoá đơn tự mâu thuẫn. Phải kẹp ở đây, rồi dùng `giamGiaHieuLuc` cho phân bổ.
        var biKep = false;
        var soKep = 0;
        var giamGiaHieuLuc = giamGiaCapDon;

        if (chinhSachTongDon == ChinhSachTongDonAm.Clamp && giamGiaCapDon.Amount > tongTruocGiam.Amount)
        {
            giamGiaHieuLuc = tongTruocGiam;
            biKep = true;
            soKep = 1;
        }

        // ── R2/R3: PHÂN BỔ giảm giá & phí theo trọng số THÀNH TIỀN, rồi làm tròn TỪNG PHẦN ───────
        var trongSo = thanhTien.Select(t => t.Amount).ToList();

        var giamGiaTungDong = PhanBoVaTron(giamGiaHieuLuc, trongSo, loaiTien, chinhSach);
        var phiTungDong = PhanBoVaTron(phiCapDon, trongSo, loaiTien, chinhSach);

        // ── R4: THUẾ THEO TỪNG DÒNG, làm tròn TỪNG DÒNG ─────────────────────────────────────────
        var cacDongKetQua = new List<DongTienHang>(cacDong.Count);

        for (var i = 0; i < cacDong.Count; i++)
        {
            var sauGiam = thanhTien[i] - giamGiaTungDong[i] + phiTungDong[i];

            var thue = chinhSach.Round(sauGiam * cacDong[i].ThueSuat);

            cacDongKetQua.Add(new DongTienHang(
                cacDong[i].Sku,
                cacDong[i].Quantity,
                donGiaDaTron[i],
                thanhTien[i],
                giamGiaTungDong[i],
                phiTungDong[i],
                thue));
        }

        // ── TỔNG = Σ DÒNG ĐÃ LÀM TRÒN — 🚫 KHÔNG làm tròn tổng (ADR-06) ─────────────────────────
        var tongGiamGia = cacDongKetQua.Aggregate(Money.Zero(loaiTien), (a, d) => a + d.GiamGiaDuocPhanBo);
        var tongPhi = cacDongKetQua.Aggregate(Money.Zero(loaiTien), (a, d) => a + d.PhiDuocPhanBo);
        var tongThue = cacDongKetQua.Aggregate(Money.Zero(loaiTien), (a, d) => a + d.Thue);
        var tongThanhToan = cacDongKetQua.Aggregate(Money.Zero(loaiTien), (a, d) => a + d.ThanhTienSauGiam + d.Thue);

        // ── 🆕 REJECT (D-219): tổng < 0 ⇒ NÉM. Tổng = 0 là HỢP LỆ (đơn 0 đồng + phí vẫn đi được) ──
        // 📌 Kiểm trên TỔNG ĐÃ TÍNH XONG (không side effect) để giữ hàm thuần; `tongThanhToan` không âm khi Clamp.
        if (chinhSachTongDon == ChinhSachTongDonAm.Reject && tongThanhToan.Amount < 0m)
        {
            throw new BusinessException(PricingErrorCodes.TongDonAm)
                .WithData("tong", tongThanhToan.Amount)
                .WithData("giamGiaCapDon", giamGiaCapDon.Amount)
                .WithData("phi", phiCapDon.Amount);
        }

        return new KetQuaTinhTien(
            cacDongKetQua, tongTruocGiam, tongGiamGia, tongPhi, tongThue, tongThanhToan, chinhSach.Ten)
        {
            BiKep = biKep,
            SoKep = soKep
        };
    }

    /// <summary>
    /// Phân bổ một khoản cấp đơn theo trọng số rồi **làm tròn từng phần** — `MoneyAllocator` đã bảo toàn tổng
    /// (phần dư lớn nhất), nên `Σ phần` **luôn bằng** khoản gốc **trước** khi làm tròn; làm tròn **từng phần**
    /// giữ được tính chất đó ở mức đơn vị nhỏ nhất của tiền tệ.
    /// ⚠️ Đơn **0 đồng** (mọi thành tiền = 0) ⇒ trọng số toàn 0 ⇒ `MoneyAllocator` có thể không chia được;
    /// trường hợp đó trả **toàn 0** (giảm giá/phí trên đơn 0 đồng không có nghĩa) — ghi rõ thay vì để ném lỗi khó hiểu.
    /// </summary>
    private static List<Money> PhanBoVaTron(
        Money tong, IReadOnlyList<decimal> trongSo, Currency loaiTien, IRoundingPolicy chinhSach)
    {
        if (trongSo.All(x => x == 0m))
        {
            return trongSo.Select(_ => Money.Zero(loaiTien)).ToList();
        }

        var phan = MoneyAllocator.PhanBo(tong, trongSo, chinhSach);

        return phan.Select(chinhSach.Round).ToList();
    }

    private static void YeuCauCungLoaiTien(Money tien, Currency mongDoi, string moTa)
    {
        if (tien.Currency != mongDoi)
        {
            throw new ArgumentException(
                $"Trộn loại tiền ở {moTa}: {tien.Currency.Code} ≠ {mongDoi.Code}. " +
                "CẤM tự quy đổi (PRC-001 BR-010).", nameof(tien));
        }
    }
}
