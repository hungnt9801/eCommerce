namespace eCommerce.Address;

/// <summary>
/// Mã lỗi miền **Address** (`ADDR-001`), quy ước `eCommerce:Address:nnnn`.
/// 🔒 Do CHÍNH module sở hữu.
/// </summary>
public static class AddressErrorCodes
{
    /// <summary>Không tìm thấy đơn vị hành chính theo (phiên bản, cấp, mã).</summary>
    public const string KhongTimThayDonVi = "eCommerce:Address:0001";

    /// <summary>Mã ĐVHC sai định dạng (tỉnh 2 chữ số, xã 5 chữ số, huyện 3 chữ số).</summary>
    public const string MaKhongHopLe = "eCommerce:Address:0002";

    /// <summary>Xã không thuộc tỉnh đã khai báo.</summary>
    public const string XaKhongThuocTinh = "eCommerce:Address:0003";

    /// <summary>
    /// Khai báo **cấp huyện** trong schema **2 cấp** — cấp huyện đã bị bỏ từ 01/7/2025
    /// (`Luật 72/2025/QH15`) ⇒ **địa chỉ MỚI không được có huyện**.
    /// </summary>
    public const string HuyenKhongDuocPhepOCap2 = "eCommerce:Address:0004";

    /// <summary>Phiên bản schema ĐVHC không được hỗ trợ.</summary>
    public const string PhienBanKhongHopLe = "eCommerce:Address:0005";

    /// <summary>Mã đã **đóng** (tỉnh/xã bị sáp nhập) — không dùng cho địa chỉ MỚI.</summary>
    public const string MaDaDong = "eCommerce:Address:0006";

    /// <summary>Huyện không thuộc tỉnh đã khai báo (chỉ có nghĩa ở schema 3 cấp).</summary>
    public const string HuyenKhongThuocTinh = "eCommerce:Address:0007";
}
