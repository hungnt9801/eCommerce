namespace eCommerce.Notification;

/// <summary>
/// Yêu cầu xếp hàng — biên vào #1 (ETO) và #2 (API). `Recipient` **CHỈ** ở bộ nhớ — CẤM log/ETO (`D-191`).
/// </summary>
public sealed record EnqueueRequest(
    Guid? TenantId,
    string IdempotencyKey,
    string TemplateCode,
    ChannelKind ChannelKind,
    string Locale,
    string Recipient,
    IReadOnlyDictionary<string, string> Data,
    int Priority,
    DateTime ScheduledAtUtc,
    DateTime? ExpiresAtUtc = null,
    Guid? BatchId = null,
    string? SourceEventType = null,
    string? SourceAggregateId = null,
    string CorrelationId = "");

/// <summary>Kết quả xếp hàng. `AlreadyQueued` = trùng khoá payload giống (không ném).</summary>
public sealed record EnqueueResult(
    Guid QueueItemId,
    bool AlreadyQueued,
    string Status,
    string? ErrorCode);

/// <summary>
/// Cổng xếp hàng của module (`D-197`) — dùng bởi app service (biên #2) và job (biên nội bộ).
/// 🚨 `IdempotencyKey` PHẢI ổn định — CẤM sinh `Guid.NewGuid()` mỗi lần gọi.
/// </summary>
public interface INotificationQueueAppender
{
    Task<EnqueueResult> EnqueueAsync(EnqueueRequest request, CancellationToken ct = default);
}
