using eCommerce.PaymentHub;
using eCommerce.PaymentHub.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace eCommerce.HttpApi.Host.Controllers;

/// <summary>
/// 🚨 **ENDPOINT NHẬN IPN CỦA VNPAY** (`S5`) — `GET /api/webhooks/vnpay`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **ĐÂY LÀ CỬA DUY NHẤT ĐỂ TIỀN ĐI VÀO SỔ.** Trước `S5`, `MarkSucceeded` **có** code nhưng **không** có
/// endpoint nào gọi tới từ cổng thật ⇒ câu *"khách đã trả chưa"* **chỉ** trả lời được bằng job `querydr`
/// (nợ §15.3 #2 của `PAY-001`, và §2.1 #5 của `gd1-coding-sessions`).
///
/// 🔓 **`[AllowAnonymous]` — CỐ Ý, và là điều kiện để endpoint tồn tại:** VNPay gọi từ **Internet**, không có
/// token của ta. 🔒 Nhưng *"công khai"* ở đây **không** phải *"mở"*: mọi request **buộc** qua **HMAC-SHA512**
/// (`VnPayRequestBuilder.KiemChuKyTuQueryTho`) **trước** khi chạm DB. 📌 Đúng `PAY-001` §7.7.4 #1: bỏ bước
/// chữ ký thì *"ai gửi `vnp_ResponseCode=00` cũng thành đơn đã trả tiền"* ⇒ **giao hàng miễn phí**.
/// ➕ Endpoint này **đã được duyệt** trong allowlist (`AllowlistCongKhaiTests`) — thêm `[AllowAnonymous]` mà
/// không khai là **test đỏ**, cố ý: mỗi dòng allowlist là một cửa ra Internet.
///
/// 🔒 **BỐN QUY TẮC CỦA CONTROLLER NÀY (mỗi cái đều là một lỗi đã trả giá ở đâu đó trong dự án):**
/// <list type="number">
///   <item>**Chỉ `GET`.** VNPay gửi IPN bằng `GET` với query string. 📌 Không nhận `POST`: thêm một verb là
///     thêm một bề mặt, mà `POST` của MVC còn kéo theo **chống giả mạo** (`400` nếu thiếu token) — một cái bẫy
///     đã cắn dự án (xem `tools/README-probe.md` §E).</item>
///   <item>**Dùng QUERY THÔ (`Request.QueryString`), KHÔNG dùng `Request.Query`.** 🔑 `Request.Query` đã **giải
///     mã**, và mã hoá lại sẽ đổi byte (`+` ↔ `%20`) ⇒ chữ ký lệch ⇒ **từ chối IPN thật**. Đây đúng họ lỗi
///     `F-PAY-1`, chỉ khác lớp — xem chú thích `KiemChuKyTuQueryTho`.</item>
///   <item>🚨 **Sai chữ ký ⇒ `HTTP 400` + body `{"RspCode":"97"}`** (quyết định của chủ dự án trong `S5`).
///     📌 Lý do chọn `400` dù đặc tả VNPay quen dùng `200` + `RspCode`: một phép kiểm **phải đo được bằng mã
///     HTTP** — cổng ra của `S5` ghi rõ *"sai chữ ký ⇒ 400"*, và body vẫn giữ đúng hình dạng VNPay để phía cổng
///     đọc được nếu họ đọc. ⚠️ Hệ quả đã biết: VNPay sẽ **retry** (vì không phải `2xx`) — với một chữ ký sai thì
///     retry **không** gây hại (payload vẫn bị từ chối) và **có lợi**: nếu nguyên nhân là `HashSecret` lệch cấu
///     hình thì log/metric của ta **có nhiều mẫu** để thấy. 📄 Ghi ở `PAY-001` §7.2.</item>
///   <item>🔒 **`catch BusinessException` ⇒ `400`, KHÔNG để thành `500`.** `AbpExceptionFilter` biến
///     `BusinessException` thành **`500`** trên MVC (bẫy #7 của dự án) ⇒ cổng sẽ retry **mãi** cho một lỗi
///     **dữ liệu** (ví dụ IPN lệch số tiền — retry không bao giờ sửa được điều đó). 📌 Controller **không nuốt**
///     guard: nó ghi log `Error` **kèm mã** rồi trả `RspCode 99` để phía cổng có mã tra cứu.</item>
/// </list>
/// </summary>
[AllowAnonymous]
[Route("api/webhooks/vnpay")]
public class VnPayIpnController : ControllerBase
{
    private const string ThanhCong = "{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}";
    private const string ChuKySai = "{\"RspCode\":\"97\",\"Message\":\"Invalid Checksum\"}";

    private readonly IPaymentProvider _cong;
    private readonly IXuLyKetQuaCong _xuLy;
    private readonly ILogger<VnPayIpnController> _logger;

