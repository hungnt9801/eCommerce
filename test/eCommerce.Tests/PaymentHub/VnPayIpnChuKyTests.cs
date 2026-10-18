using System.Net;
using System.Security.Cryptography;
using System.Text;
using eCommerce.PaymentHub.Payments;
using eCommerce.PaymentHub.Providers;
using Microsoft.Extensions.Options;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **CHỮ KÝ IPN — BA LUẬT CỦA CHUỖI KÝ** (`S5` — đóng `F-PAY-1`, `PAY-001` **TC-07**).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO TỆP NÀY TỒN TẠI (đây không phải test "cho đủ"):** `F-PAY-1` là phát hiện **mức 🔴 Cao** đã ghi từ
/// 17/10/2026 mà **suốt `S4` không có một ca test nào** chạm tới. Nội dung: `VerifyWebhookAsync` **giữ** tham số
/// rỗng khi dựng chuỗi ký, còn `KiemChuKy` (cùng ý niệm, khác tệp) **bỏ** chúng. Hai luật, một tệp ⇒ phản hồi
/// hợp lệ bị coi là giả ⇒ **từ chối IPN THẬT** ⇒ *"tiền đã vào mà sổ không biết"* — và **không có lỗi nào nổi
/// lên**; lưới an toàn duy nhất là job `querydr` (15 phút/lượt).
///
/// 🔑 **VÌ SAO PHẢI ĐO CẢ HAI CHIỀU:** nếu chỉ kiểm *"IPN có tham số rỗng ⇒ hợp lệ"* thì một bản sửa **quá tay**
/// (bỏ luôn việc kiểm chữ ký) cũng **xanh**. ⇒ Tệp này kiểm **cả** chiều ngược: chữ ký tính theo cách **cũ**
/// (giữ tham số rỗng) **PHẢI bị từ chối**, và hash **PHẢI** thực sự khác nhau — tức là chứng minh **phép tiêm đã
/// xảy ra**, không chỉ "cổng không bắt".
///
/// ➕ **Và luật thứ ba, chỉ phát hiện khi làm `S5`:** chuỗi ký phải dựng từ **query THÔ**, không được **giải mã
/// rồi encode lại**. Lý do cụ thể: `WebUtility.UrlEncode(' ')` = `%20`, còn `application/x-www-form-urlencoded`
/// (nhiều thư viện HTTP dùng) gửi `+` ⇒ **cùng nội dung, khác byte** ⇒ hash lệch ⇒ lại từ chối IPN thật.
/// 📌 Tệp này khoá luật đó bằng một ca có `+` trong `vnp_OrderInfo`.
/// </summary>
public class VnPayIpnChuKyTests
{
    private const string HashSecret = "bi_mat_test_khong_dung_that";

    private static VnPayPaymentProvider Provider(string? biMat = HashSecret)
        => new(Options.Create(new PaymentHubOptions
        {
            VnPay = new VnPayOptions { TmnCode = "TESTTMN", HashSecret = biMat ?? string.Empty }
        }));

