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

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ADDR-004 — SỔ ĐỊA CHỈ (`Address` + `CustomerAddressLink`). Dải **01xx** để KHÔNG đụng 0001…0007
    // (mã cũ đã có nghĩa khác — bài học `cus-001` **F6** ở `ADDR-004` §15.2).
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Không có `Address` (hoặc thuộc tenant khác — cố ý không tiết lộ sự tồn tại).</summary>
    public const string KhongTimThayDiaChi = "eCommerce:Address:0101";

    /// <summary>
    /// Cố sửa **nội dung địa lý** của địa chỉ đã `DaChuanHoa`/`DaXacMinh`.
    /// 🔒 `ADR-05`: nội dung bất biến ⇒ phải tạo **địa chỉ mới** + **liên kết mới**.
    /// </summary>
    public const string DaCoNoiDungDiaLy = "eCommerce:Address:0102";

    /// <summary>Chuyển trạng thái không hợp lệ (kể cả chuyển **lùi**, hoặc thoát khỏi trạng thái hấp thụ).</summary>
    public const string ChuyenTrangThaiKhongHopLe = "eCommerce:Address:0103";

    /// <summary>
    /// **Fail-closed**: không tra được đơn vị hành chính hợp lệ ⇒ **KHÔNG** lưu.
    /// 🔒 `ADR-09`: **cấm** mọi đường "đoán" đơn vị hành chính.
    /// </summary>
    public const string KhongXacDinhDuocDonVi = "eCommerce:Address:0104";

    /// <summary>Thiếu nội dung địa chỉ (rỗng, hoặc vượt độ dài tối đa).</summary>
    public const string ThieuNoiDung = "eCommerce:Address:0105";

    /// <summary>Thiếu **căn cứ** khi xác minh / khi chuẩn hoá (căn cứ là bằng chứng, không phải tuỳ chọn).</summary>
    public const string ThieuCanCuXacMinh = "eCommerce:Address:0106";

    /// <summary>Cố ẩn danh/xoá địa chỉ **còn liên kết hiệu lực** trong sổ của khách (`ADR-11`).</summary>
    public const string ConLienKetHieuLuc = "eCommerce:Address:0107";
}
