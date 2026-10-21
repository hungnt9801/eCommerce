using eCommerce.PaymentHub.Payments;
using Microsoft.Extensions.Options;

namespace eCommerce.PaymentHub.Providers;

/// <summary>
/// 🚨 **CỔNG GIẢ LẬP — CHẠY OFFLINE, CHỮ KÝ VẪN THẬT** (`S8` · `PAY-001` §7.7.2).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO NÓ TỒN TẠI:** `PAY-001` §7.7 chốt *"bypass được lớp **nhà cung cấp**, TUYỆT ĐỐI KHÔNG bypass
/// lớp **chữ ký**"*. Thiếu nó thì **không có cách nào** kiểm đường tiền một cách **tất định** (IPN thật đòi
/// site mở ra Internet + credential), và **hai ca âm quan trọng nhất** — *IPN gửi trùng* và *sai chữ ký* —
/// chỉ đo được bằng cách **tự sinh IPN**.
///
/// 🔒 **BA LỚP, VÀ LỚP NÀO THAY ĐỔI:**
/// <list type="number">
///   <item>① **Ra ngoài** (gọi `vpcpay.html`) — ✅ **THAY** bằng URL trỏ trang giả lập nội bộ. Đây là **nhà cung cấp**,
///     nên thay được — đó chính là lý do `IPaymentProvider` là interface.</item>
///   <item>② **Vào trong** (cổng gọi IPN) — ✅ **THAY** bằng: trang giả lập **tự gọi** `/api/webhooks/vnpay` của **CHÍNH TA**
///     qua HTTP thật. 📌 Không gọi hàm trong tiến trình: đi qua **endpoint thật** mới kiểm được
///     controller + interceptor + chữ ký + DB như cổng thật.</item>
///   <item>③ **Xác thực HMAC + ý nghĩa "đã thu tiền"** — 🔴 **KHÔNG THAY ĐỔI**. <see cref="VerifyWebhookAsync"/>
///     **uỷ quyền** cho <see cref="VnPayPaymentProvider"/>: cùng một thuật toán, cùng một khoá, cùng một
///     đường ghi nhận. 📌 Bypass lớp này là *"ai gửi `vnp_ResponseCode=00` cũng thành đơn đã trả tiền"* —
///     tức **giao hàng miễn phí** (`PAY-001` §7.7.4 #1).</item>
/// </list>
///
/// 🚨 **CHỐT AN TOÀN (thi hành ở `PaymentHubApplicationModule`, không ở đây):** lớp này **chỉ** được đăng ký
/// khi `ASPNETCORE_ENVIRONMENT = Development`; đặt `PaymentHub:Provider = Fake` ở môi trường khác ⇒
/// **NÉM NGAY lúc khởi động**. 📌 Không ném thì hậu quả là *"production nhận thanh toán giả, sổ sách sai mà
/// không có lỗi nào"* — đúng thứ §7.7.4 #3 cấm.
/// </summary>
public class FakePaymentProvider : IPaymentProvider
{
    /// <summary>Mã cổng — **khác** `vnpay` để dữ liệu `PayTransactions` nói thật *"giao dịch này là giả lập"*.
    /// 📌 Nếu dùng lại `vnpay` thì báo cáo đối soát sẽ **trộn** giao dịch thật với giao dịch thử.</summary>
    public const string MaCong = "fake";

    private readonly FakePaymentOptions _options;
    private readonly VnPayPaymentProvider _that;

    public FakePaymentProvider(IOptions<PaymentHubOptions> options, VnPayPaymentProvider that)
    {
        _options = options.Value.Fake;
        _that = that;
    }

    public string ProviderCode => MaCong;

    public bool SupportsRedirectCheckout => true;

    /// <summary>
    /// Trả URL **trang giả lập nội bộ** thay vì URL VNPay. 📌 Không gọi mạng, không cần credential.
    /// 🔑 `returnUrl` được **mang theo** để trang giả lập biết đường quay lại — ⚠️ nhưng nó **KHÔNG phải
    /// bằng chứng thanh toán** (`PAY-001` D2): trang kết quả đọc trạng thái **từ DB**.
    /// </summary>
    public Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SimulatorBaseUrl))
        {
            return Task.FromResult(new CreatePaymentResult
            {
                Success = false,
                Error = "Chưa cấu hình `PaymentHub:Fake:SimulatorBaseUrl` ⇒ không dựng được URL trang giả lập."
            });
        }

        var goc = _options.SimulatorBaseUrl.TrimEnd('/');

        var url = $"{goc}/pay-gia-lap/{Uri.EscapeDataString(request.MerchantReference)}"
                  + $"?returnUrl={Uri.EscapeDataString(request.ReturnUrl ?? string.Empty)}";

        return Task.FromResult(new CreatePaymentResult { Success = true, CheckoutUrl = url });
    }

    /// <summary>
    /// 🚨 **UỶ QUYỀN CHO `VnPayPaymentProvider` — CỐ Ý, KHÔNG VIẾT LẠI.** Đây là **lớp ③** (bảo mật) và
    /// luật của dự án là *"một luật duy nhất"*: viết một bản kiểm chữ ký thứ hai cho cổng giả lập nghĩa là
    /// ta kiểm **bản sao**, không kiểm **code thật** — 📌 và hai bản sẽ lệch nhau đúng vào lúc có sự cố
    /// (tiền lệ `F-PAY-1`: hai quy tắc trong hai hàm ⇒ **từ chối IPN thật**).
    /// </summary>
    public Task<WebhookVerificationResult> VerifyWebhookAsync(
        string rawBody,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
        => _that.VerifyWebhookAsync(rawBody, headers, cancellationToken);

    /// <summary>Hoàn tiền / truy vấn trạng thái: **uỷ quyền** cho VNPay — ở môi trường dev chưa có credential
    /// thì chúng trả lỗi **hạ tầng có mã** như trước `S8` (không có hành vi mới nào để sai).</summary>
    public Task<RefundResult> RefundAsync(
        string merchantReference,
        decimal amount,
        string reason,
        string? providerTransactionId = null,
        CancellationToken cancellationToken = default)
        => _that.RefundAsync(merchantReference, amount, reason, providerTransactionId, cancellationToken);

    /// <inheritdoc />
    public Task<PaymentTransactionStatus> QueryStatusAsync(
        string merchantReference,
        CancellationToken cancellationToken = default)
        => _that.QueryStatusAsync(merchantReference, cancellationToken);
}
