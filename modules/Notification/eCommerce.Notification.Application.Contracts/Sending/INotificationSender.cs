namespace eCommerce.Notification;

/// <summary>
/// Hợp đồng bàn giao **NỘI MODULE** — CANONICAL cho WP-22 (`D-264`). `NOT-002` hiện thực ở WP-23.
/// Nhận nội dung **ĐÃ RENDER** (`Subject`/`Body`) + `RecipientEncrypted`; **KHÔNG** nhận
/// `TemplateCode`/`Data`/`Recipient` plaintext (`ADR-06`/`ADR-09`).
/// </summary>
public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(NotificationSendRequest request, CancellationToken ct = default);
}

/// <summary>
/// Yêu cầu gửi — `Subject`/`Body` là bản **đã render + mask** đã lưu trên mục; **KHÔNG** có PII plaintext.
/// </summary>
public sealed record NotificationSendRequest(
    Guid? TenantId,
    Guid QueueItemId,
    string IdempotencyKey,
    ChannelKind ChannelKind,
    string RecipientEncrypted,
    string Subject,
    string Body,
    int Priority);

public sealed record NotificationSendResult(
    bool Accepted,
    bool AlreadySent,
    bool IsTransient,
    string? ErrorCode);
