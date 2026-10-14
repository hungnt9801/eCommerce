using System.Net;
using System.Security.Cryptography;
using System.Text;
using eCommerce.PaymentHub.Payments;
using Microsoft.Extensions.Options;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// Test cho xác thực IPN của VNPay — đây là hàng rào chống gian lận quan trọng nhất
/// trong toàn bộ luồng thanh toán.
///
/// Test tự tính chữ ký bằng HMAC-SHA512 (viết độc lập với provider) rồi kiểm tra provider
/// chấp nhận/từ chối đúng. Không cần mạng, không cần DB.
/// </summary>
public class VnPayWebhookTests
{
    private const string HashSecret = "test_hash_secret_khong_dung_cho_production";

    private static VnPayPaymentProvider CreateProvider(
        eCommerce.Kernel.Observability.eCommerceMetrics? metrics = null) => new(
        Options.Create(new PaymentHubOptions
        {
            VnPay = new VnPayOptions
            {
                TmnCode = "TESTTMN",
                HashSecret = HashSecret
            }
        }),
        metrics);

    private static string HmacSha512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    /// <summary>Tạo query string IPN giống VNPay: tham số sắp xếp tăng dần theo tên + vnp_SecureHash.</summary>
    private static string BuildIpnQuery(
        string txnRef,
        decimal amount,
        string responseCode,
        string transactionStatus,
        string? overrideAmount = null)
    {
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = overrideAmount ?? (amount * 100m).ToString("0"),
            ["vnp_BankCode"] = "NCB",
            ["vnp_CurrCode"] = "VND",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = "TESTTMN",
            ["vnp_TransactionNo"] = "14000001",
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TxnRef"] = txnRef
        };

