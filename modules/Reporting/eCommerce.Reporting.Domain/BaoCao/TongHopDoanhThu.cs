using Volo.Abp;

namespace eCommerce.Reporting;

/// <summary>
/// **CHỨNG TỪ BÁN HÀNG ĐÃ CHUẨN HOÁ** — đầu vào của <see cref="TongHopDoanhThu"/>.
///
/// 📌 Chỉ chứa những gì **cần cho doanh thu**: số tiền **cấp chứng từ** (chiết khấu tổng) tách khỏi
/// tiền dòng, đúng như dữ liệu nguồn. `Subtotal` của nguồn **không** được truyền vào đây — nếu truyền,
/// tầng tổng hợp có **hai** nguồn số tiền (`Subtotal` và Σ dòng) và chúng sẽ lệch nhau lúc nào không biết.
/// </summary>
public sealed record ChungTuBanHangNguon(
    NguonBaoCao Nguon,
    Guid ChungTuId,
    string SoChungTu,
    DateOnly NgayKinhDoanh,
    DateTime XayRaLucUtc,
    Guid? KhoId,
    Guid? NhanVienId,
    Guid? KhachId,
    decimal ChietKhauChungTu);

/// <summary>**DÒNG CHỨNG TỪ ĐÃ CHUẨN HOÁ** — đầu vào của <see cref="TongHopDoanhThu"/>.</summary>
public sealed record DongBanHangNguon(
    Guid DongNguonId,
    Guid ChungTuId,
    string Sku,
    string? TenSanPham,
    decimal SoLuong,
    decimal DonGia,
    decimal ChietKhauDong,
    decimal ThanhTienDong,
    decimal? PhanTramVat,
    decimal TienVat);

/// <summary>Kết quả một lượt tính cho MỘT ngày: dòng sự kiện + dòng tổng hợp theo nguồn.</summary>
public sealed record KetQuaTinhNgay(
    IReadOnlyList<DongSuKienBanHang> Dong,
    IReadOnlyList<DongTongHopNgay> TongHop)
{
    /// <summary>Tổng doanh thu đã tính (cộng từ **cột** của dòng tổng hợp).</summary>
    public decimal TongDoanhThu => TongHop.Sum(x => x.DoanhThu);
}

/// <summary>
/// **LÕI THUẦN CỦA BÁO CÁO DOANH THU** (`WP-08`, DoD *"tổng doanh thu báo cáo = SUM(RetailBills + Orders)"*).
///
/// 🚨 **CÔNG THỨC — MỘT CHỖ DUY NHẤT:**
/// <code>
/// doanhThu(chứng từ) = Σ(thanhTienDong) − chietKhauChungTu
/// </code>
/// 🔑 **VÌ SAO `Subtotal − DiscountTotal` (khớp `D-45`) và KHÔNG dùng `TotalPayment`:** `TotalPayment` của
/// hoá đơn POS **gồm VAT** và **trừ tiền điểm** ⇒ báo cáo *"doanh số hàng"* sẽ **không** cộng được với
/// doanh thu đơn online (đơn online không trừ điểm kiểu đó) và **không** đối chiếu được với tiền tích luỹ
/// của khách (`D-45`). 📌 Nếu báo cáo theo `TotalPayment` thì phải **ghi rõ** — xem
/// `BaoCaoDoanhThuDto.DinhNghiaTien` trả về trong API.
///
/// 🔒 **BA LUẬT:**
/// <list type="number">
///   <item>**Chiết khấu &gt; tiền hàng ⇒ NÉM** (`Reports:0004`). Doanh thu âm nghĩa là chứng từ sai dấu;
///     trả `0` cho "đẹp" là **che lỗi dữ liệu** và báo cáo tháng sẽ âm thầm nhỏ hơn thực tế.</item>
///   <item>**Chứng từ KHÔNG có dòng ⇒ KHÔNG ghi dòng tổng hợp** (không bịa một chứng từ 0 đồng). Nếu có
///     chiết khấu mà không có dòng thì đó là chứng từ hỏng ⇒ ném.</item>
///   <item>**Một dòng nguồn chỉ được xuất hiện MỘT lần** (`Reports:0008`). Nếu tầng đọc trả trùng (join
///     nhân bản) mà không chặn, báo cáo **tăng gấp đôi** đúng những dòng bị trùng — sai lệch kiểu tệ nhất.</item>
/// </list>
/// 📌 Hàm **không** đọc DB, không đọc `IClock`: nhờ vậy test được bằng tay và hành vi **tất định**.
/// </summary>
public static class TongHopDoanhThu
{
    /// <summary>Công thức doanh thu của MỘT chứng từ (một chỗ duy nhất — xem chú thích lớp).</summary>
    public static decimal TinhDoanhThuChungTu(decimal tongTienDong, decimal chietKhauChungTu)
    {
        if (tongTienDong < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("tongTienDong", tongTienDong)
                .WithData("lyDo", "Tổng tiền dòng KHÔNG được âm.");
        }

        if (chietKhauChungTu < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("chietKhauChungTu", chietKhauChungTu)
                .WithData("lyDo", "Chiết khấu chứng từ KHÔNG được âm.");
        }

        var doanhThu = Math.Round(tongTienDong - chietKhauChungTu, 2, MidpointRounding.AwayFromZero);

        if (doanhThu < 0m)
        {
            throw new BusinessException(ReportingErrorCodes.DoanhThuAm)
                .WithData("tongTienDong", tongTienDong)
                .WithData("chietKhauChungTu", chietKhauChungTu)
                .WithData("lyDo",
                    "Chiết khấu chứng từ LỚN HƠN tổng tiền dòng ⇒ doanh thu âm. Sửa chứng từ, không làm tròn về 0.");
        }

        return doanhThu;
    }

