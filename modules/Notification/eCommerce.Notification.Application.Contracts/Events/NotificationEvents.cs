using eCommerce.Kernel.Events;

namespace eCommerce.Notification.Events;

// 🚨 Mọi ETO mang `CorrelationId` (XC-005 BR-003) và KHÔNG mang PII người nhận (`D-191`).
// Chúng là sự kiện THÔNG BÁO (gắn `INotificationEvent`): dữ liệu đã nằm trong DB ⇒ thiếu người nhận
// chỉ là Warning, KHÔNG phải mất dữ liệu.

/// <summary>Phát khi ghi mục hàng đợi (`Moi`). Tiêu thụ: `REP-*` (job bàn giao quét DB, KHÔNG nghe ETO).</summary>
public sealed record NotificationEnqueuedEto(
    Guid? TenantId,
    Guid QueueItemId,
    ChannelKind ChannelKind,
    string TemplateCode,
    string IdempotencyKey,
    DateTime ScheduledAtUtc,
    string CorrelationId) : INotificationEvent;

/// <summary>Phát khi mục `DaBanGiao`.</summary>
public sealed record NotificationHandedOffEto(
    Guid? TenantId,
    Guid QueueItemId,
    ChannelKind ChannelKind,
    string TemplateCode,
    string IdempotencyKey,
    DateTime ScheduledAtUtc,
    DateTime DispatchedAtUtc,
    int Priority,
    string CorrelationId) : INotificationEvent;

/// <summary>Phát khi mục `Loi` — cảnh báo vận hành + `REP-*`.</summary>
public sealed record NotificationQueueFailedEto(
    Guid? TenantId,
    Guid QueueItemId,
    ChannelKind ChannelKind,
    string TemplateCode,
    string IdempotencyKey,
    DateTime ScheduledAtUtc,
    string? ErrorCode,
    string? LastReasonMasked,
    string CorrelationId) : INotificationEvent;

/// <summary>Phát khi lô được duyệt.</summary>
public sealed record NotificationBatchApprovedEto(
    Guid? TenantId,
    Guid BatchId,
    int TotalItems,
    Guid? ApprovedByUserId,
    string CorrelationId) : INotificationEvent;
