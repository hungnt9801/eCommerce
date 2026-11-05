using Volo.Abp;

namespace eCommerce.Pos;

/// <summary>
/// 🚨 **HÀM THUẦN: TIỀN CỦA MỘT HOÁ ĐƠN BÁN LẺ** (`WP-06`, `D-42`) — **nguồn sự thật DUY NHẤT** của
/// *"hoá đơn này bao nhiêu tiền"*.
///
/// 🔑 **VÌ SAO TÁCH RA HÀM THUẦN:**
/// <list type="bullet">
///   <item>Tầng Domain **không** phụ thuộc EF/DB ⇒ test được **không cần Postgres** (cùng khuôn
///     `TinhTienChungTu` của `WP-04c`);</item>
///   <item>UI **không** tự nhân lại: DTO trả số đã tính ⇒ không có chuyện *"UI hiện 99.000 mà DB ghi 100.000"*.
///     Hai nơi cùng tính một công thức là mầm lệch số **không có lỗi nào**;</item>
///   <item>Ba luật tiền của Nhanh (`nhanh-pos-module-map.md` §5.1) nằm **một chỗ**: ① chiết khấu dòng **HOẶC**
///     bill (không cả hai); ② điểm tiêu trừ **sau** chiết khấu; ③ tổng thanh toán **không** âm.</item>
/// </list>
///
/// 📌 **THỨ TỰ TÍNH (ghi rõ để không phải suy luận):**
/// <code>
/// thành tiền dòng = SL × đơn giá − chiết khấu dòng        (chiết khấu dòng ≤ SL × đơn giá)
/// tiền hàng       = Σ thành tiền dòng
/// VAT             = Σ (thành tiền dòng × %VAT dòng / 100) (chỉ dòng CÓ khai %VAT)
/// tổng thanh toán = tiền hàng − chiết khấu bill − tiền điểm + VAT
/// </code>
/// ⚠️ VAT tính **trên thành tiền dòng SAU chiết khấu dòng**; chiết khấu tổng và điểm trừ ở **bước cuối**
/// (không phân bổ ngược vào dòng) — cùng giả định `D-37`: **giá chưa gồm VAT**.
/// </summary>
public static class TinhTienHoaDon
{
    /// <summary>Tỉ lệ quy đổi điểm mặc định của `WP-06`: **1 điểm = 1.000 đ** (cấu hình được ở `WP-07`).</summary>
    public const decimal TyLeDiemMacDinh = 1000m;

    /// <summary>Một dòng đưa vào tính tiền (dữ liệu **thô** từ client, chưa qua entity).</summary>
    public sealed record DongVao(decimal SoLuong, decimal DonGia, decimal? ChietKhauDong = null, decimal? VatPercent = null);

    /// <summary>Kết quả tính tiền của **toàn** hoá đơn.</summary>
    public sealed record KetQua(
        decimal TienHang,
        decimal ChietKhauDong,
        decimal TienVat,
        decimal ChietKhauHoaDon,
        decimal TienDiem,
        decimal TongThanhToan);

    /// <summary>
    /// **Thành tiền MỘT dòng** = `SL × đơn giá − chiết khấu dòng`.
    /// 🚨 Ném `eCommerce:Pos:0002` khi SL ≤ 0 và `eCommerce:Pos:0003` khi giá/chiết khấu âm hoặc
    /// **chiết khấu vượt thành tiền** — bán âm tiền là lỗi dữ liệu, không phải "khuyến mại".
    /// </summary>
    public static decimal ThanhTienDong(decimal soLuong, decimal donGia, decimal chietKhauDong = 0m)
    {
        if (soLuong <= 0m)
        {
            throw new BusinessException(PosErrorCodes.SoLuongKhongHopLe)
                .WithData("soLuong", soLuong)
                .WithData("lyDo", "Số lượng dòng phải LỚN HƠN 0.");
        }

        if (donGia < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("donGia", donGia)
                .WithData("lyDo", "Đơn giá KHÔNG được âm. Muốn tặng hàng thì đặt giá 0 + chiết khấu 100%.");
        }

        if (chietKhauDong < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("chietKhauDong", chietKhauDong)
                .WithData("lyDo", "Chiết khấu dòng KHÔNG được âm (âm = tăng giá, hãy sửa đơn giá).");
        }

        var goc = soLuong * donGia;

        if (chietKhauDong > goc)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("chietKhauDong", chietKhauDong)
                .WithData("thanhTienTruocChietKhau", goc)
                .WithData("lyDo", "Chiết khấu dòng VƯỢT thành tiền dòng.");
        }

