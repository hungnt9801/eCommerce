using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>
/// **NHẬT KÝ CHUYỂN TRẠNG THÁI ĐƠN HÀNG** (`SAL-003` §5.2.2) — **APPEND-ONLY**.
///
/// 🚨 **VÌ SAO PHẢI LÀ MỘT BẢNG RIÊNG (không phải cột `LastStatusChangedAt`):** câu hỏi cần trả lời khi
/// tranh chấp với khách/sàn là *"**ai** đổi, **lúc nào**, **từ đâu sang đâu**, **vì sao**"* — một cột thời gian
/// không trả lời được. 📌 Không có bảng này thì mọi tuyên bố *"có state machine"* chỉ là **niềm tin**:
/// trạng thái đổi mà **không để lại dấu vết**.
///
/// 🔒 **APPEND-ONLY (quy ước D7):** mọi thuộc tính có setter **kín**; entity **không** có phương thức sửa.
/// Lý do: lịch sử trạng thái là **chứng từ**; sửa được là mất giá trị chứng cứ — cùng nguyên tắc với sổ cái kho
/// (`StockLedger`). 🚨 **CẤM** thêm `UpdateReason(...)`/setter công khai vào lớp này.
/// </summary>
public class OrderStatusHistory : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid OrderId { get; protected set; }

    /// <summary>`Fulfillment` · `Payment` · `Reconciliation` · `Closure` (lưu **chuỗi** để đọc SQL/log dễ).</summary>
    public string Axis { get; protected set; } = default!;

    /// <summary>Giá trị trục **trước** khi đổi (số, theo enum của trục).</summary>
    public int FromStatus { get; protected set; }

    /// <summary>Giá trị trục **sau** khi đổi.</summary>
    public int ToStatus { get; protected set; }

    /// <summary>`Customer` · `Staff` · `SystemJob` · `MarketplaceWebhook` · `PaymentGateway` · `Reconciliation`.</summary>
    public string ActorType { get; protected set; } = default!;

    /// <summary>User id (nullable **chỉ** với `SystemJob`/webhook/IPN). Không FK tới `AbpUsers` — lịch sử phải sống khi user bị xoá.</summary>
    public Guid? ActorId { get; protected set; }

    /// <summary>Mã lý do (ví dụ `XacNhanDon`, `HuyDon`, `IPNXacNhan`, `BackfillMigration`).</summary>
    public string ReasonCode { get; protected set; } = default!;

    /// <summary>Mô tả ngắn — ⚠️ **cấm PII**; không nhận chuỗi tự do từ client.</summary>
    public string? ReasonText { get; protected set; }

    /// <summary>Nối với log/ETO (`XC-005`). Chuỗi rỗng nghĩa là "không có CorrelationId" — **không** null.</summary>
    public string CorrelationId { get; protected set; } = string.Empty;

    /// <summary>Trùng `OrderStatusChangedEto.EventId` (khi ETO được cài) để tra chéo outbox ↔ lịch sử.</summary>
    public Guid EventId { get; protected set; }

    /// <summary>`AggregateVersion` **của trục** sau khi đổi (không phải của cả đơn — D6).</summary>
    public long VersionAfter { get; protected set; }

    /// <summary>Thời điểm **nghiệp vụ** — không dùng `CreationTime` của ABP làm nguồn sự thật.</summary>
    public DateTime OccurredAtUtc { get; protected set; }

    protected OrderStatusHistory() { }

    public OrderStatusHistory(
        Guid id,
        Guid orderId,
        string axis,
        int fromStatus,
        int toStatus,
        StatusActorType actorType,
        Guid? actorId,
        string reasonCode,
        string? correlationId,
        long versionAfter,
        DateTime occurredAtUtc,
        string? reasonText = null)
        : base(id)
    {
        OrderId = orderId;
        Axis = axis;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorType = actorType.ToString();
        ActorId = actorId;
        ReasonCode = reasonCode;
        CorrelationId = correlationId ?? string.Empty;
        VersionAfter = versionAfter;
        OccurredAtUtc = occurredAtUtc;
        ReasonText = reasonText;
        EventId = Guid.NewGuid();
    }
}
