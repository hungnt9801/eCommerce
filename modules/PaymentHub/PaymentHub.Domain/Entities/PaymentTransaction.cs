using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.PaymentHub.Payments;

/// <summary>Trạng thái giao dịch thanh toán.</summary>
public enum PaymentTransactionStatus
{
    /// <summary>Đã tạo yêu cầu thanh toán, chưa có tiền.</summary>
    Initiated = 0,

    /// <summary>Đang chờ khách thanh toán / chờ ngân hàng xác nhận.</summary>
    Pending = 1,

    /// <summary>Đã nhận tiền VÀ đã xác thực chữ ký.</summary>
    Succeeded = 2,

    Failed = 3,
    Cancelled = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}

/// <summary>
/// Một giao dịch thanh toán với một cổng.
///
/// ⚠️ NGUYÊN TẮC QUAN TRỌNG NHẤT CỦA MODULE NÀY:
///   "ReturnUrl / redirect page KHÔNG phải bằng chứng thanh toán thành công."
///   Người dùng có thể quay lại URL đó, hoặc tự tạo URL đó.
///   Chỉ IPN/webhook ĐÃ XÁC THỰC CHỮ KÝ (và khớp số tiền) mới được chuyển trạng thái sang Succeeded.
/// </summary>
public class PaymentTransaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã cổng: "vnpay", "momo", "payos", "sepay", "casso"...</summary>
    public string ProviderCode { get; protected set; } = default!;

    /// <summary>Đơn hàng liên quan (có thể null nếu là giao dịch nạp ví riêng).</summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Mã giao dịch phía ta gửi sang cổng. Phải UNIQUE theo (TenantId, ProviderCode).
    /// VNPay yêu cầu <c>vnp_TxnRef</c> KHÔNG được trùng trong ngày.
    /// </summary>
    public string MerchantReference { get; protected set; } = default!;

    /// <summary>Mã giao dịch do cổng cấp (vnp_TransactionNo, payOS reference, MoMo transId...).</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>decimal — không dùng double cho tiền.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentTransactionStatus Status { get; protected set; } = PaymentTransactionStatus.Initiated;

    /// <summary>Thời điểm cổng xác nhận (KHÔNG phải thời điểm ta nhận webhook).</summary>
    public DateTime? PaidAtUtc { get; set; }

    public decimal RefundedAmount { get; set; }

    /// <summary>Payload thô của IPN/webhook cuối cùng — giữ để đối soát và xử lý tranh chấp.</summary>
    public string? LastWebhookPayload { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>Số lần đã nhận webhook cho giao dịch này (để phát hiện gửi trùng).</summary>
    public int WebhookReceivedCount { get; set; }

    /// <summary>
    /// Khoá chống trùng của cổng (mã giao dịch cổng cấp, hoặc id giao dịch ngân hàng với Casso/SePay).
    /// Dùng unique index để bảo đảm xử lý webhook idempotent.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    protected PaymentTransaction() { }

    public PaymentTransaction(Guid id, string providerCode, string merchantReference, decimal amount) : base(id)
    {
        ProviderCode = providerCode;
        MerchantReference = merchantReference;
        Amount = amount;
    }

    /// <summary>
    /// Ghi nhận webhook ĐÃ QUA XÁC THỰC CHỮ KÝ.
    /// Idempotent: nếu giao dịch đã ở trạng thái cuối thì chỉ tăng bộ đếm, không đổi trạng thái.
    /// </summary>
    public bool MarkSucceeded(string? providerTransactionId, string? idempotencyKey, string rawPayload, DateTime? paidAtUtc)
    {
        WebhookReceivedCount++;
        LastWebhookPayload = Truncate(rawPayload, 8000);

        if (Status == PaymentTransactionStatus.Succeeded)
        {
            return false; // webhook gửi trùng — không xử lý lại
        }

        ProviderTransactionId = providerTransactionId ?? ProviderTransactionId;
        IdempotencyKey = idempotencyKey ?? IdempotencyKey;
        Status = PaymentTransactionStatus.Succeeded;
        PaidAtUtc = paidAtUtc ?? DateTime.UtcNow;
        FailureReason = null;
        return true;
    }

    public void MarkFailed(string reason, string? rawPayload = null)
    {
        WebhookReceivedCount++;
        LastWebhookPayload = Truncate(rawPayload ?? LastWebhookPayload, 8000);

        if (Status == PaymentTransactionStatus.Succeeded)
        {
            // Đã có tiền rồi thì không được ghi đè thành thất bại.
            return;
        }

        Status = PaymentTransactionStatus.Failed;
        FailureReason = Truncate(reason, 2000);
    }

    public void RegisterRefund(decimal amount)
    {
        RefundedAmount += amount;
        Status = RefundedAmount >= Amount
            ? PaymentTransactionStatus.Refunded
            : PaymentTransactionStatus.PartiallyRefunded;
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : (value.Length <= max ? value : value[..max]);
}
