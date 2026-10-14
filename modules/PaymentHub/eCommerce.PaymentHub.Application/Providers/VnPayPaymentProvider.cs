using System.Net;
using System.Security.Cryptography;
using System.Text;
using eCommerce.PaymentHub.Payments;
using Microsoft.Extensions.Options;

namespace eCommerce.PaymentHub.Payments;

public class VnPayOptions
{
    /// <summary>vnp_TmnCode — mã website do VNPay cấp.</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>vnp_HashSecret — khoá ký HMAC-SHA512. KHÔNG commit giá trị thật.</summary>
    public string HashSecret { get; set; } = string.Empty;

    /// <summary>Sandbox: https://sandbox.vnpayment.vn/paymentv2/vpcpay.html</summary>
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Endpoint truy vấn/hoàn tiền (khác URL thanh toán).</summary>
    public string TransactionApiUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

    public string Version { get; set; } = "2.1.0";
    public string Command { get; set; } = "pay";
    public string CurrCode { get; set; } = "VND";
    public string Locale { get; set; } = "vn";
}

public class PaymentHubOptions
{
    public VnPayOptions VnPay { get; set; } = new();
}

/// <summary>
/// Provider VNPay — bản khung nhưng phần KÝ và XÁC THỰC CHỮ KÝ là code thật.
///
/// Đặc tả đã xác minh (xem docs/architecture.md):
///   • Ký: HMAC-SHA512 trên chuỗi tham số đã SẮP XẾP TĂNG DẦN theo tên, nối bằng '&amp;'
///     dạng <c>key=value</c> (đã URL-encode), TRỪ các tham số <c>vnp_SecureHash*</c>.
///   • vnp_Amount = số tiền × 100 (VNPay dùng đơn vị nhỏ nhất).
///   • vnp_TxnRef KHÔNG được trùng trong cùng một ngày.
///   • IPN BẮT BUỘC HTTPS và phải trả JSON {"RspCode":"00","Message":"Confirm Success"}.
///   • Thành công chỉ khi vnp_ResponseCode == "00" VÀ vnp_TransactionStatus == "00".
///   • ReturnUrl KHÔNG phải bằng chứng thanh toán — chỉ IPN đã xác thực mới được đổi trạng thái.
/// </summary>
public class VnPayPaymentProvider : IPaymentProvider
{
    private readonly VnPayOptions _options;

    /// <summary>📊 Metric — tuỳ chọn (DI inject bản thật; test truyền null vẫn chạy).</summary>
    private readonly eCommerce.Kernel.Observability.eCommerceMetrics? _metrics;

    public VnPayPaymentProvider(
        IOptions<PaymentHubOptions> options,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null)
    {
        _options = options.Value.VnPay;
        _metrics = metrics;
    }

    /// <summary>
    /// 📊 Phát metric **webhook bị TỪ CHỐI** (không đáng tin). Nhãn HỮU HẠN: nhà cung cấp + lý do.
    ///
    /// 🚨 PHÂN BIỆT QUAN TRỌNG — chỉ 2 ca này mới là "từ chối":
    ///   • **thiếu chữ ký**, • **chữ ký sai**.
    ///   **KHÔNG** tính là từ chối khi chữ ký ĐÚNG nhưng `vnp_ResponseCode != 00`: đó là webhook
    ///   **hợp lệ** báo **giao dịch thất bại/khách huỷ**. Nếu tính nhầm thì metric sẽ **báo động giả**
    ///   mỗi lần khách huỷ thanh toán — và cảnh báo mất giá trị.
    /// </summary>
    private void GhiMetricTuChoi(string lyDo)
        => _metrics?.PaymentWebhookRejected.Add(1,
            new KeyValuePair<string, object?>(
                eCommerce.Kernel.Observability.eCommerceMetricTags.Provider, ProviderCode),
            new KeyValuePair<string, object?>(
                eCommerce.Kernel.Observability.eCommerceMetricTags.Reason, lyDo));

    public string ProviderCode => "vnpay";

    public bool SupportsRedirectCheckout => true;

