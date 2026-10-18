using eCommerce.PaymentHub.Payments;

namespace eCommerce.PaymentHub;

/// <summary>
/// 🚨 **YÊU CẦU TẠO GIAO DỊCH THANH TOÁN** (`S4`) — đường vào **DUY NHẤT** của bảng `PayTransactions`
/// ở tầng sản phẩm (trước `S4`, `new PaymentTransaction(...)` **chỉ** có trong test — nợ #1 của `PAY-001` §15.3).
///
/// 🔒 **VÌ SAO INPUT MANG `SoDon` CHỨ KHÔNG PHẢI `Order`:** `PaymentHub` **không** tham chiếu `Order`
/// (module tự chứa). Nơi gọi **đã có** đơn ⇒ truyền **số đơn** xuống; 🔑 số đơn là thứ **ổn định** (không đổi
/// sau khi tạo) nên dùng được làm gốc của `MerchantReference`.
///
/// ⚠️ **`OrderId` là THAM CHIẾU MỀM** (không FK — `PAY-001` §5.1), vì giao dịch có thể tồn tại **trước** khi
/// đơn được ghi (nạp ví) hoặc **không** thuộc đơn nào.
/// </summary>
public class TaoGiaoDichInput
{
    /// <summary>Đơn liên quan (nếu có). 🔑 Cầu nối `Order.PaymentStatus` dựa vào **chính** trường này.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>Số đơn hiển thị (`Order.Number`) — gốc **ổn định** của `MerchantReference`.
    /// 🚨 **CẤM** truyền chuỗi ngẫu nhiên mỗi lần gọi: đó là `Guid.NewGuid()` đội lốt (`BR-PAY-002`).</summary>
    public string SoDon { get; set; } = default!;

    /// <summary>Số tiền **phải thu** (đơn vị tiền thật, VND). 🔒 `> 0`; so sánh bằng `decimal.Compare`, cấm `==`.</summary>
    public decimal SoTien { get; set; }

    public string TienTe { get; set; } = "VND";

    /// <summary>URL cổng trả khách về. ⚠️ **KHÔNG phải bằng chứng thanh toán** (`PAY-001` `D2`) — chỉ để hiển thị kết quả.</summary>
    public string ReturnUrl { get; set; } = default!;

    /// <summary>URL cổng gọi lại khi có kết quả — **BẮT BUỘC HTTPS** với VNPay (`PAY-001` §7.2).</summary>
    public string IpnUrl { get; set; } = default!;

    /// <summary>IP của khách (VNPay bắt buộc `vnp_IpAddr`). `null` ⇒ provider dùng mặc định an toàn.</summary>
    public string? ClientIp { get; set; }

    /// <summary>`vnp_OrderInfo` — nội dung đơn hiển thị ở cổng/ngân hàng. `null` ⇒ tự sinh từ số đơn.</summary>
    public string? NoiDung { get; set; }
}

/// <summary>
/// **KẾT QUẢ TẠO GIAO DỊCH** (`S4`).
///
/// 🔑 **`LaGiaoDichDaCo` là trường quan trọng nhất** — nó nói *"lần gọi này **không** tạo giao dịch mới"*.
/// 📌 Nếu không có nó, hai lần gọi cho **cùng một đơn** (khách bấm hai lần, client retry sau timeout) trả về
/// hai object **trông giống hệt nhau** ⇒ không ai phân biệt được *"vừa tạo"* với *"dùng lại"* — và đó chính là
/// chỗ người ta viết báo cáo sai (*"hệ thống tạo 2 giao dịch"* trong khi thật ra chỉ có 1).
/// </summary>
public class TaoGiaoDichKetQuaDto
{
    public Guid PaymentTransactionId { get; set; }

    /// <summary>`vnp_TxnRef` — **cùng một giá trị** cho mọi lần gọi lại trên cùng một ý định thu tiền.</summary>
    public string MerchantReference { get; set; } = default!;

