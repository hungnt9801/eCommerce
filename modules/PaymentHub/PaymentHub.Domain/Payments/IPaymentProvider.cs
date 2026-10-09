namespace eCommerce.PaymentHub.Payments;

public class CreatePaymentRequest
{
    public Guid PaymentTransactionId { get; set; }
    /// <summary>Mã giao dịch phía ta (vnp_TxnRef...).</summary>
    public string MerchantReference { get; set; } = default!;
    /// <summary>Số tiền theo đơn vị nhỏ nhất mà cổng yêu cầu (VNPay: VND × 100).</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string OrderInfo { get; set; } = default!;
    public string ReturnUrl { get; set; } = default!;
    /// <summary>URL nhận IPN/webhook — BẮT BUỘC HTTPS với hầu hết cổng VN.</summary>
    public string IpnUrl { get; set; } = default!;
    public string? ClientIp { get; set; }
    public Dictionary<string, string>? ExtraData { get; set; }
}

public class CreatePaymentResult
{
    public bool Success { get; set; }
    /// <summary>URL để redirect khách sang trang thanh toán (null với cổng webhook-first như Casso/SePay).</summary>
    public string? CheckoutUrl { get; set; }
    /// <summary>QR code dạng chuỗi (VietQR) nếu cổng trả về.</summary>
    public string? QrCodeData { get; set; }
    public string? Error { get; set; }
}

/// <summary>Kết quả xác thực một IPN/webhook đến từ cổng.</summary>
public class WebhookVerificationResult
{
    /// <summary>Chữ ký hợp lệ? Nếu false, TUYỆT ĐỐI không được đổi trạng thái đơn.</summary>
    public bool SignatureValid { get; set; }

    /// <summary>Cổng báo giao dịch thành công (khác với chữ ký hợp lệ).</summary>
    public bool IsSuccess { get; set; }

    public string MerchantReference { get; set; } = default!;
    public string? ProviderTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime? PaidAtUtc { get; set; }
    public string? IdempotencyKey { get; set; }
    public string RawPayload { get; set; } = default!;
    public string? FailureReason { get; set; }

    /// <summary>
    /// Nội dung phải trả về cho cổng để cổng ngừng retry.
    /// Ví dụ VNPay yêu cầu JSON <c>{"RspCode":"00","Message":"Confirm Success"}</c>.
    /// </summary>
    public string? AckResponseBody { get; set; }
}

/// <summary>
/// Một cổng thanh toán. Chi tiết riêng của từng cổng (thuật toán ký, tham số, mã lỗi)
/// phải nằm SAU interface này.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Mã cổng: "vnpay", "momo", "payos", "sepay", "casso"...</summary>
    string ProviderCode { get; }

    /// <summary>
    /// Cổng này có luồng redirect/checkout (VNPay/MoMo/payOS) hay chỉ có webhook
    /// (Casso/SePay — biến động số dư ngân hàng). Cổng webhook-only thì
    /// <see cref="CreatePaymentAsync"/> trả <c>QrCodeData</c> thay vì CheckoutUrl.
    /// </summary>
    bool SupportsRedirectCheckout { get; }

    Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực IPN/webhook: kiểm tra chữ ký, khớp mã giao dịch, khớp SỐ TIỀN và tiền tệ.
    /// Phải chạy được mà KHÔNG cần DB (nhận payload thô + headers).
    /// </summary>
    Task<WebhookVerificationResult> VerifyWebhookAsync(string rawBody, IDictionary<string, string> headers, CancellationToken cancellationToken = default);

    Task<bool> RefundAsync(string merchantReference, decimal amount, string reason, CancellationToken cancellationToken = default);

    /// <summary>Truy vấn trạng thái từ cổng — dùng cho job đối soát (webhook có thể mất).</summary>
    Task<PaymentTransactionStatus> QueryStatusAsync(string merchantReference, CancellationToken cancellationToken = default);
}
