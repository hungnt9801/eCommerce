namespace eCommerce.EInvoice;

/// <summary>
/// Mã lỗi riêng cho **cấp số hoá đơn** — dải `eCommerce:EInvoice:02xx`
/// (tách khỏi `01xx` của state machine để tra log là biết ngay nhóm lỗi).
/// </summary>
public static class EInvoiceNumberingErrorCodes
{
    /// <summary>
    /// Không giành được khoá khi cấp số.
    /// 🚨 Đây là lỗi **BẮT BUỘC phải ném**: nếu cứ cấp số khi không giữ khoá thì hai yêu cầu đồng thời
    /// sẽ **cùng nhận một số** ⇒ **trùng số hoá đơn** — lỗi pháp lý nghiêm trọng, phải phát hành lại.
    /// </summary>
    public const string KhongGianhDuocKhoaCapSo = "eCommerce:EInvoice:0201";

    /// <summary>Số hoá đơn không hợp lệ (không phải số, âm, hoặc sai định dạng).</summary>
    public const string SoHoaDonKhongHopLe = "eCommerce:EInvoice:0202";

    /// <summary>Ký hiệu mẫu số rỗng khi cấp số.</summary>
    public const string ThieuKyHieuMauSo = "eCommerce:EInvoice:0203";
}
