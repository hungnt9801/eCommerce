namespace eCommerce.Messaging;

/// <summary>
/// Mã lỗi của miền **Messaging** (`XC-002`), quy ước `eCommerce:Messaging:nnnn`.
/// 🔒 Do CHÍNH module sở hữu (quy ước: module sở hữu hằng số của mình — bài học `XC-003` F2).
/// </summary>
public static class MessagingErrorCodes
{
    /// <summary>
    /// Cùng khoá idempotency nhưng **payload khác** ⇒ KHÔNG được coi là "đã xử lý".
    /// 🚨 Đây là lỗi **phải ném**, không được im lặng trả kết quả cũ: nếu bỏ qua thì một yêu cầu
    /// **khác nội dung** sẽ bị trả về kết quả của yêu cầu trước đó — sai tiền, sai đơn.
    /// </summary>
    public const string PayloadConflict = "eCommerce:Messaging:0001";

    /// <summary>Cùng khoá đang được xử lý ở nơi khác ⇒ tầng HTTP trả <c>409</c> (chốt `XC-003` Q4).</summary>
    public const string DangXuLy = "eCommerce:Messaging:0002";

    /// <summary>Khoá idempotency không hợp lệ (rỗng, quá dài, chứa ký tự cấm).</summary>
    public const string KhoaKhongHopLe = "eCommerce:Messaging:0003";

    /// <summary>Bản ghi idempotency ở trạng thái không cho phép thao tác được yêu cầu.</summary>
    public const string ChuyenTrangThaiKhongHopLe = "eCommerce:Messaging:0004";

    /// <summary>Payload không phải JSON hợp lệ nên không băm được.</summary>
    public const string PayloadKhongHopLe = "eCommerce:Messaging:0005";
}