    public Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.AddHours(7); // VNPay dùng giờ Việt Nam (GMT+7)

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = _options.Version,
            ["vnp_Command"] = _options.Command,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Locale"] = _options.Locale,
            ["vnp_CurrCode"] = _options.CurrCode,
            ["vnp_TxnRef"] = request.MerchantReference,
            // ⚠️ VNPay yêu cầu nhân 100. Dùng decimal rồi làm tròn, không dùng double.
            ["vnp_Amount"] = decimal.Round(request.Amount * 100m, 0, MidpointRounding.AwayFromZero).ToString("0"),
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = request.ReturnUrl,
            ["vnp_IpAddr"] = request.ClientIp ?? "127.0.0.1",
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss")
        };

        var queryString = BuildQueryString(parameters);
        var secureHash = HmacSha512(_options.HashSecret, queryString);

        var checkoutUrl = $"{_options.PaymentUrl}?{queryString}&vnp_SecureHash={secureHash}";

        return Task.FromResult(new CreatePaymentResult
        {
            Success = true,
            CheckoutUrl = checkoutUrl
        });
    }

    /// <summary>
    /// Xác thực IPN. Chạy được độc lập với DB: chỉ nhận payload thô + headers.
    /// </summary>
    public Task<WebhookVerificationResult> VerifyWebhookAsync(
        string rawBody,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        // VNPay gửi IPN dạng query string (GET). Hỗ trợ cả trường hợp body chứa query string.
        var parameters = ParseQueryString(rawBody);

        var result = new WebhookVerificationResult { RawPayload = rawBody };

        if (!parameters.TryGetValue("vnp_SecureHash", out var receivedHash))
        {
            GhiMetricTuChoi("thieu-chu-ky");
            result.SignatureValid = false;
            result.IsSuccess = false;
            result.FailureReason = "Thiếu vnp_SecureHash.";
            return Task.FromResult(result);
        }

        // Bỏ các tham số hash trước khi tính lại chữ ký.
        var toSign = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in parameters)
        {
            if (kv.Key is "vnp_SecureHash" or "vnp_SecureHashType")
            {
                continue;
            }

            toSign[kv.Key] = kv.Value;
        }

        var expectedHash = HmacSha512(_options.HashSecret, BuildQueryString(toSign));

        result.SignatureValid = string.Equals(expectedHash, receivedHash, StringComparison.OrdinalIgnoreCase);

        if (!result.SignatureValid)
        {
            GhiMetricTuChoi("chu-ky-sai");
        }
        result.MerchantReference = parameters.GetValueOrDefault("vnp_TxnRef") ?? string.Empty;
        result.ProviderTransactionId = parameters.GetValueOrDefault("vnp_TransactionNo");
        result.Currency = parameters.GetValueOrDefault("vnp_CurrCode") ?? "VND";
        result.RawPayload = rawBody;

        if (decimal.TryParse(parameters.GetValueOrDefault("vnp_Amount"), out var amountX100))
        {
            // Chia lại 100 để so khớp với số tiền nội bộ.
            result.Amount = amountX100 / 100m;
        }

        // Thành công phải thoả CẢ HAI mã.
        var responseCode = parameters.GetValueOrDefault("vnp_ResponseCode");
        var transactionStatus = parameters.GetValueOrDefault("vnp_TransactionStatus");
        result.IsSuccess = result.SignatureValid && responseCode == "00" && transactionStatus == "00";

        if (!result.IsSuccess)
        {
            result.FailureReason = $"vnp_ResponseCode={responseCode}, vnp_TransactionStatus={transactionStatus}";
        }

        // Nội dung bắt buộc để VNPay ngừng retry IPN.
        result.AckResponseBody = result.SignatureValid
            ? "{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}"
            : "{\"RspCode\":\"97\",\"Message\":\"Invalid Checksum\"}";

        return Task.FromResult(result);
    }

    public Task<bool> RefundAsync(string merchantReference, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        // TODO: POST {TransactionApiUrl} với vnp_Command=refund, ký như trên.
        // Lưu ý: hoàn tiền là giao dịch KHÔNG đồng bộ — phải gọi querydr để xác nhận kết quả.
        throw new NotImplementedException("Hoàn tiền VNPay chưa được cài đặt trong khung này.");
    }

    public Task<PaymentTransactionStatus> QueryStatusAsync(string merchantReference, CancellationToken cancellationToken = default)
    {
        // TODO: POST {TransactionApiUrl} với vnp_Command=querydr.
        // ⚠️ BẮT BUỘC có job đối soát dùng hàm này: IPN/webhook có thể mất.
        throw new NotImplementedException("Truy vấn giao dịch VNPay chưa được cài đặt trong khung này.");
    }

    // ---------- helpers ----------

    private static string BuildQueryString(SortedDictionary<string, string> parameters)
        => string.Join("&", parameters.Select(kv =>
            $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

    private static string HmacSha512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = WebUtility.UrlDecode(pair[..idx]);
            var value = WebUtility.UrlDecode(pair[(idx + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