    /// <summary>
    /// Tính **một ngày** (một nguồn hoặc nhiều nguồn): dòng sự kiện + dòng tổng hợp theo `(Ngày, Nguồn)`.
    /// </summary>
    public static KetQuaTinhNgay TinhNgay(
        IReadOnlyList<ChungTuBanHangNguon> chungTu,
        IReadOnlyList<DongBanHangNguon> dong)
    {
        ArgumentNullException.ThrowIfNull(chungTu);
        ArgumentNullException.ThrowIfNull(dong);

        var theoChungTu = chungTu.ToDictionary(x => x.ChungTuId);
        var daGap = new HashSet<(NguonBaoCao, Guid)>();

        foreach (var ct in chungTu)
        {
            // 🔒 Chứng từ CÓ chiết khấu mà KHÔNG có dòng: doanh thu = 0 − chiết khấu ⇒ âm. Đó là chứng từ
            // hỏng (hoặc dòng đã bị xoá) — phải dừng, không được bỏ qua im lặng.
            if (ct.ChietKhauChungTu > 0m && dong.All(x => x.ChungTuId != ct.ChungTuId))
            {
                throw new BusinessException(ReportingErrorCodes.ChungTuKhongCoDong)
                    .WithData("soChungTu", ct.SoChungTu)
                    .WithData("chietKhauChungTu", ct.ChietKhauChungTu)
                    .WithData("lyDo", "Chứng từ có chiết khấu nhưng KHÔNG có dòng nào.");
            }
        }

        var dongSuKien = new List<DongSuKienBanHang>(dong.Count);

        foreach (var d in dong)
        {
            if (!theoChungTu.TryGetValue(d.ChungTuId, out var ct))
            {
                throw new BusinessException(ReportingErrorCodes.DongMoCoi)
                    .WithData("dongNguonId", d.DongNguonId)
                    .WithData("chungTuId", d.ChungTuId)
                    .WithData("lyDo", "Dòng không thuộc chứng từ nào trong lượt tổng hợp.");
            }

            if (!daGap.Add((ct.Nguon, d.DongNguonId)))
            {
                throw new BusinessException(ReportingErrorCodes.DongTrung)
                    .WithData("dongNguonId", d.DongNguonId)
                    .WithData("nguon", ct.Nguon.ToString())
                    .WithData("lyDo", "Một dòng nguồn xuất hiện HAI lần trong cùng lượt ⇒ doanh thu sẽ bị nhân đôi.");
            }

            dongSuKien.Add(new DongSuKienBanHang(
                ct.NgayKinhDoanh,
                ct.Nguon,
                ct.ChungTuId,
                d.DongNguonId,
                ct.SoChungTu,
                ct.KhoId,
                ct.NhanVienId,
                ct.KhachId,
                d.Sku,
                d.TenSanPham,
                d.SoLuong,
                d.DonGia,
                d.ChietKhauDong,
                d.ThanhTienDong,
                d.PhanTramVat,
                d.TienVat,
                ct.XayRaLucUtc));
        }

        var tongHop = new List<DongTongHopNgay>();

        foreach (var nhomNguon in dongSuKien.GroupBy(x => (x.NgayKinhDoanh, x.Nguon)))
        {
            // 🔑 ĐẾM CHỨNG TỪ THEO NGUỒN CỦA CHÍNH CHỨNG TỪ (không theo dòng): chứng từ không có dòng KHÔNG
            // được đếm là "1 hoá đơn 0 đồng" — nếu đếm, số hoá đơn trên báo cáo khác số hoá đơn thật.
            var ctCuaNguon = chungTu
                .Where(x => x.Nguon == nhomNguon.Key.Nguon && x.NgayKinhDoanh == nhomNguon.Key.NgayKinhDoanh)
                .ToList();

            var ctCoDong = ctCuaNguon
                .Where(ct => nhomNguon.Any(x => x.ChungTuId == ct.ChungTuId))
                .ToList();

            var tongTienDong = Math.Round(ctCoDong.Sum(ct => nhomNguon
                .Where(x => x.ChungTuId == ct.ChungTuId)
                .Sum(x => x.ThanhTienDong)), 2, MidpointRounding.AwayFromZero);

            var chietKhau = Math.Round(ctCoDong.Sum(ct => ct.ChietKhauChungTu), 2, MidpointRounding.AwayFromZero);

            tongHop.Add(new DongTongHopNgay(
                nhomNguon.Key.NgayKinhDoanh,
                nhomNguon.Key.Nguon,
                ctCoDong.Count,
                nhomNguon.Count(),
                Math.Round(nhomNguon.Sum(x => x.SoLuong), 3, MidpointRounding.AwayFromZero),
                tongTienDong,
                chietKhau,
                TinhDoanhThuChungTu(tongTienDong, chietKhau),
                Math.Round(nhomNguon.Sum(x => x.TienVat), 2, MidpointRounding.AwayFromZero)));
        }

        return new KetQuaTinhNgay(dongSuKien, tongHop);
    }