    private static string Hmac(string biMat, string chuoi)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(biMat));

        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(chuoi)));
    }

    /// <summary>
    /// Dựng query IPN **nguyên văn** rồi ký trên **chính** chuỗi đó — đúng cách một cổng thật làm.
    ///
    /// 🔑 **Ba tham số tách rời có chủ ý**, và đây là điều làm các ca dưới **đo được** điều chúng nói:
    /// <list type="bullet">
    ///   <item>`hienThi` — tham số **XUẤT HIỆN** trong query (gồm cả cái rỗng);</item>
    ///   <item>`deKy` — tham số **ĐƯỢC KÝ** (bỏ cái rỗng);</item>
    ///   <item>hai tập khác nhau ⇒ ta **mô phỏng** được cả cách cổng ký (bỏ rỗng) **và** cách bản CŨ của ta ký
    ///     (giữ rỗng) — 🔑 không có hai tập này thì không có ca "chiều ngược".</item>
    /// </list>
    /// 📌 **Sắp xếp `Ordinal` theo TÊN khoá** ngay trong test — không dựa vào thứ tự khai báo của dữ liệu mẫu:
    /// nếu dựa vào nó thì test chỉ kiểm *"hàm khớp với thứ tự tôi viết"*, còn luật `Ordinal` (quy tắc 1 của VNPay)
    /// **không** được kiểm.
    /// </summary>
    private static string KyQuery(
        IEnumerable<KeyValuePair<string, string>> hienThi,
        IEnumerable<KeyValuePair<string, string>> deKy,
        string biMat)
    {
        static string Chuoi(IEnumerable<KeyValuePair<string, string>> ds) => string.Join("&", ds
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

        return Chuoi(hienThi) + "&vnp_SecureHash=" + Hmac(biMat, Chuoi(deKy));
    }

    /// <summary>Ký **toàn bộ** tham số đang xuất hiện (không bỏ cái rỗng) — mô phỏng cách bản CŨ của ta ký.</summary>
    private static string KyQuery(IEnumerable<KeyValuePair<string, string>> thamSo, string biMat)
        => KyQuery(thamSo, thamSo, biMat);

    private static List<KeyValuePair<string, string>> ThamSoIpn(
        string txnRef,
        decimal amount,
        string responseCode = "00",
        string transactionStatus = "00",
        string? bankCode = "NCB")
    {
        var ds = new List<KeyValuePair<string, string>>
        {
            new("vnp_Amount", (amount * 100m).ToString("0")),
            new("vnp_CurrCode", "VND"),
            new("vnp_ResponseCode", responseCode),
            new("vnp_TmnCode", "TESTTMN"),
            new("vnp_TransactionNo", "14000001"),
            new("vnp_TransactionStatus", transactionStatus),
            new("vnp_TxnRef", txnRef)
        };

        // 📌 `vnp_BankCode` là ca **RỖNG** trong đề bài `F-PAY-1` (VNPay gửi `vnp_BankCode=` cho một số
        // phương thức).
        if (bankCode is not null)
        {
            ds.Add(new KeyValuePair<string, string>("vnp_BankCode", bankCode));
        }

        return ds;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ① `F-PAY-1` — THAM SỐ RỖNG: IPN THẬT PHẢI ĐƯỢC CHẤP NHẬN
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TC07_IPN_co_tham_so_RONG__chu_ky_PHAI_hop_le()
    {
        // 🚨 Ca CHÍNH của `F-PAY-1`. Cổng ký **không** kèm tham số rỗng ⇒ chuỗi ký mà cổng dùng KHÔNG có
        // `vnp_BankCode`. Ta phải bỏ nó y như vậy; bản trước **giữ** ⇒ hash khác ⇒ từ chối IPN THẬT.
        var provider = Provider();

        var coRong = ThamSoIpn("PAY-20261018-0001-R01", 250000m, bankCode: string.Empty);

        // 🔑 Ký trên tập **đã bỏ** tham số rỗng — đúng như cổng làm; tham số rỗng **vẫn xuất hiện** trong query.
        var body = KyQuery(coRong, coRong.Where(kv => kv.Value.Length > 0), HashSecret);

        Assert.Contains("vnp_BankCode=", body);   // 🔑 điều kiện tiên quyết: tham số rỗng CÓ trong query

        var ketQua = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(ketQua.SignatureValid,
            "🚨 `F-PAY-1`: IPN có tham số RỖNG bị coi là chữ ký sai ⇒ đây là IPN THẬT bị từ chối " +
            "(mất tiền im lặng). Chuỗi ký phải BỎ tham số rỗng như `KiemChuKy`.");
        Assert.True(ketQua.IsSuccess);
        Assert.Equal("PAY-20261018-0001-R01", ketQua.MerchantReference);
        Assert.Equal(250000m, ketQua.Amount);
    }

    [Fact]
    public async Task TC07b_chu_ky_tinh_theo_cach_CU__giu_tham_so_rong__PHAI_bi_tu_choi()
    {
        // 🔒 **CHIỀU NGƯỢC — chống bản sửa "quá tay".** Nếu ai đó làm cho `VerifyWebhookAsync` chấp nhận
        // **mọi** chữ ký (ví dụ bỏ bước kiểm để "cho IPN vào"), thì ca `TC07` ở trên vẫn XANH. Ca này ĐỎ.
        var provider = Provider();

        var coRong = ThamSoIpn("PAY-20261018-0002-R01", 250000m, bankCode: string.Empty);

        // Ký **GIỮ** tham số rỗng — đúng cách bản CŨ của ta tính (⇒ cổng KHÔNG ký như vậy).
        var body = KyQuery(coRong, HashSecret);

        var ketQua = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.False(ketQua.SignatureValid,
            "Chữ ký tính theo cách CŨ (giữ tham số rỗng) KHÔNG được chấp nhận — nếu được, ta đang chấp nhận " +
            "một chuỗi ký mà cổng không bao giờ gửi.");
    }

    [Fact]
    public async Task TC07c_BANG_CHUNG_hai_cach_ky_cho_ra_hash_KHAC_nhau()
    {
        // 🔑 **Khẳng định phép tiêm ĐÃ XẢY RA** (bài học `README-probe` §25.5, áp cho tầng C#): nếu hai cách
        // ký cho cùng một hash thì hai ca trên **không đo được gì** — *"cổng không bắt"* và *"tiêm không thành"*
        // trông giống hệt nhau.
        var coRong = ThamSoIpn("PAY-20261018-0003-R01", 250000m, bankCode: string.Empty);

        var chuoiBo = string.Join("&", coRong.Where(kv => kv.Value.Length > 0)
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

        var chuoiGiu = string.Join("&", coRong
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

        // 🔑 Ba khẳng định: hai chuỗi **KHÁC** nhau (có gì đó để đo), và hai hash **KHÁC** nhau.
        Assert.NotEqual(chuoiBo, chuoiGiu);
        Assert.EndsWith("vnp_BankCode=", chuoiGiu);
        Assert.NotEqual(Hmac(HashSecret, chuoiBo), Hmac(HashSecret, chuoiGiu));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ② LUẬT "QUERY THÔ" — KHÔNG giải mã rồi encode lại
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task IPN_co_dau_CONG_thay_cho_khoang_trang__van_hop_le()
    {
        // 🚨 **Ca này sinh ra ngay trong `S5`:** một cổng/thư viện gửi khoảng trắng dưới dạng `+`
        // (`application/x-www-form-urlencoded`). Nếu ta **giải mã** rồi encode lại bằng `WebUtility.UrlEncode`
        // thì `+` ⇒ `%20` ⇒ **hash lệch** ⇒ từ chối IPN thật. 🔒 Chuỗi ký phải là **byte của cổng**.
        var provider = Provider();

        // Query **nguyên văn**: `vnp_OrderInfo=Thanh+toan+don+hang`.
        var chuoiKy = "vnp_Amount=25000000&vnp_CurrCode=VND&vnp_OrderInfo=Thanh+toan+don+hang" +
                      "&vnp_ResponseCode=00&vnp_TmnCode=TESTTMN&vnp_TransactionNo=14000001" +
                      "&vnp_TransactionStatus=00&vnp_TxnRef=PAY-20261018-0004-R01";

        var body = chuoiKy + "&vnp_SecureHash=" + Hmac(HashSecret, chuoiKy);

        var ketQua = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(ketQua.SignatureValid,
            "Chuỗi ký PHẢI dựng từ query THÔ: `+` không được đổi thành `%20` (đổi byte ⇒ chữ ký lệch).");
    }

    [Fact]
    public Task Query_THO_co_phan_tu_khong_phai_key_value__PHAI_bi_tu_choi()
    {
        // 🚨 Không được **đoán**: một query méo nghĩa là ta không có cơ sở nào để nói "chữ ký đúng".
        // 📌 Bỏ qua phần tử méo là **âm thầm** kiểm một chuỗi khác với chuỗi cổng gửi.
        var hopLe = VnPayRequestBuilder.KiemChuKyTuQueryTho(
            "vnp_TxnRef=X&vnp_Amount=1&honghe", HashSecret, out var lyDo);

        Assert.False(hopLe);
        Assert.Equal(VnPayRequestBuilder.LyDoTuChoiChuKy.QueryKhongHopLe, lyDo);

        return Task.CompletedTask;
    }

    [Fact]
    public Task Query_THO_co_khoa_TRUNG__PHAI_bi_tu_choi()
    {
        // ⚠️ "Lấy khoá đầu" hay "lấy khoá cuối" là **không tất định** giữa các tầng parse ⇒ từ chối thay vì
        // để tầng này đọc khoá đầu còn tầng kia đọc khoá cuối.
        var hopLe = VnPayRequestBuilder.KiemChuKyTuQueryTho(
            "vnp_TxnRef=X&vnp_TxnRef=Y&vnp_SecureHash=abc", HashSecret, out var lyDo);

        Assert.False(hopLe);
        Assert.Equal(VnPayRequestBuilder.LyDoTuChoiChuKy.QueryKhongHopLe, lyDo);

        return Task.CompletedTask;
    }

    [Fact]
    public Task Query_THO_thieu_vnp_SecureHash__ly_do_la_THIEU_CHU_KY()
    {
        var hopLe = VnPayRequestBuilder.KiemChuKyTuQueryTho(
            "vnp_TxnRef=X&vnp_Amount=100", HashSecret, out var lyDo);

        Assert.False(hopLe);
        Assert.Equal(VnPayRequestBuilder.LyDoTuChoiChuKy.ThieuChuKy, lyDo);

        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // ③ FAIL-CLOSED KHI CHƯA CẤU HÌNH KHOÁ KÝ (lỗ hổng mở ra khi endpoint IPN lên sóng)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HashSecret_RONG__PHAI_TU_CHOI_va_PHAT_metric__khong_bao_gio_cho_qua()
    {
        // 🚨 **LỖ HỔNG THẬT NẾU KHÔNG CÓ NHÁNH NÀY:** `HMACSHA512` với khoá **rỗng** vẫn tính ra hash ⇒ kẻ tấn
        // công ở ngoài **tự tính được** cùng hash đó ⇒ **ký được IPN giả** `vnp_ResponseCode=00` ⇒
        // `MarkSucceeded` ⇒ **đơn đã trả tiền** ⇒ **giao hàng miễn phí** (`PAY-001` §7.7.4 #1).
        // 📌 Trạng thái repo hôm nay (`appsettings.json` để `HashSecret: ""`) chính là trạng thái này ⇒ ca này
        // khoá lại điều kiện an toàn **tối thiểu** của endpoint mới.
        var meter = new System.Diagnostics.Metrics.Meter("eCommerce-Test-" + Guid.NewGuid());
        using var _ = meter;

        var metrics = new eCommerce.Kernel.Observability.eCommerceMetrics(meter);
        var provider = new VnPayPaymentProvider(
            Options.Create(new PaymentHubOptions { VnPay = new VnPayOptions { TmnCode = "TESTTMN", HashSecret = string.Empty } }),
            metrics);

        // 🔑 Dùng **cùng khuôn** `BoGhi` của `VnPayWebhookTests`: `SetMeasurementEventCallback` phải được đặt
        // **trước** `Start()`, nếu không thì metric không bao giờ tới được listener ⇒ ca "có phát metric" sẽ
        // **xanh/đỏ vì lý do không phải điều mình kiểm**.
        var ghi = new List<(string Ten, long GiaTri, Dictionary<string, object?> Nhan)>();
        using var listener = new System.Diagnostics.Metrics.MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var nhan = new Dictionary<string, object?>();
            foreach (var t in tags)
            {
                nhan[t.Key] = t.Value;
            }

            ghi.Add((instrument.Name, value, nhan));
        });

        listener.Start();

        // 🔑 Ký bằng **khoá rỗng** — đúng điều kẻ tấn công làm được khi ta chưa cấu hình.
        var thamSo = ThamSoIpn("PAY-20261018-0005-R01", 250000m, bankCode: null);
        var body = KyQuery(thamSo, biMat: string.Empty);

        var ketQua = await provider.VerifyWebhookAsync(body, new Dictionary<string, string>());

        Assert.False(ketQua.SignatureValid,
            "🚨 `HashSecret` RỖNG mà vẫn chấp nhận chữ ký ⇒ bất kỳ ai cũng ký được IPN giả (giao hàng miễn phí).");
        Assert.False(ketQua.IsSuccess);
        Assert.Contains("97", ketQua.AckResponseBody);   // vẫn trả ACK đúng hình dạng VNPay

        var tuChoi = ghi.Where(x => x.Ten == eCommerce.Kernel.Observability.eCommerceMetricNames.PaymentWebhookRejectedTotal).ToList();
        Assert.Single(tuChoi);
        Assert.Equal("thieu-hashsecret", tuChoi[0].Nhan[eCommerce.Kernel.Observability.eCommerceMetricTags.Reason]);
    }
}
