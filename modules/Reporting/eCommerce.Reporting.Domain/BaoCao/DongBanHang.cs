namespace eCommerce.Reporting;

/// <summary>
/// **NGUỒN CỦA MỘT BẢNG TỔNG HỢP** (`WP-08`).
///
/// 🔑 **VÌ SAO CẦN MỘT ENUM DÙNG CHUNG cho cả sự kiện bán và mốc tổng hợp:** sổ doanh thu trộn **hai**
/// chứng từ khác nhau (hoá đơn bán lẻ tại quầy + đơn hàng) — chúng không được cộng lẫn vào một khoá
/// (một hoá đơn quầy và một đơn online có thể trùng `Guid` do nhập tay/seed, và cách đọc của người dùng
/// là *"doanh thu quầy"* ≠ *"doanh thu online"*). Ảnh tồn kho là **nguồn thứ ba** chỉ dùng cho mốc tổng hợp.
///
/// 🔒 Số là **hợp đồng dữ liệu** (`D-17`/`D-33`): lưu `int` trong DB ⇒ **CHỈ APPEND** giá trị mới.
/// </summary>
public enum NguonBaoCao
{
    /// <summary>0 — **HOÁ ĐƠN BÁN LẺ** (`PosRetailBills`, `D-18`).</summary>
    BanLe = 0,

    /// <summary>1 — **ĐƠN HÀNG** (`OrdOrders`, kể cả đơn sàn).</summary>
    DonHang = 1,

    /// <summary>
    /// 2 — **ẢNH TỒN KHO** (`InvItems`). 📌 Chỉ có nghĩa cho <see cref="RptWatermark"/> và
    /// `RptFactInventorySnapshots`; **không** được dùng làm nguồn của `RptFactSalesLines` — một "dòng bán"
    /// gắn với kho là **vô nghĩa** và làm doanh thu trùng.
    /// </summary>
    TonKho = 2
}

/// <summary>
/// **MỘT DÒNG DOANH THU ĐÃ TỔNG HỢP** — `RptFactSalesLines` (`07 §WP-08`: `FactSalesLine`).
///
/// 🔑 **GRAIN = MỘT DÒNG CHỨNG TỪ NGUỒN** (`DongNguonId`): đó là khoá tự nhiên để job chạy lại **hai
/// lần** không nhân đôi (unique `(TenantId, Nguon, DongNguonId)` ở DB — hàng rào cuối, vì tầng ứng dụng
/// không đủ sức chặn hai lượt chạy đồng thời).
///
/// 📌 **`DonGia`/`ChietKhauDong`/`ThanhTienDong` là ẢNH CHỤP lúc tổng hợp**: sửa giá bán sau này
/// **không** được làm đổi báo cáo của kỳ đã chốt — cùng nguyên tắc *"chứng từ bất biến"* của `WP-06`.
/// </summary>
public sealed record DongSuKienBanHang(
    DateOnly NgayKinhDoanh,
    NguonBaoCao Nguon,
    Guid ChungTuId,
    Guid DongNguonId,
    string SoChungTu,
    Guid? KhoId,
    Guid? NhanVienId,
    Guid? KhachId,
    string Sku,
    string? TenSanPham,
    decimal SoLuong,
    decimal DonGia,
    decimal ChietKhauDong,
    decimal ThanhTienDong,
    decimal? PhanTramVat,
    decimal TienVat,
    DateTime XayRaLucUtc);

/// <summary>
/// **MỘT DÒNG DOANH THU THEO NGÀY + NGUỒN** — `RptAggDailySales` (`07 §WP-08`: `AggDaily*`).
///
/// 🚨 **`DoanhThu` KHÔNG được tính lại từ `DongSuKienBanHang` ở tầng đọc.** Nó là **cột đã ghi**:
/// `DoanhThu = TongTienDong − ChietKhauChungTu` (công thức **một chỗ** —
/// <see cref="TongHopDoanhThu.TinhDoanhThuChungTu"/>). 📌 Nếu tầng đọc tự cộng lại thì sớm muộn hai chỗ
/// hiểu chiết khấu khác nhau ⇒ *"báo cáo tháng này lệch 1 đồng"* và không ai tìm ra.
/// </summary>
public sealed record DongTongHopNgay(
    DateOnly NgayKinhDoanh,
    NguonBaoCao Nguon,
    int SoChungTu,
    int SoDong,
    decimal SoLuong,
    decimal TongTienDong,
    decimal ChietKhauChungTu,
    decimal DoanhThu,
    decimal TienVat);