    public string ProviderCode { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = default!;

    /// <summary>Trạng thái **trong DB** sau thao tác (`Initiated`/`Pending`/…).</summary>
    public string TrangThai { get; set; } = default!;

    /// <summary>URL để redirect khách sang cổng. `null` với cổng webhook-first (Casso/SePay) hoặc khi cổng lỗi.</summary>
    public string? CheckoutUrl { get; set; }

    /// <summary>`true` = **dùng lại** giao dịch đang mở của đơn (`MerchantReference` **không** đổi).</summary>
    public bool LaGiaoDichDaCo { get; set; }

    /// <summary>Lỗi khi dựng URL thanh toán (nếu có) — 🔑 **KHÔNG** có nghĩa *"khách chưa trả"*: giao dịch **vẫn**
    /// tồn tại và có thể được thu qua đường khác; nó chỉ nói *"chưa lấy được URL"*.</summary>
    public string? LoiTaoUrl { get; set; }
}

/// <summary>
/// 🚨 **KẾT QUẢ GHI NHẬN MỘT KẾT QUẢ CỦA CỔNG** (`S4`) — trả về **sự thật đã ghi**, không phải ý định.
///
/// 🔒 **Hai cờ tách rời có chủ ý:**
/// <list type="bullet">
///   <item><see cref="DaGhiSo"/> — lần này có **thực sự** đổi trạng thái trong DB (lần thứ 2 của một IPN trùng
///     sẽ là `false`);</item>
///   <item><see cref="DaPhatSuKien"/> — có phát ETO hay không (📌 luôn bằng `DaGhiSo`: phát sự kiện cho một
///     chuyển dịch **không xảy ra** sẽ làm đơn nhận *"đã trả tiền"* hai lần ⇒ notification trùng, audit trùng).</item>
/// </list>
/// </summary>
public class KetQuaGhiNhanDto
{
    public Guid PaymentTransactionId { get; set; }

    public string MerchantReference { get; set; } = default!;

    public string TrangThaiSau { get; set; } = default!;

    /// <summary>`true` = lần này **thực sự** đổi trạng thái (lần gửi trùng ⇒ `false`, **không** phải lỗi).</summary>
    public bool DaGhiSo { get; set; }

    public bool DaPhatSuKien { get; set; }

    /// <summary>🔑 **Bằng chứng IPN gửi trùng** — đếm **mọi** lần nhận, kể cả lần bị bỏ qua.</summary>
    public int SoLanNhanWebhook { get; set; }

    /// <summary>Giao dịch đã thu được tiền (dùng cho UI: *"đơn này đã trả chưa"*).</summary>
    public bool DaThuTien { get; set; }
}

/// <summary>
/// 🚨 **ĐƯỜNG GHI NHẬN KẾT QUẢ CỦA CỔNG** (`S4`) — cầu nối giữa *"cổng nói gì"* và *"sổ sách ghi gì"*.
///
/// 🔒 **ĐÂY LÀ HỢP ĐỒNG NỘI BỘ, KHÔNG PHẢI API CÔNG KHAI** và **cố ý KHÔNG** phải `IApplicationService`
/// ⇒ ABP **không** sinh controller cho nó. Lý do: một endpoint HTTP *"đánh dấu đã trả tiền"* là **cửa mở**
/// chính xác thứ `PAY-001` `D3`/`BR-PAY-001` cấm (*chỉ IPN đã xác thực chữ ký hoặc `querydr` đã xác thực mới
/// được chuyển `Succeeded`*). 📌 Endpoint IPN thật (`S5`) **gọi vào** đây **sau khi** đã xác thực chữ ký.
///
/// 🚨 **Fail-closed:** implementation **NÉM** `eCommerce:Payment:0008` nếu
/// <see cref="WebhookVerificationResult.SignatureValid"/> là `false` — không có cờ cấu hình nào tắt được
/// (`PAY-001` §7.7.4 #1).
/// </summary>
public interface IXuLyKetQuaCong
{
    /// <summary>Ghi nhận kết quả **đã qua xác thực chữ ký** của một cổng thanh toán.</summary>
    /// <param name="ketQua">Kết quả của <see cref="IPaymentProvider.VerifyWebhookAsync"/> — 🔒 phải có
    /// `SignatureValid = true`.</param>
    Task<KetQuaGhiNhanDto> GhiNhanAsync(WebhookVerificationResult ketQua, CancellationToken cancellationToken = default);
}