    public VnPayIpnController(
        IPaymentProvider cong,
        IXuLyKetQuaCong xuLy,
        ILogger<VnPayIpnController> logger)
    {
        _cong = cong;
        _xuLy = xuLy;
        _logger = logger;
    }

    /// <summary>Nhận IPN: ① xác thực chữ ký (fail-closed) ② ghi nhận (idempotent) ③ trả ACK.</summary>
    [HttpGet]
    public async Task<IActionResult> Nhan(CancellationToken cancellationToken)
    {
        // ① **QUERY THÔ** — chính là chuỗi cổng đã ký. 📌 `Request.QueryString.Value` giữ nguyên byte gốc
        // (kể cả `?` ở đầu); `VerifyWebhookAsync` tự bỏ `?`.
        var queryTho = Request.QueryString.Value ?? string.Empty;

        var ketQua = await _cong.VerifyWebhookAsync(queryTho, ThuThapHeader(), cancellationToken);

        if (!ketQua.SignatureValid)
        {
            // 🚨 **KHÔNG** đổi trạng thái, **KHÔNG** ghi gì. Chỉ log + đếm (metric đã phát trong provider).
            // Neo ASCII để tìm được trong log (bài học `I4`: log tiếng Việt bị hỏng dấu khi đọc qua console).
            _logger.LogWarning(
                "IPN BỊ TỪ CHỐI (chữ ký không hợp lệ): mã tham chiếu={MerchantReference} · lý do={LyDo} · " +
                "độ dài query={DoDai}. ⇒ KHÔNG ghi nhận thanh toán. [PAY-IPN-TU-CHOI]",
                ketQua.MerchantReference, ketQua.FailureReason, queryTho.Length);

            return Json(400, ChuKySai);
        }

        try
        {
            var ghi = await _xuLy.GhiNhanAsync(ketQua, cancellationToken);

            _logger.LogInformation(
                "IPN ĐÃ GHI NHẬN: mã tham chiếu={MerchantReference} · trạng thái sau={TrangThai} · " +
                "đã ghi sổ={DaGhi} · đã phát sự kiện={DaPhat} · số lần nhận webhook={SoLan} · " +
                "cổng báo thành công={ThanhCong}. [PAY-IPN-DA-GHI]",
                ghi.MerchantReference, ghi.TrangThaiSau, ghi.DaGhiSo, ghi.DaPhatSuKien, ghi.SoLanNhanWebhook,
                ketQua.IsSuccess);

            return Json(200, ThanhCong);
        }
        catch (BusinessException ex)
        {
            // 🔒 Guard của tầng ghi nhận **không bị nuốt**: ghi log `Error` kèm mã, rồi trả `400` + `RspCode 99`
            // (VNPay: *"lỗi khác"*). 📌 `400` chứ không `500`: đây là **lỗi dữ liệu**, retry không sửa được, và
            // một `500` sẽ khiến cổng retry theo lịch của họ mà **không ai** biết vì sao.
            _logger.LogError(
                "IPN HỢP LỆ NHƯNG KHÔNG GHI ĐƯỢC: mã tham chiếu={MerchantReference} · mã lỗi={Ma} · lý do={LyDo}. " +
                "⇒ KHÔNG đổi trạng thái; cần người vận hành đối chiếu. [PAY-IPN-NAM-NGOAI]",
                ketQua.MerchantReference, ex.Code, ex.Message);

            return Json(400, "{\"RspCode\":\"99\",\"Message\":\"" + (ex.Code ?? "unknown") + "\"}");
        }
    }

    /// <summary>
    /// Trả body **nguyên văn** với `Content-Type: application/json`.
    /// 📌 Không dùng `Ok(object)`: nó serialize theo camelCase và bọc thêm lớp ⇒ body **không còn** đúng hình
    /// dạng `{"RspCode":"00","Message":"…"}` mà VNPay đọc.
    /// </summary>
    private ContentResult Json(int statusCode, string than)
        => new() { Content = than, ContentType = "application/json; charset=utf-8", StatusCode = statusCode };

    /// <summary>
    /// Header của request ⇒ từ điển cho <see cref="IPaymentProvider.VerifyWebhookAsync"/>.
    /// 📌 Hiện VNPay **không** dùng header để xác thực (chữ ký nằm trong query), nhưng hợp đồng của interface
    /// nhận header ⇒ truyền **thật** thay vì từ điển rỗng: nếu sau này cổng khác xác thực bằng header thì
    /// controller này **không** phải sửa.
    /// </summary>
    private Dictionary<string, string> ThuThapHeader()
    {
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (ten, giaTri) in Request.Headers)
        {
            header[ten] = giaTri.ToString();
        }

        return header;
    }
}