    /// <summary>
    /// **GỘP THEO NGÀY (idempotent)** — trả về danh sách *"đã có, TRỪ phần của ngày/nguồn vừa tính, rồi
    /// thêm phần mới"*.
    ///
    /// 🚨 **VÌ SAO CẦN HÀM NÀY (chứ không chỉ `DELETE` rồi `INSERT`):** đây là **định nghĩa** của
    /// *"chạy lại không nhân đôi"* — một đơn vị **thay thế theo `(ngày, nguồn)`**, không phải *"cộng thêm"*.
    /// 📌 Tầng DB hiện thực đúng định nghĩa đó bằng `DELETE WHERE Ngày/Nguồn` + `INSERT`; unique
    /// `(TenantId, Nguon, DongNguonId)` là **hàng rào cuối** nếu ai đó viết đường ghi thứ hai.
    /// 🔒 Test `Wp08BaoCaoTests` gọi hàm này **hai lần** và khẳng định số dòng + tổng doanh thu **không đổi**.
    /// </summary>
    public static IReadOnlyList<DongSuKienBanHang> GopTheoNgay(
        IReadOnlyList<DongSuKienBanHang> hienCo,
        IReadOnlyList<DongSuKienBanHang> moi)
    {
        ArgumentNullException.ThrowIfNull(hienCo);
        ArgumentNullException.ThrowIfNull(moi);

        var khoaThayThe = moi.Select(x => (x.NgayKinhDoanh, x.Nguon)).ToHashSet();

        return hienCo
            .Where(x => !khoaThayThe.Contains((x.NgayKinhDoanh, x.Nguon)))
            .Concat(moi)
            .ToList();
    }

    /// <summary>Cùng luật <see cref="GopTheoNgay"/> cho dòng **tổng hợp theo ngày**.</summary>
    public static IReadOnlyList<DongTongHopNgay> GopTongHopTheoNgay(
        IReadOnlyList<DongTongHopNgay> hienCo,
        IReadOnlyList<DongTongHopNgay> moi)
    {
        ArgumentNullException.ThrowIfNull(hienCo);
        ArgumentNullException.ThrowIfNull(moi);

        var khoaThayThe = moi.Select(x => (x.NgayKinhDoanh, x.Nguon)).ToHashSet();

        return hienCo
            .Where(x => !khoaThayThe.Contains((x.NgayKinhDoanh, x.Nguon)))
            .Concat(moi)
            .ToList();
    }
}
