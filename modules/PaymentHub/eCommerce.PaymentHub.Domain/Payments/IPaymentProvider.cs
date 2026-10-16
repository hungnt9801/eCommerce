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

    /// <summary>
    /// 🚨 **YÊU CẦU HOÀN TIỀN** — trả <see cref="RefundResult"/>, **KHÔNG** trả `bool`.
    /// ⚠️ **Xem bất biến ở <see cref="RefundResult"/>**: hoàn tiền là giao dịch **BẤT ĐỒNG BỘ**;
    /// chỉ được ghi sổ đã hoàn khi <see cref="RefundResult.DaHoanThat"/> là `true`.
    /// </summary>
    /// <param name="providerTransactionId">
    /// 🚨 **Mã giao dịch do CỔNG cấp** (`vnp_TransactionNo`), lấy từ IPN đã xác thực
    /// (<c>PaymentTransaction.ProviderTransactionId</c>). **BẮT BUỘC truyền khi đã biết.**
    /// <para>
    /// **VÌ SAO PHẢI CÓ THAM SỐ NÀY (đã cân nhắc và quyết định, không phải "cho đủ"):** VNPay cho phép
    /// tra giao dịch gốc **chỉ** bằng `vnp_TxnRef` (mã của **ta**) — nhưng khi đó cổng phải tự tra, và ca
    /// tra **thất bại** là ca **thật**: giao dịch gốc được tạo ở **môi trường khác** (sandbox vs thật),
    /// hoặc `vnp_TxnRef` **không unique** giữa các kênh. Cổng trả lỗi *"không tìm thấy giao dịch"* —
    /// **không** nói vì sao. Truyền `vnp_TransactionNo` làm đường tra **tất định**.
    /// </para>
    /// <para>
    /// 🔒 **Cấm** tự sinh/đoán giá trị này: nó phải đến từ **IPN đã qua xác thực chữ ký** hoặc
    /// **`querydr` đã qua xác thực chữ ký**. Một `ProviderTransactionId` sai ⇒ hoàn tiền **cho giao dịch
    /// của người khác** (nếu cổng chấp nhận) hoặc lỗi khó hiểu.
    /// </para>
    /// </param>
    Task<RefundResult> RefundAsync(
        string merchantReference,
        decimal amount,
        string reason,
        string? providerTransactionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Truy vấn trạng thái từ cổng — dùng cho job đối soát (webhook có thể mất).</summary>
    Task<PaymentTransactionStatus> QueryStatusAsync(string merchantReference, CancellationToken cancellationToken = default);
}

/// <summary>
/// 🚨 **KẾT QUẢ YÊU CẦU HOÀN TIỀN** (`SAL-006`) — **KIỂU RIÊNG, KHÔNG PHẢI `bool`**.
///
/// **VÌ SAO KHÔNG ĐƯỢC DÙNG `bool` (đây là quyết định thiết kế, không phải sở thích):**
/// hoàn tiền ở mọi cổng VN là **GIAO DỊCH BẤT ĐỒNG BỘ** — cổng **nhận** yêu cầu (mã `00`) nhưng việc
/// chuyển tiền được xử lý **sau**, và **có thể thất bại** (tài khoản nguồn không đủ, ngân hàng từ chối).
/// Nếu API trả `bool true` thì:
/// <list type="bullet">
///   <item>người vận hành **đọc là "đã hoàn xong"** ⇒ **không ai** theo dõi bước xác nhận;</item>
///   <item>khách **chưa** nhận tiền nhưng hệ thống **đã** ghi `Refunded` ⇒ **sổ sách nói dối**;</item>
///   <item>và vì `bool` **không có chỗ** ghi *"mã phản hồi là gì"*, khi sai thì **không điều tra được**.</item>
/// </list>
/// 📌 Đây đúng họ lỗi *"báo thành công trong khi chưa làm xong"* — cùng dạng với việc `ChannelShopSyncer`
/// báo *"đã bàn giao"* trong khi **không ai nhận** ⇒ **mất đơn vĩnh viễn** (`XC-002` §0).
///
/// 🔒 **Bất biến sử dụng:** chỉ được gọi `PaymentTransaction.RegisterRefund` khi
/// <see cref="DaHoanThat"/> là `true`. Khi <see cref="ChoXacNhan"/> là `true` thì **phải** có đường
/// xác nhận bằng `QueryStatusAsync` — **cấm** coi *"cổng đã nhận yêu cầu"* là *"tiền đã ra"*.
/// </summary>
public class RefundResult
{
    /// <summary>Cổng **đã nhận** yêu cầu (mã phản hồi `00`). ⚠️ **KHÔNG** đồng nghĩa tiền đã ra.</summary>
    public bool DaNhanYeuCau { get; set; }

    /// <summary>
    /// 🚨 Tiền **ĐÃ RA** — chỉ `true` khi cổng trả kết quả hoàn tiền **thành công NGAY** (một số ngân hàng
    /// nội bộ xử lý đồng bộ). 🔒 Mặc định **`false`** ⇒ **fail-closed**: không chắc thì **không** ghi sổ.
    /// </summary>
    public bool DaHoanThat { get; set; }

    /// <summary>`true` ⇒ **yêu cầu đã nhận nhưng kết quả CHƯA BIẾT** ⇒ **phải** xác nhận bằng
    /// <see cref="IPaymentProvider.QueryStatusAsync"/>. 🔒 Đây là **trạng thái MẶC ĐỊNH** của hoàn tiền VNPay.</summary>
    public bool ChoXacNhan { get; set; }

    /// <summary>Mã phản hồi của cổng (VNPay: `vnp_ResponseCode`) — **giữ nguyên văn** để điều tra.</summary>
    public string? MaPhanHoi { get; set; }

    /// <summary>Thông điệp của cổng (VNPay: `vnp_Message`).</summary>
    public string? ThongDiep { get; set; }

    /// <summary>Mã giao dịch hoàn tiền do cổng cấp (`vnp_TransactionNo`) — tra chéo khi đối soát.</summary>
    public string? MaGiaoDichHoan { get; set; }

    /// <summary>Lý do thất bại nghiệp vụ (nếu có) — **không** chứa dữ liệu nhạy cảm.</summary>
    public string? LyDoThatBai { get; set; }

    /// <summary>Lỗi **HẠ TẦNG** (timeout, không kết nối được) — ⚠️ **khác hẳn** mã lỗi nghiệp vụ của cổng:
    /// lỗi hạ tầng thì **KHÔNG BIẾT** cổng có nhận được yêu cầu hay không ⇒ **cấm retry mù** (`SAL-006` ADR).</summary>
    public string? LoiHaTang { get; set; }
}
