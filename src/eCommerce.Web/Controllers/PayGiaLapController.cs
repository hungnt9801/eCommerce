using System.Globalization;
using System.Net;
using System.Text;
using eCommerce.PaymentHub.Payments;
using eCommerce.PaymentHub.Providers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **TRANG GIẢ LẬP CỔNG THANH TOÁN** (`S8` · `PAY-001` §7.7.2) — **CHỈ Ở DEVELOPMENT**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO CẦN:** chưa có credential VNPay sandbox ⇒ **không thể** đi qua `vpcpay.html` thật, và IPN thật
/// còn đòi site **mở ra Internet**. Nhưng *"chưa có credential"* **không** được phép biến thành
/// *"không kiểm được đường tiền"*: đây là trang thay **nhà cung cấp** (lớp ① của §7.7), **không** thay
/// **chữ ký** (lớp ③).
///
/// 🔒 **CHỮ KÝ LÀ THẬT — ĐÂY LÀ ĐIỀU QUAN TRỌNG NHẤT CỦA TỆP NÀY:**
/// <list type="number">
///   <item>Query `vnp_*` được ký bằng **chính** `VnPayRequestBuilder.DungChuoiKy` + `Ky` — **không** viết lại
///     thuật toán. 📌 Viết lại nghĩa là ta kiểm **bản sao**, không kiểm **code thật** (tiền lệ `F-PAY-1`).</item>
///   <item>Sau khi ký, trang này gọi **ENDPOINT IPN THẬT** (`/api/webhooks/vnpay` ở tiến trình Host) bằng
///     **HTTP thật** — không gọi hàm trong tiến trình ⇒ đi qua controller + interceptor + kiểm chữ ký + ghi DB
///     đúng như cổng thật. 📌 Đó là điểm khác biệt giữa *"kiểm được"* và *"trông như đã kiểm"*.</item>
///   <item>Kết quả IPN trả về **được hiển thị** (kể cả khi khác `RspCode 00`): nếu bước gọi IPN hỏng mà trang
///     vẫn hiện *"đã thanh toán"* thì ta vừa tự lừa mình.</item>
/// </list>
///
/// 🚨 **CHỐT AN TOÀN:** mọi hành động **`404`** khi không phải `Development`. 📌 Đăng ký `Fake` provider đã
/// có fail-fast riêng (`PaymentHubApplicationModule`); lớp này là **lớp thứ hai** — cố ý trùng, vì hậu quả
/// của việc lọt ra production là *"đơn được đánh dấu đã trả tiền mà không có đồng nào vào sổ"*.
///
/// ⚠️ **KHÔNG nằm trong allowlist công khai:** đây là công cụ nội bộ của môi trường dev, `[Authorize]` cấp
/// class. 🔑 Trang **không** là *"cổng thanh toán"* của khách — khách thật đi qua `vpcpay.html` của VNPay.
/// </summary>
[Authorize]
[Route("pay-gia-lap")]
public class PayGiaLapController : Controller
{
    /// <summary>Khoá bí mật **dev** dùng để ký — 📌 chính khoá mà `VnPayPaymentProvider` dùng để **kiểm**.
    /// Dùng cùng khoá là **điều kiện kỹ thuật**: ký khác khoá thì mọi IPN bị `400` — và đó không phải lỗi
    /// cần "nới lỏng kiểm tra", mà là ta ký sai.</summary>
    private readonly PaymentHubOptions _cauHinh;

    private readonly IRepository<PaymentTransaction, Guid> _giaoDich;
    private readonly IHttpClientFactory _http;
    private readonly IWebHostEnvironment _moiTruong;
    private readonly IAntiforgery _chongGiaMao;
    private readonly ILogger<PayGiaLapController> _logger;

    public PayGiaLapController(
        IOptions<PaymentHubOptions> cauHinh,
        IRepository<PaymentTransaction, Guid> giaoDich,
        IHttpClientFactory http,
        IWebHostEnvironment moiTruong,
        IAntiforgery chongGiaMao,
        ILogger<PayGiaLapController> logger)
    {
        _cauHinh = cauHinh.Value;
        _giaoDich = giaoDich;
        _http = http;
        _moiTruong = moiTruong;
        _chongGiaMao = chongGiaMao;
        _logger = logger;
    }

