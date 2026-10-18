namespace eCommerce.PaymentHub.Events;

/// <summary>
/// 🚨 **SỰ KIỆN: CỔNG ĐÃ XÁC NHẬN THU TIỀN VÀ TA ĐÃ GHI SỔ** (`S4`) — `PaymentSucceededEto`.
///
/// **ĐÂY LÀ CÂU TRẢ LỜI CHO `Q1` CỦA `PAY-001`** (*"ai ghi `Order.PaymentStatus`?"*): `PaymentHub` **không**
/// tham chiếu `Order` (và `eCommerce.Application` **không được** tham chiếu `PaymentHub.Application`) ⇒ cầu nối
/// đi qua **sự kiện phân tán**: nơi phát là module thanh toán, nơi nhận là handler ở tầng Sales
/// (`src/eCommerce.Application/Sales/OrderPaymentStatusHandler.cs`), nơi **có** `IRepository<Order>`.
/// 📌 Quyết định ghi ở `PAY-001` §15.1 **Q1** + §16 (`S4`).
///
/// 🔑 **VÌ SAO ETO NÀY **KHÔNG** gắn `INotificationEvent` (đọc kỹ, đây là chỗ dễ hiểu sai):**
/// `INotificationEvent` = *"mất nó **KHÔNG** mất dữ liệu"* (`shared/eCommerce.Kernel/Events/INotificationEvent.cs`).
/// Ở đây **ngược lại**: nếu sự kiện mất thì **tiền đã vào** mà đơn vẫn `Unpaid` ⇒ giao hàng sai / thu tiền hai
/// lần. Vì vậy đây là sự kiện **MANG DỮ LIỆU**: `EventCoverageChecker` sẽ báo **Error** nếu không có người nhận
/// — đó là hành vi **mong muốn**, và có test khoá lại
/// (`test/eCommerce.Tests/PaymentHub/OrderPaymentStatusHandlerBatBienTests.cs`).
///
/// ⚠️ **CHƯA QUA OUTBOX BỀN VỮNG (`XC-002`)** — hiện phát qua event bus **in-process** (cùng UoW, cùng
/// transaction DB) ⇒ *"không ghi sổ được thì cũng không phát"* và ngược lại; 🔒 nhưng **chưa** chống mất sự kiện
/// khi tiến trình chết **sau** khi commit. Đó là **nợ có mã** ghi ở `PAY-001` §16.4, **không** phải tính năng đã có.
///
/// 🔒 **KHÔNG chứa dữ liệu nhạy cảm**: không số thẻ, không payload thô (`XC-005` **BR-005**; đối chiếu
/// `ADDR-004` §7.1 — PII trong event bus là PII **khó xoá nhất**).
/// </summary>
[Serializable]
public class PaymentSucceededEto
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    /// <summary>Tenant sở hữu giao dịch — 🔑 cần cho consumer chạy **ngoài** UoW phát (outbox/relay tương lai).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Khoá giao dịch thanh toán (giữ tên chuẩn `AggregateId` của `XC-002` **D6**).</summary>
    public Guid AggregateId { get; set; }

    /// <summary>Trùng `AggregateId` cho **dễ đọc** ở consumer — 📌 hai trường cùng giá trị là **có chủ ý**
    /// (một số consumer đọc theo tên nghiệp vụ, số khác theo tên chuẩn `XC-002`).</summary>
    public Guid PaymentTransactionId { get; set; }

    /// <summary>Đơn liên quan. `null` = giao dịch **nạp ví** hoặc không gắn đơn ⇒ handler **bỏ qua**, không phải lỗi.</summary>
    public Guid? OrderId { get; set; }

    public string ProviderCode { get; set; } = default!;

    /// <summary>`vnp_TxnRef` — mã **của ta**, ổn định (`SinhMaThamChieu`).</summary>
    public string MerchantReference { get; set; } = default!;

    /// <summary>`vnp_TransactionNo` — mã **cổng** cấp. 🔑 Dùng làm `IdempotencyKey` + để hoàn tiền tra tất định.</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>**decimal** — không bao giờ `double` cho tiền.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    /// <summary>Thời điểm **cổng** xác nhận (không phải lúc ta nhận webhook).</summary>
    public DateTime? PaidAtUtc { get; set; }
}

/// <summary>
/// **SỰ KIỆN: CỔNG BÁO GIAO DỊCH THẤT BẠI / KHÁCH HUỶ** (`S4`) — chỉ phát khi **thật sự** đổi trạng thái
/// (tức `MarkFailed` trả `true`), nhờ vậy IPN thất bại gửi lại **không** phát lại sự kiện.
///
/// 🔒 Cùng quy ước với <see cref="PaymentSucceededEto"/>: **mang dữ liệu** (đơn phải biết *"đã thất bại"* để
/// không chờ tiền), **không** PII.
/// ⚠️ **KHÔNG** dùng sự kiện này để *"hạ"* đơn đã ở `Paid`/`Refunded`: luật hấp thụ nằm ở
/// `Order.DatTrangThaiThanhToan` (`S4`) và handler **phải** đi qua đó.
/// </summary>
[Serializable]
public class PaymentFailedEto
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

    public Guid? TenantId { get; set; }

    public Guid AggregateId { get; set; }

    public Guid PaymentTransactionId { get; set; }

    public Guid? OrderId { get; set; }

    public string ProviderCode { get; set; } = default!;

    public string MerchantReference { get; set; } = default!;

    public string? ProviderTransactionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    /// <summary>Lý do thất bại **đã cắt 2000 ký tự** ở entity. Không chứa dữ liệu nhạy cảm.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Mã phản hồi của cổng (VNPay: `vnp_ResponseCode`) — giữ nguyên văn để điều tra.</summary>
    public string? ProviderResponseCode { get; set; }
}
