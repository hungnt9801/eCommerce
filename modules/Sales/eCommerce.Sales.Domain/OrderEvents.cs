using eCommerce.Kernel.Events;

namespace eCommerce.Orders;

/// <summary>
/// **SỰ KIỆN: ĐƠN HÀNG ĐỔI TRẠNG THÁI** (`SAL-003` BR-017) — phát **trong cùng Unit of Work** với lần ghi
/// trạng thái + lịch sử, đi qua **outbox** (`XC-002` §17) ⇒ *không mất khi process chết giữa lúc publish*.
///
/// 🚨 **VÌ SAO GẮN `INotificationEvent` (phân loại, không phải chi tiết kỹ thuật):**
/// dữ liệu **đã nằm trong DB** trước khi phát (`OrdOrders` + `OrdStatusHistory`) ⇒ mất sự kiện **không** mất
/// dữ liệu; người nhận đọc lại lịch sử được. 📌 Nhờ marker này, `EventCoverageScan` lúc khởi động phân loại
/// *"chưa có người nhận"* ở mức **Warning** thay vì **Error** — đúng `XC-002` §7.1 (bài học 15/10/2026:
/// gắn marker sai cho sự kiện **mang dữ liệu** sẽ **hạ** cảnh báo của sự kiện thật).
///
/// ⚠️ **KHÔNG** gắn marker này cho `PaymentSucceededEto`/`ChannelOrderPulledEto`: mất chúng là **mất dữ liệu**.
/// </summary>
public class OrderStatusChangedEto : INotificationEvent
{
    public Guid OrderId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Số đơn hiển thị — để log/consumer không phải join lại.</summary>
    public string Number { get; set; } = default!;

    /// <summary>`Fulfillment` · `Payment` · `Reconciliation` · `Closure`.</summary>
    public string Axis { get; set; } = default!;

    public int FromStatus { get; set; }

    public int ToStatus { get; set; }

    /// <summary>`AggregateVersion` **của trục** sau khi đổi (D6 — không phải version của cả đơn).</summary>
    public long VersionAfter { get; set; }

    public string ActorType { get; set; } = default!;

    public Guid? ActorId { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Trùng `OrderStatusHistory.EventId` ⇒ tra chéo outbox ↔ lịch sử.</summary>
    public Guid EventId { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