    /// <summary>`GET /pay-gia-lap/{maThamChieu}?returnUrl=…` — mô phỏng **trang thanh toán của cổng**
    /// (3 nút phải không: Thành công / Thất bại / Huỷ → `POST` cùng route).</summary>
    [HttpGet("{maThamChieu}")]
    public async Task<IActionResult> Index(string maThamChieu, string? returnUrl)
    {
        if (!_moiTruong.IsDevelopment())
        {
            return NotFound();
        }

        var giaoDich = await TimGiaoDichAsync(maThamChieu);

        if (giaoDich is null)
        {
            return NotFound();
        }

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">");
        html.Append("<title>Giả lập cổng thanh toán</title></head><body>");
        html.Append("<h1 id=\"tieu-de\">Giả lập cổng thanh toán</h1>");
        html.Append("<p id=\"canh-bao\">[PAY-GIA-LAP] Đây là <strong>cổng GIẢ LẬP</strong> (chỉ Development). ");
        html.Append("Chữ ký HMAC-SHA512 là <strong>THẬT</strong> và IPN sẽ được gửi tới <strong>endpoint thật</strong>.</p>");
        html.Append("<table id=\"thong-tin\">");
        html.Append("<tr><th>Mã tham chiếu</th><td id=\"ma-tham-chieu\">").Append(WebUtility.HtmlEncode(giaoDich.MerchantReference)).Append("</td></tr>");
        html.Append("<tr><th>Số tiền</th><td id=\"so-tien\">").Append(giaoDich.Amount.ToString("N0", CultureInfo.InvariantCulture)).Append(" ").Append(WebUtility.HtmlEncode(giaoDich.Currency)).Append("</td></tr>");
        html.Append("<tr><th>Trạng thái trong sổ</th><td id=\"trang-thai\">").Append(giaoDich.Status.ToString()).Append("</td></tr>");
        html.Append("</table>");

        // 🔐 Token phát từ **chính máy chủ** cho **chính form này** — E2E đọc giá trị từ HTML (không đoán),
        // vì một token giả/thiếu ⇒ `400` và ca kiểm sẽ đỏ vì **phép đo**, không phải vì sản phẩm.
        var tokenChongGiaMao = _chongGiaMao.GetAndStoreTokens(HttpContext);

        foreach (var (ma, nhan, css) in new[]
                 {
                     ("thanh-cong", "Thành công (thu tiền)", "ok"),
                     ("that-bai", "Thất bại (không đủ số dư)", "loi"),
                     ("huy", "Khách huỷ", "huy")
                 })
        {
            html.Append($"<form method=\"post\" action=\"/pay-gia-lap/{WebUtility.UrlEncode(giaoDich.MerchantReference)}\" class=\"form-ket-qua\">");
            html.Append($"<input name=\"{tokenChongGiaMao.FormFieldName}\" type=\"hidden\" value=\"{WebUtility.HtmlEncode(tokenChongGiaMao.RequestToken)}\" />");
            html.Append($"<input type=\"hidden\" name=\"returnUrl\" value=\"{WebUtility.HtmlEncode(returnUrl ?? string.Empty)}\" />");
            html.Append($"<input type=\"hidden\" name=\"ketQua\" value=\"{ma}\" />");
            html.Append($"<button type=\"submit\" id=\"nut-{ma}\" class=\"{css}\">{nhan}</button>");
            html.Append("</form>");
        }

        html.Append("</body></html>");

