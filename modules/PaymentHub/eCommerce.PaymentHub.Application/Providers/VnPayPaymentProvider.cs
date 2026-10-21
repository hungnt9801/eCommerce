using System.Net;
using System.Security.Cryptography;
using System.Text;
using eCommerce.PaymentHub.Payments;
using eCommerce.PaymentHub.Providers;
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

    /// <summary>
    /// Cấu hình **cổng giả lập** (`S8`). 🔒 **Chỉ dùng ở `Development`** — đăng ký có điều kiện ở
    /// `PaymentHubApplicationModule` (đặt `PaymentHub:Provider = Fake` ngoài Development ⇒ **ném lúc khởi động**).
    /// </summary>
    public Providers.FakePaymentOptions Fake { get; set; } = new();
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

    /// <summary>
    /// 🌐 `HttpClient` để gọi API giao dịch của VNPay (`refund`, `querydr`).
    /// ⚠️ **Tuỳ chọn** (`null` được phép): nếu Host **chưa** cấu hình `AddHttpClient`, provider vẫn dựng được
    /// và `CreatePaymentAsync`/`VerifyWebhookAsync` (thuần) vẫn chạy; chỉ `RefundAsync`/`QueryStatusAsync`
    /// trả lỗi **hạ tầng có mã** thay vì **NỔ** `NullReferenceException`.
    /// 📌 Vì sao quan trọng: một `NullReferenceException` ở đây sẽ bị nuốt thành *"hoàn tiền thất bại"* chung
    /// chung ⇒ **không ai** biết là **lỗi cấu hình DI** — mất hàng giờ điều tra sai hướng.
    /// </summary>
    private readonly HttpClient? _http;

    public VnPayPaymentProvider(
        IOptions<PaymentHubOptions> options,
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null,
        HttpClient? http = null)
    {
        _options = options.Value.VnPay;
        _metrics = metrics;
        _http = http;
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
    ///
    /// ══════════════════════════════════════════════════════════════════════════════════════════════════
    /// 🚨 **SỬA `S5` (18/10/2026) — `F-PAY-1`: HÀM NÀY TỪNG TỰ DỰNG CHUỖI KÝ THEO MỘT LUẬT KHÁC.**
    /// Bản trước **giữ** tham số rỗng và **giải mã rồi encode lại** từng giá trị, trong khi `KiemChuKy` (cùng
    /// tệp ý niệm, khác tệp vật lý) **bỏ** tham số rỗng. Hệ quả **đo được bằng lý luận, không phải suy đoán**:
    /// IPN thật có `vnp_BankCode=` (rỗng) ⇒ ta tính hash **khác** ⇒ `SignatureValid = false` ⇒ **không ghi nhận
    /// tiền đã vào** ⇒ **mất tiền im lặng**; lưới an toàn duy nhất còn lại là job `querydr`.
    ///
    /// ✅ **Nay chỉ còn MỘT luật:** gọi <see cref="VnPayRequestBuilder.KiemChuKyTuQueryTho"/> — hàm dựng chuỗi ký
    /// từ **query THÔ** (byte của cổng), bỏ tham số rỗng, sắp xếp `Ordinal`. 📌 Không còn bản sao thứ hai của
    /// thuật toán trong lớp này, nên **không còn chỗ** để hai luật lệch nhau.
    /// </summary>
    public Task<WebhookVerificationResult> VerifyWebhookAsync(
        string rawBody,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        // VNPay gửi IPN dạng query string (GET). Hỗ trợ cả trường hợp body chứa query string.
        var parameters = ParseQueryString(rawBody);

        // 📌 Điền **tham số dữ kiện** TRƯỚC khi kiểm chữ ký: khi từ chối, log/metric vẫn phải nói được
        // *"IPN của giao dịch nào"* — thiếu chúng thì ca "sai chữ ký" là một dòng vô nghĩa.
        var result = new WebhookVerificationResult
        {
            RawPayload = rawBody,
            MerchantReference = parameters.GetValueOrDefault("vnp_TxnRef") ?? string.Empty,
            ProviderTransactionId = parameters.GetValueOrDefault("vnp_TransactionNo"),
            Currency = parameters.GetValueOrDefault("vnp_CurrCode") ?? "VND"
        };

        if (decimal.TryParse(parameters.GetValueOrDefault("vnp_Amount"), out var amountX100))
        {
            // Chia lại 100 để so khớp với số tiền nội bộ.
            result.Amount = amountX100 / 100m;
        }

        // 🚨 **FAIL-CLOSED KHI CHƯA CẤU HÌNH KHOÁ KÝ** (`S5` — phát hiện trong chính phiên này).
        // Trước `S5` nhánh này **không tồn tại**: `HashSecret` rỗng ⇒ `HMACSHA512` vẫn chạy với **khoá rỗng** ⇒
        // người ngoài tính được hash bằng khoá rỗng ⇒ **ký được IPN giả** ⇒ `MarkSucceeded` ⇒ *"đơn đã trả tiền"*
        // ⇒ **giao hàng miễn phí** (`PAY-001` §7.7.4 #1). 📌 Đúng trạng thái repo hôm nay (`appsettings.json` để
        // `HashSecret: ""`), nên nếu endpoint IPN lên mà thiếu nhánh này thì **lỗ hổng thành hiện thực**.
        if (string.IsNullOrWhiteSpace(_options.HashSecret))
        {
            GhiMetricTuChoi("thieu-hashsecret");

            result.SignatureValid = false;
            result.IsSuccess = false;
            result.FailureReason =
                "CHƯA cấu hình `PaymentHub:VnPay:HashSecret` ⇒ KHÔNG kiểm được chữ ký ⇒ từ chối IPN (fail-closed).";
            result.AckResponseBody =
                "{\"RspCode\":\"97\",\"Message\":\"Invalid Checksum\"}";

            return Task.FromResult(result);
        }

        // 🔑 MỘT luật duy nhất cho chữ ký IPN (xem chú thích hàm).
        result.SignatureValid = VnPayRequestBuilder.KiemChuKyTuQueryTho(rawBody, _options.HashSecret, out var lyDo);

        if (!result.SignatureValid)
        {
            GhiMetricTuChoi(lyDo ?? VnPayRequestBuilder.LyDoTuChoiChuKy.ChuKySai);

            result.IsSuccess = false;
            result.FailureReason = $"Chữ ký KHÔNG hợp lệ ({lyDo}).";
        }

        // Thành công phải thoả CẢ HAI mã.
        var responseCode = parameters.GetValueOrDefault("vnp_ResponseCode");
        var transactionStatus = parameters.GetValueOrDefault("vnp_TransactionStatus");
        result.IsSuccess = result.SignatureValid && responseCode == "00" && transactionStatus == "00";

        if (!result.IsSuccess && result.FailureReason is null)
        {
            result.FailureReason = $"vnp_ResponseCode={responseCode}, vnp_TransactionStatus={transactionStatus}";
        }

        // Nội dung bắt buộc để VNPay ngừng retry IPN.
        result.AckResponseBody = result.SignatureValid
            ? "{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}"
            : "{\"RspCode\":\"97\",\"Message\":\"Invalid Checksum\"}";

        return Task.FromResult(result);
    }

    /// <summary>
    /// 🚨 **GỬI YÊU CẦU HOÀN TIỀN** (`SAL-006`) — `vnp_Command=refund` (API giao dịch, **POST JSON**).
    ///
    /// 🔒 **BẤT BIẾN QUAN TRỌNG NHẤT: mã `00` NGHĨA LÀ "CỔNG ĐÃ NHẬN", KHÔNG PHẢI "TIỀN ĐÃ RA".**
    /// VNPay xử lý hoàn tiền **bất đồng bộ** ở phía ngân hàng ⇒ hàm này **luôn** trả
    /// `DaHoanThat = false` + `ChoXacNhan = true` khi cổng nhận yêu cầu. Tầng gọi **phải** xác nhận bằng
    /// <see cref="QueryStatusAsync"/> **trước khi** gọi `PaymentTransaction.RegisterRefund`.
    /// 📌 Lý do (đã trả giá ở dạng khác trong dự án): ghi *"đã hoàn"* khi tiền chưa ra ⇒**sổ sách nói dối**,
    /// khách khiếu nại, và **không ai** theo dõi bước xác nhận.
    ///
    /// ⚠️ **LỖI HẠ TẦNG ≠ LỖI NGHIỆP VỤ** (đây là chỗ dễ mất tiền nhất):
    /// timeout / không kết nối được ⇒ **KHÔNG BIẾT** cổng có nhận được yêu cầu hay không.
    /// ⇒ Trả `LoiHaTang` **và** `ChoXacNhan = true` — **cấm** người gọi *"retry mù"* (có thể hoàn **HAI LẦN**).
    /// Đường đúng: **tra `querydr`** rồi mới quyết định.
    /// </summary>
    public async Task<RefundResult> RefundAsync(
        string merchantReference,
        decimal amount,
        string reason,
        string? providerTransactionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(merchantReference))
        {
            return new RefundResult { LyDoThatBai = "Thiếu mã giao dịch gốc (`merchantReference`)." };
        }

        if (amount <= 0m)
        {
            return new RefundResult { LyDoThatBai = "Số tiền hoàn phải LỚN HƠN 0." };
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            // 🚨 Lý do là tham số **bắt buộc** của cổng (`vnp_OrderInfo`) VÀ là dấu vết audit:
            // hoàn tiền mà không ghi lý do thì 6 tháng sau không ai giải thích được vì sao tiền ra.
            return new RefundResult { LyDoThatBai = "Hoàn tiền BẮT BUỘC có lý do (vnp_OrderInfo + audit)." };
        }

        if (_http is null)
        {
            return new RefundResult
            {
                LoiHaTang = "Chưa cấu hình HttpClient cho PaymentHub ⇒ KHÔNG gọi được cổng.",
                LyDoThatBai = "Lỗi CẤU HÌNH (không phải lỗi cổng): thiếu `AddHttpClient`. Không được coi là 'đã hoàn'."
            };
        }

        // ⚠️ Không biết số tiền gốc ⇒ **không** tự đoán `02`/`03`. VNPay sẽ tự kiểm và từ chối nếu sai loại
        // kèm mã lỗi rõ ràng — **tốt hơn** là ta đoán sai rồi hoàn một phần trong khi ý định là toàn phần.
        // 📌 Người gọi có thể truyền `amount` bằng đúng số đã thu ⇒ khi đó `03` (một phần) vẫn hoàn đúng số tiền.
        var thamSo = VnPayRequestBuilder.ThamSoHoanTien(
            _options.TmnCode, merchantReference, amount,
            laHoanToanPhan: false, lyDo: reason, nguoiThucHien: "eCommerce",
            maGiaoDichCuaCong: providerTransactionId);

        return await GoiApiGiaoDichAsync(thamSo, cancellationToken);
    }

    /// <summary>
    /// **TRUY VẤN TRẠNG THÁI GIAO DỊCH** (`vnp_Command=querydr`) — `SAL-006` **BR** và `REC-001`.
    ///
    /// 📌 **VÌ SAO BẮT BUỘC PHẢI CÓ (không phải "tính năng thêm"):** IPN/webhook **có thể mất**
    /// (mạng, deploy, cổng bỏ sót). Không có `querydr` thì giao dịch đã thu tiền **mãi mãi** ở `Pending`,
    /// và **không ai biết** khách đã trả hay chưa ⇒ đơn không được xử lý, hoặc bị huỷ oan.
    ///
    /// 🔒 **Ánh xạ mã phản hồi (đọc kỹ vì đây là chỗ suy diễn nguy hiểm):**
    /// <list type="bullet">
    ///   <item>`00` ⇒ **đã thu tiền** ⇒ `Succeeded`;</item>
    ///   <item>`01`/`02`/`04`/`07` ⇒ giao dịch **chưa/không** thành công ⇒ `Failed` (khách chưa trả);</item>
    ///   <item>`24` ⇒ khách **huỷ** ⇒ `Cancelled` (⚠️ khác `Failed`: khách **cố ý** huỷ, không phải lỗi);</item>
    ///   <item>khác ⇒ **KHÔNG ĐOÁN**: trả `Pending` — nghĩa là *"vẫn chưa biết"*, để job đối soát gọi lại.
    ///     📌 Ánh xạ bừa một mã lạ sang `Failed` sẽ **chôn** một giao dịch đã thu tiền ⇒ **mất tiền im lặng**.</item>
    /// </list>
    /// ⚠️ Lỗi hạ tầng/cấu hình ⇒ **`Pending`** (không phải `Failed`): *"chưa biết"* **khác** *"không thành công"*.
    /// </summary>
    public async Task<PaymentTransactionStatus> QueryStatusAsync(
        string merchantReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(merchantReference) || _http is null)
        {
            return PaymentTransactionStatus.Pending;
        }

        var thamSo = VnPayRequestBuilder.ThamSoTruyVan(_options.TmnCode, merchantReference);
        var kq = await GoiApiGiaoDichAsync(thamSo, cancellationToken);

        if (kq.LoiHaTang is not null || !kq.DaNhanYeuCau)
        {
            // ⚠️ "Chưa biết" KHÁC "không thành công" — trả `Pending` để job còn gọi lại.
            return PaymentTransactionStatus.Pending;
        }

        return kq.MaPhanHoi switch
        {
            VnPayRequestBuilder.MaThanhCong => PaymentTransactionStatus.Succeeded,
            "24" => PaymentTransactionStatus.Cancelled,
            "01" or "02" or "04" or "07" => PaymentTransactionStatus.Failed,
            _ => PaymentTransactionStatus.Pending
        };
    }

    /// <summary>
    /// 🌐 Gọi `{TransactionApiUrl}` với tham số đã ký, rồi đọc phản hồi.
    /// 🔒 **Mọi** lỗi hạ tầng được **BẮT** và chuyển thành <see cref="RefundResult.LoiHaTang"/> —
    /// **không** để exception lọt lên: exception thô ở tầng gọi sẽ bị hiểu là *"hoàn tiền thất bại"*
    /// trong khi sự thật là *"**chưa biết** cổng có nhận hay không"* → hai kết luận **trái ngược**.
    /// </summary>
    private async Task<RefundResult> GoiApiGiaoDichAsync(
        SortedDictionary<string, string> thamSo,
        CancellationToken cancellationToken)
    {
        var chuoiKy = VnPayRequestBuilder.DungChuoiKy(thamSo);
        var chuKy = VnPayRequestBuilder.Ky(_options.HashSecret, chuoiKy);

        // VNPay nhận **JSON** cho API giao dịch, và chữ ký là **một trường trong JSON** (khác IPN dạng query string).
        var than = new Dictionary<string, string>(thamSo, StringComparer.Ordinal)
        {
            ["vnp_SecureHash"] = chuKy
        };

        try
        {
            using var noiDung = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(than), Encoding.UTF8, "application/json");

            using var phanHoi = await _http!.PostAsync(_options.TransactionApiUrl, noiDung, cancellationToken);
            var thanPhanHoi = await phanHoi.Content.ReadAsStringAsync(cancellationToken);

            // 🔒 Đọc phản hồi **kể cả** khi HTTP không phải 2xx: VNPay trả JSON mô tả lỗi kèm mã,
            // và mã đó hữu ích hơn hẳn "HTTP 400". ⚠️ `DocPhanHoi` **tự kiểm chữ ký** — phản hồi không
            // hợp lệ **không** được tin dù nội dung nói "thành công".
            var kq = VnPayRequestBuilder.DocPhanHoi(thanPhanHoi, _options.HashSecret);

            if (!phanHoi.IsSuccessStatusCode && kq.LyDoThatBai is null)
            {
                kq.LyDoThatBai = $"Cổng trả HTTP {(int)phanHoi.StatusCode}.";
            }

            return kq;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // ⏱️ TIMEOUT ⇒ **KHÔNG BIẾT** cổng có nhận được yêu cầu hay không.
            return new RefundResult
            {
                LoiHaTang = "Timeout",
                // 📌 Ghi **loại** exception vào lý do (không chỉ nói "timeout"): `TaskCanceledException` có thể
                // đến từ `HttpClient.Timeout` **hoặc** từ một tầng khác — phân biệt được thì mới sửa đúng chỗ.
                LyDoThatBai = $"Hết thời gian chờ cổng ({ex.InnerException?.GetType().Name ?? ex.GetType().Name}). " +
                              "KHÔNG BIẾT cổng đã nhận yêu cầu hay chưa " +
                              "⇒ CẤM retry mù (có thể hoàn HAI LẦN); phải tra `querydr` trước.",
                ChoXacNhan = true
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new RefundResult { LoiHaTang = "Cancelled", LyDoThatBai = "Yêu cầu bị huỷ theo `CancellationToken`." };
        }
        catch (HttpRequestException ex)
        {
            return new RefundResult
            {
                LoiHaTang = "HttpRequestException",
                LyDoThatBai = $"Không kết nối được cổng ({ex.GetType().Name}). KHÔNG BIẾT cổng đã nhận yêu cầu " +
                              "hay chưa ⇒ CẤM retry mù; phải tra `querydr` trước.",
                ChoXacNhan = true
            };
        }
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