        return goc - chietKhauDong;
    }

    /// <summary>
    /// **Tiền VAT của một dòng** — `null` khi dòng **không khai** %VAT (khác `0`: *"chưa khai"* ≠ *"0%"*,
    /// cùng lý do `D-37`). Ném `0003` khi %VAT ngoài `[0, 100]`.
    /// </summary>
    public static decimal? TienVat(decimal thanhTien, decimal? vatPercent)
    {
        if (vatPercent is null)
        {
            return null;
        }

        if (vatPercent is < 0m or > 100m)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("vatPercent", vatPercent)
                .WithData("lyDo", "%VAT phải trong khoảng 0…100.");
        }

        return Math.Round(thanhTien * vatPercent.Value / 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// **Tính TOÀN hoá đơn** — ném đúng mã cho từng luật (xem chú thích lớp).
    /// </summary>
    public static KetQua Tinh(
        IReadOnlyCollection<DongVao> dong,
        decimal chietKhauHoaDon = 0m,
        decimal tienDiem = 0m)
    {
        if (dong.Count == 0)
        {
            throw new BusinessException(PosErrorCodes.HoaDonThieuDong)
                .WithData("lyDo", "Hoá đơn phải có ÍT NHẤT một dòng hàng.");
        }

        if (chietKhauHoaDon < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("chietKhauHoaDon", chietKhauHoaDon)
                .WithData("lyDo", "Chiết khấu hoá đơn KHÔNG được âm.");
        }

        if (tienDiem < 0m)
        {
            throw new BusinessException(PosErrorCodes.DiemKhongHopLe)
                .WithData("tienDiem", tienDiem)
                .WithData("lyDo", "Tiền quy đổi từ điểm KHÔNG được âm.");
        }

        decimal tienHang = 0m;
        decimal chietKhauDongTong = 0m;
        decimal tienVat = 0m;

        foreach (var d in dong)
        {
            var chietKhau = d.ChietKhauDong ?? 0m;
            var thanhTien = ThanhTienDong(d.SoLuong, d.DonGia, chietKhau);

            tienHang += thanhTien;
            chietKhauDongTong += chietKhau;
            tienVat += TienVat(thanhTien, d.VatPercent) ?? 0m;
        }

        // 🚨 LUẬT NHANH (§5.1): **chiết khấu dòng HOẶC chiết khấu tổng**, không dùng đồng thời.
        // 📌 Chặn ở ĐÂY (domain) chứ không chỉ ở UI: UI chặn được người bấm tay, còn API/import/offline
        //    gửi lại thì không ai chặn — mà hai tầng chiết khấu cùng lúc là **giảm giá hai lần** trên cùng
        //    món hàng, không có lỗi nào để lần ra (doanh thu chỉ "thấp hơn dự kiến").
        if (chietKhauHoaDon > 0m && chietKhauDongTong > 0m)
        {
            throw new BusinessException(PosErrorCodes.ChietKhauHaiTang)
                .WithData("chietKhauDong", chietKhauDongTong)
                .WithData("chietKhauHoaDon", chietKhauHoaDon)
                .WithData("lyDo", "Một hoá đơn chỉ được chiết khấu THEO DÒNG **hoặc** THEO TỔNG, không cả hai.");
        }

        if (chietKhauHoaDon > tienHang)
        {
            throw new BusinessException(PosErrorCodes.ChietKhauVuotTienHang)
                .WithData("chietKhauHoaDon", chietKhauHoaDon)
                .WithData("tienHang", tienHang);
        }

        var sauChietKhau = tienHang - chietKhauHoaDon;

        if (tienDiem > sauChietKhau)
        {
            throw new BusinessException(PosErrorCodes.DiemKhongHopLe)
                .WithData("tienDiem", tienDiem)
                .WithData("toiDa", sauChietKhau)
                .WithData("lyDo", "Tiền quy đổi từ điểm VƯỢT số phải trả còn lại.");
        }

        var tong = sauChietKhau - tienDiem + tienVat;

        if (tong < 0m)
        {
            throw new BusinessException(PosErrorCodes.TongThanhToanAm)
                .WithData("tongThanhToan", tong)
                .WithData("lyDo", "Tổng thanh toán ÂM — kiểm lại chiết khấu/điểm.");
        }

        return new KetQua(
            TienHang: Math.Round(tienHang, 2, MidpointRounding.AwayFromZero),
            ChietKhauDong: Math.Round(chietKhauDongTong, 2, MidpointRounding.AwayFromZero),
            TienVat: Math.Round(tienVat, 2, MidpointRounding.AwayFromZero),
            ChietKhauHoaDon: Math.Round(chietKhauHoaDon, 2, MidpointRounding.AwayFromZero),
            TienDiem: Math.Round(tienDiem, 2, MidpointRounding.AwayFromZero),
            TongThanhToan: Math.Round(tong, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// **Tiền quy đổi từ điểm** = `soDiem × tyLe`. Ném `0015` khi số điểm âm hoặc tỉ lệ ≤ 0
    /// (tỉ lệ 0 sẽ biến *"tiêu 100 điểm"* thành **miễn phí 0 đồng** — im lặng và sai).
    /// </summary>
    public static decimal QuyDoiDiem(decimal soDiem, decimal tyLe = TyLeDiemMacDinh)
    {
        if (soDiem < 0m)
        {
            throw new BusinessException(PosErrorCodes.DiemKhongHopLe)
                .WithData("soDiem", soDiem)
                .WithData("lyDo", "Số điểm tiêu KHÔNG được âm.");
        }

        if (tyLe <= 0m)
        {
            throw new BusinessException(PosErrorCodes.DiemKhongHopLe)
                .WithData("tyLe", tyLe)
                .WithData("lyDo", "Tỉ lệ quy đổi điểm phải LỚN HƠN 0.");
        }

        return Math.Round(soDiem * tyLe, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// **Kiểm phương thức thanh toán** — ném `0008` khi giá trị **không nằm trong danh mục**
    /// <see cref="RetailPaymentMethod"/>. 📌 Kiểm bằng `Enum.IsDefined` **trên tập đã khai**: client
    /// gửi số `99` sẽ lọt mọi `switch` (rơi vào nhánh mặc định) ⇒ tiền **biến mất** khỏi báo cáo theo
    /// phương thức mà **không có lỗi nào**.
    /// </summary>
    public static void KiemPhuongThuc(IEnumerable<RetailPaymentMethod> phuongThuc)
    {
        foreach (var pt in phuongThuc)
        {
            if (!Enum.IsDefined(pt))
            {
                throw new BusinessException(PosErrorCodes.PhuongThucKhongHopLe)
                    .WithData("phuongThuc", (int)pt)
                    .WithData("lyDo", "Phương thức thanh toán KHÔNG nằm trong danh mục RetailPaymentMethod.");
            }
        }
    }

    /// <summary>
    /// **Chia phần đã trả**: `tiền thừa` (khách đưa thừa) và `còn nợ` — **không bao giờ cả hai &gt; 0**.
    /// 📌 `tiền thừa` trả lại khách bằng **tiền mặt** ⇒ `CashSession` phải trừ nó khỏi doanh thu tiền mặt
    /// (nếu không, két đếm ra **thiếu** đúng bằng tiền thối).
    /// </summary>
    public static (decimal TienThua, decimal ConNo) ChiaPhanDaTra(decimal tongThanhToan, decimal daTra)
    {
        if (daTra < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienKhongHopLe)
                .WithData("daTra", daTra)
                .WithData("lyDo", "Số tiền đã trả KHÔNG được âm.");
        }

        if (daTra > tongThanhToan)
        {
            return (Math.Round(daTra - tongThanhToan, 2, MidpointRounding.AwayFromZero), 0m);
        }

        return (0m, Math.Round(tongThanhToan - daTra, 2, MidpointRounding.AwayFromZero));
    }
}