        var toSign = string.Join("&", parameters.Select(kv =>
            $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

        var hash = HmacSha512(HashSecret, toSign);

        return $"{toSign}&vnp_SecureHash={hash}";
    }

    [Fact]
    public async Task Ipn_hop_le_va_thanh_cong_thi_duoc_chap_nhan()
    {
        var provider = CreateProvider();
        var body = BuildIpnQuery("ORDER-2026-001", 250000m, "00", "00");

        var result = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(result.SignatureValid);
        Assert.True(result.IsSuccess);
        Assert.Equal("ORDER-2026-001", result.MerchantReference);
        Assert.Equal(250000m, result.Amount);       // vnp_Amount là VND × 100 ⇒ phải chia lại
        Assert.Equal("14000001", result.ProviderTransactionId);
        Assert.Contains("\"RspCode\":\"00\"", result.AckResponseBody);
    }

    [Fact]
    public async Task Ipn_bi_sua_so_tien_thi_chu_ky_khong_hop_le()
    {
        var provider = CreateProvider();

        // Kẻ tấn công đổi số tiền thành 1.000.000 nhưng giữ nguyên chữ ký cũ.
        var original = BuildIpnQuery("ORDER-2026-002", 250000m, "00", "00");
        var tampered = original.Replace("vnp_Amount=25000000", "vnp_Amount=100000000");

        var result = await provider.VerifyWebhookAsync(tampered, new Dictionary<string, string>());

        Assert.False(result.SignatureValid);
        Assert.False(result.IsSuccess);
        Assert.Contains("\"RspCode\":\"97\"", result.AckResponseBody); // báo sai checksum cho VNPay
    }

    [Fact]
    public async Task Ipn_chu_ky_dung_nhung_giao_dich_that_bai_thi_khong_duoc_coi_la_thanh_cong()
    {
        var provider = CreateProvider();

        // Chữ ký hợp lệ (do VNPay ký thật) nhưng giao dịch thất bại.
        var body = BuildIpnQuery("ORDER-2026-003", 100000m, "24", "02");

        var result = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(result.SignatureValid);
        Assert.False(result.IsSuccess); // ⚠️ Đây là lỗi hay gặp: chỉ kiểm tra chữ ký là đủ
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task Ipn_thieu_chu_ky_thi_bi_tu_choi()
    {
        var provider = CreateProvider();

        var result = await provider.VerifyWebhookAsync("vnp_TxnRef=X&vnp_Amount=100", new Dictionary<string, string>());

        Assert.False(result.SignatureValid);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Checkout_url_duoc_ky_va_co_amount_nhan_100()
    {
        var provider = CreateProvider();

        var result = await provider.CreatePaymentAsync(new CreatePaymentRequest
        {
            MerchantReference = "ORDER-2026-004",
            Amount = 123456.78m,
            OrderInfo = "Thanh toan don hang",
            ReturnUrl = "https://shop.example.com/payment/return",
            IpnUrl = "https://shop.example.com/api/payment/vnpay/ipn",
            ClientIp = "1.2.3.4"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.CheckoutUrl);

        // 123456.78 × 100 = 12345678
        Assert.Contains("vnp_Amount=12345678", result.CheckoutUrl);
        Assert.Contains("vnp_SecureHash=", result.CheckoutUrl);
        Assert.Contains("vnp_TxnRef=ORDER-2026-004", result.CheckoutUrl);
    }

    // ══════════════════════ 📊 METRIC "WEBHOOK BỊ TỪ CHỐI" ══════════════════════

    private sealed class BoGhi : IDisposable
    {
        private readonly System.Diagnostics.Metrics.MeterListener _listener = new();

        public List<(string Ten, long GiaTri, Dictionary<string, object?> Nhan)> Ghi { get; } = [];

        public BoGhi(System.Diagnostics.Metrics.Meter meter)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var nhan = new Dictionary<string, object?>();
                foreach (var t in tags)
                {
                    nhan[t.Key] = t.Value;
                }

                Ghi.Add((instrument.Name, value, nhan));
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private static (VnPayPaymentProvider Provider, BoGhi Ghi) ProviderCoMetric()
    {
        var meter = new System.Diagnostics.Metrics.Meter("eCommerce-Test-" + Guid.NewGuid());
        var metrics = new eCommerce.Kernel.Observability.eCommerceMetrics(meter);

        return (CreateProvider(metrics), new BoGhi(meter));
    }

    private static long DemTuChoi(BoGhi ghi)
        => ghi.Ghi
            .Where(x => x.Ten == eCommerce.Kernel.Observability.eCommerceMetricNames.PaymentWebhookRejectedTotal)
            .Sum(x => x.GiaTri);

    [Fact]
    public async Task IPN_thieu_chu_ky_thi_PHAT_metric_tu_choi()
    {
        var (provider, ghi) = ProviderCoMetric();
        using var _ = ghi;

        await provider.VerifyWebhookAsync("vnp_TxnRef=X&vnp_Amount=100", new Dictionary<string, string>());

        Assert.Equal(1, DemTuChoi(ghi));
        var nhan = Assert.Single(ghi.Ghi,
            x => x.Ten == eCommerce.Kernel.Observability.eCommerceMetricNames.PaymentWebhookRejectedTotal).Nhan;
        Assert.Equal("vnpay", nhan[eCommerce.Kernel.Observability.eCommerceMetricTags.Provider]);
        Assert.Equal("thieu-chu-ky", nhan[eCommerce.Kernel.Observability.eCommerceMetricTags.Reason]);
    }

    [Fact]
    public async Task IPN_chu_ky_SAI_thi_PHAT_metric_tu_choi()
    {
        var (provider, ghi) = ProviderCoMetric();
        using var _ = ghi;

        var body = BuildIpnQuery("ORDER-2026-009", 100000m, "00", "00");
        var tampered = body.Replace("vnp_Amount=10000000", "vnp_Amount=99000000");

        await provider.VerifyWebhookAsync(tampered, new Dictionary<string, string>());

        var nhan = Assert.Single(ghi.Ghi,
            x => x.Ten == eCommerce.Kernel.Observability.eCommerceMetricNames.PaymentWebhookRejectedTotal).Nhan;
        Assert.Equal("chu-ky-sai", nhan[eCommerce.Kernel.Observability.eCommerceMetricTags.Reason]);
    }

    [Fact]
    public async Task IPN_HOP_LE_thi_KHONG_phat_metric_tu_choi()
    {
        // Chống BÁO ĐỘNG GIẢ: webhook hợp lệ thì tuyệt đối không được tính là "bị từ chối".
        var (provider, ghi) = ProviderCoMetric();
        using var _ = ghi;

        var body = BuildIpnQuery("ORDER-2026-010", 250000m, "00", "00");

        var result = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(result.SignatureValid);
        Assert.Equal(0, DemTuChoi(ghi));
    }

    [Fact]
    public async Task IPN_chu_ky_DUNG_nhung_giao_dich_THAT_BAI_thi_KHONG_tinh_la_TU_CHOI()
    {
        // 🚨 Đây là ca dễ tính nhầm nhất: chữ ký ĐÚNG nhưng `vnp_ResponseCode != 00`
        // (khách huỷ / giao dịch thất bại). Đây là webhook **HỢP LỆ** — KHÔNG phải bị từ chối.
        // Nếu tính nhầm thì metric sẽ báo động GIẢ mỗi lần khách huỷ thanh toán.
        var (provider, ghi) = ProviderCoMetric();
        using var _ = ghi;

        var body = BuildIpnQuery("ORDER-2026-011", 250000m, "24", "02");   // 24 = khách huỷ

        var result = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(result.SignatureValid);      // chữ ký hợp lệ
        Assert.False(result.IsSuccess);          // nhưng giao dịch thất bại
        Assert.Equal(0, DemTuChoi(ghi));         // ⬅️ KHÔNG phải webhook bị từ chối
    }
}