        return Content(html.ToString(), "text/html; charset=utf-8");
    }

    /// <summary>
    /// `POST /pay-gia-lap/{maThamChieu}` — **cổng báo kết quả**: dựng query `vnp_*`, **ký HMAC-SHA512**,
    /// gọi **IPN THẬT**, rồi chuyển khách về `returnUrl` kèm `?ma=…`.
    /// </summary>
    /// <remarks>
    /// 🚨 **KHÔNG BAO GIỜ tin `returnUrl` như bằng chứng thanh toán** (`PAY-001` D2): nó chỉ là **đường điều
    /// hướng**. Trạng thái thật do trang kết quả đọc **từ DB** — nơi chỉ IPN đã kiểm chữ ký mới ghi được.
    /// 🔒 `returnUrl` được **giới hạn miền**: chỉ nhận đường dẫn nội bộ hoặc URL **cùng host** (chống
    /// *open redirect* — một trang thanh toán chuyển khách sang domain lạ là mồi lừa đảo kinh điển).
    /// </remarks>
    [HttpPost("{maThamChieu}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TraKetQua(string maThamChieu, string? returnUrl, string? ketQua)
    {
        if (!_moiTruong.IsDevelopment())
        {
            return NotFound();
        }

        var giaoDich = await TimGiaoDichAsync(maThamChieu);

        if (giaoDich is null)
        {
            return NotFound();
        }

        var (responseCode, transactionStatus, moTa) = ketQua switch
        {
            "thanh-cong" => ("00", "00", "Khach dong y thanh toan"),
            "huy" => ("24", "02", "Khach huy giao dich"),
            _ => ("51", "02", "Tai khoan khong du so du")
        };

        // ── ① DỰNG + KÝ QUERY ĐÚNG NHƯ VNPAY ------------------------------------------------------
        // 📌 Chỉ đưa vào những tham số **có giá trị**: `KiemChuKyTuQueryTho` bỏ tham số rỗng (đúng luật
        // VNPay) ⇒ nếu ta gửi cặp rỗng mà vẫn tính nó vào chuỗi ký thì chữ ký sẽ lệch (`F-PAY-1`).
        var thamSo = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = decimal.Round(giaoDich.Amount * 100m, 0, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = string.IsNullOrWhiteSpace(giaoDich.Currency) ? "VND" : giaoDich.Currency,
            ["vnp_OrderInfo"] = $"Thanh toan don {giaoDich.MerchantReference}",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = _cauHinh.VnPay.TmnCode ?? string.Empty,
            // 🔑 Mã giao dịch phía "cổng": khác `vnp_TxnRef` (mã của TA) — đúng hình dạng VNPay.
            ["vnp_TransactionNo"] = "GL" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TxnRef"] = giaoDich.MerchantReference
        };

        var chuoiKy = VnPayRequestBuilder.DungChuoiKy(thamSo);
        var chuKy = VnPayRequestBuilder.Ky(_cauHinh.VnPay.HashSecret, chuoiKy);
        var query = chuoiKy + "&vnp_SecureHash=" + chuKy;

        // ── ② GỌI IPN THẬT (HTTP) ------------------------------------------------------------------
        var ipnGoc = (_cauHinh.Fake.IpnBaseUrl ?? string.Empty).TrimEnd('/');
        var ipnUrl = $"{ipnGoc}/api/webhooks/vnpay?{query}";

        string ipnThan;
        int ipnMa;
        Exception? ipnLoi = null;

        try
        {
            using var client = _http.CreateClient("PayGiaLap");
            client.Timeout = TimeSpan.FromSeconds(20);

            using var traLoi = await client.GetAsync(ipnUrl);
            ipnMa = (int)traLoi.StatusCode;
            ipnThan = await traLoi.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            // 🔒 KHÔNG nuốt: nếu không gọi được IPN thì **chưa có thanh toán nào được ghi** — phải nói rõ,
            // và **không** được chuyển khách về trang kết quả như thể đã xong.
            ipnLoi = ex;
            ipnMa = -1;
            ipnThan = ex.Message;
        }

        _logger.LogInformation(
            "CỔNG GIẢ LẬP: {Ma} · nút {KetQua} · IPN HTTP {IpnMa} · body {Body}. [PAY-GIA-LAP-DA-KY]",
            giaoDich.MerchantReference, moTa, ipnMa, ipnThan);

        if (ipnLoi is not null || ipnMa != 200)
        {
            // 🚨 Hiện **trang lỗi có nội dung** thay vì chuyển tiếp: nếu Host chưa chạy (hoặc chữ ký lệch),
            // người dùng PHẢI thấy *"IPN không ghi được"* — 📌 đó là bằng chứng phép đo đang chạy thật.
            return Content(
                "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>Cổng giả lập: IPN lỗi</title></head><body>"
                + "<h1>[PAY-GIA-LAP-IPN-LOI]</h1>"
                + $"<p id=\"ma-tham-chieu\">{WebUtility.HtmlEncode(giaoDich.MerchantReference)}</p>"
                + $"<p id=\"ipn-http\">HTTP {ipnMa}</p>"
                + $"<pre id=\"ipn-body\">{WebUtility.HtmlEncode(ipnThan)}</pre>"
                + "<p>⇒ IPN KHÔNG được ghi nhận ⇒ đơn CHƯA thanh toán (đọc trạng thái từ DB, không đoán).</p>"
                + "</body></html>",
                "text/html; charset=utf-8");
        }

        // ── ③ CHUYỂN KHÁCH VỀ TRANG KẾT QUẢ (kèm mã tham chiếu) ------------------------------------
        var quayLai = DuongDanQuayLai(returnUrl);
        var dauNoi = quayLai.Contains('?', StringComparison.Ordinal) ? '&' : '?';

        return Redirect(quayLai + dauNoi + "ma=" + Uri.EscapeDataString(giaoDich.MerchantReference));
    }

    /// <summary>Tra giao dịch theo **mã tham chiếu** (mã của TA) — `null` nếu không có.</summary>
    private async Task<PaymentTransaction?> TimGiaoDichAsync(string maThamChieu)    {
        if (string.IsNullOrWhiteSpace(maThamChieu))
        {
            return null;
        }

        var ma = maThamChieu.Trim();

        return await _giaoDich.FirstOrDefaultAsync(x => x.MerchantReference == ma);
    }

    /// <summary>
    /// 🔒 **Giới hạn `returnUrl`** (chống *open redirect*): chỉ nhận **đường dẫn nội bộ** hoặc **URL cùng host**.
    /// ⚠️ Trả về **chuỗi rỗng an toàn** (`/DatHang`) khi không hợp lệ — **không** ném lỗi, vì đây là đầu vào
    /// của một công cụ dev và cách đúng là bỏ qua nó, không phải làm sập trang.
    /// </summary>
    private string DuongDanQuayLai(string? returnUrl)
    {
        const string macDinh = "/DatHang";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return macDinh;
        }

        var giaTri = returnUrl.Trim();

        if (Url.IsLocalUrl(giaTri))
        {
            return giaTri;
        }

        if (!Uri.TryCreate(giaTri, UriKind.Absolute, out var uri))
        {
            return macDinh;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return macDinh;
        }

        // So **host:port** với chính request hiện tại ⇒ chỉ quay lại được trong cùng ứng dụng.
        if (!string.Equals(uri.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "CỔNG GIẢ LẬP: bỏ `returnUrl` KHÁC MIỀN ({Host}) ⇒ dùng {MacDinh} (chống open redirect). [PAY-GIA-LAP-RETURNURL-KHAC-MIEN]",
                uri.Authority, macDinh);

            return macDinh;
        }

        return uri.PathAndQuery;
    }
}
