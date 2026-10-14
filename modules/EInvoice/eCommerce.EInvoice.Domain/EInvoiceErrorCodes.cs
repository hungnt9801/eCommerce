namespace eCommerce.EInvoice;

/// <summary>
/// Mã lỗi miền **EInvoice** (`EINV-001`), quy ước `eCommerce:EInvoice:nnnn`.
/// 🔒 Do CHÍNH module sở hữu.
///
/// 📌 Dải đang dùng: **0101–0199** (tách khỏi dải 0001–0007 mà `EINV-001`/`EINV-002` từng đề xuất
/// trùng nhau — phát hiện F2 của `EINV-003`).
/// </summary>
public static class EInvoiceErrorCodes
{
    /// <summary>Chuyển trạng thái không hợp lệ (state machine ném lỗi — quy ước #11).</summary>
    public const string ChuyenTrangThaiKhongHopLe = "eCommerce:EInvoice:0101";

    /// <summary>
    /// Cố **phát hành** hoá đơn **CHƯA KÝ**. Đây là ràng buộc pháp lý: quy trình **ký 2 bước**
    /// (`SIGN-001`) phải xong trước khi phát hành.
    /// </summary>
    public const string ChuaKyKhongDuocPhatHanh = "eCommerce:EInvoice:0102";

    /// <summary>Thiếu lý do khi điều chỉnh / thay thế / huỷ (bắt buộc theo quy định).</summary>
    public const string ThieuLyDo = "eCommerce:EInvoice:0103";

    /// <summary>Hoá đơn đã ở trạng thái **kết thúc** (đã huỷ) — không thao tác được nữa.</summary>
    public const string HoaDonDaKetThuc = "eCommerce:EInvoice:0104";

    /// <summary>Chưa phát hành nên chưa được gửi cơ quan thuế.</summary>
    public const string ChuaPhatHanhKhongDuocGuiCQT = "eCommerce:EInvoice:0105";

    /// <summary>Chưa gửi cơ quan thuế nên chưa thể nhận kết quả.</summary>
    public const string ChuaGuiKhongTheNhanKetQua = "eCommerce:EInvoice:0106";

    /// <summary>Thiếu thông tin bắt buộc trên hoá đơn (số, ký hiệu, ngày lập…).</summary>
    public const string ThieuThongTinBatBuoc = "eCommerce:EInvoice:0107";

    /// <summary>
    /// Hoá đơn liên kết (thay thế / điều chỉnh) **vô nghĩa**: rỗng, hoặc trỏ tới chính nó.
    /// Chặn ở tầng domain vì đây là giá trị **chắc chắn sai** mà không cần đọc DB.
    /// </summary>
    public const string ThieuHoaDonLienKet = "eCommerce:EInvoice:0108";
}
