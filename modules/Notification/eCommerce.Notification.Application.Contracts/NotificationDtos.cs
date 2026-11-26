using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Notification;

/// <summary>Placeholder trong DTO — `{name, kind, required}`.</summary>
public class NotificationPlaceholderDto
{
    public string Name { get; set; } = string.Empty;
    public NotificationPlaceholderKind Kind { get; set; }
    public bool Required { get; set; }
}

public class NotificationTemplateDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ChannelKind ChannelKind { get; set; }
    public string Locale { get; set; } = string.Empty;
    public NotificationPurpose Purpose { get; set; }
    public int CoalesceWindowSeconds { get; set; }
    public int? DefaultExpiresAfterSeconds { get; set; }
    public NotificationTemplateStatus Status { get; set; }
    public Guid? CurrentPublishedVersionId { get; set; }
    public bool IsSystem { get; set; }
}

public class NotificationTemplateVersionDto : EntityDto<Guid>
{
    public Guid TemplateId { get; set; }
    public int Version { get; set; }
    public string? Subject { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public NotificationBodyFormat BodyFormat { get; set; }
    public IReadOnlyList<NotificationPlaceholderDto> Placeholders { get; set; } = [];
    public string ContentHash { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public Guid? PublishedByUserId { get; set; }
}

public class CreateTemplateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ChannelKind ChannelKind { get; set; }
    public string Locale { get; set; } = "vi";
    public NotificationPurpose Purpose { get; set; }
    public NotificationBodyFormat BodyFormat { get; set; } = NotificationBodyFormat.Text;
}

public class PublishTemplateVersionDto
{
    public Guid TemplateId { get; set; }
    public string? Subject { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public IReadOnlyList<NotificationPlaceholderDto> Placeholders { get; set; } = [];
    public NotificationBodyFormat BodyFormat { get; set; } = NotificationBodyFormat.Text;
}

public class PreviewTemplateDto
{
    public Guid TemplateId { get; set; }
    public Guid? TemplateVersionId { get; set; }

    /// <summary>Dữ liệu mẫu người dùng nhập tay — CẤM dữ liệu thật (`D-260`).</summary>
    public Dictionary<string, string> SampleData { get; set; } = new(StringComparer.Ordinal);
}

public class PreviewResultDto
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyFingerprint { get; set; } = string.Empty;
}

/// <summary>Yêu cầu xếp một tin thủ công (biên vào #2) — **bắt buộc** `IdempotencyKey`.</summary>
public class SendManualRequest
{
    public string? IdempotencyKey { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public ChannelKind ChannelKind { get; set; }
    public string Locale { get; set; } = "vi";
    public string Recipient { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.Ordinal);
    public int Priority { get; set; } = NotificationPriorities.Normal;
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    /// <summary>Gắn mục vào một lô gửi (`ChoDuyet`) — bổ sung hợp đồng `docDeviation` (BA cập nhật §6.1).</summary>
    public Guid? BatchId { get; set; }
    public string? SourceAggregateId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class GetQueueRequest : PagedAndSortedResultRequestDto
{
    public NotificationQueueStatus? Status { get; set; }
    public ChannelKind? ChannelKind { get; set; }
    public DateTime? TuNgayUtc { get; set; }
    public DateTime? DenNgayUtc { get; set; }
}

public class CancelQueueItemRequest
{
    public string LyDo { get; set; } = string.Empty;
}

public class NotificationQueueItemDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? RootItemId { get; set; }
    public Guid? BatchId { get; set; }
    public string? SourceEventType { get; set; }
    public string? SourceAggregateId { get; set; }
    public ChannelKind ChannelKind { get; set; }
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }

    /// <summary>Người nhận đã **che** — CẤM hiển thị đầy đủ (`D-191`).</summary>
    public string RecipientMasked { get; set; } = string.Empty;

    public string? RenderedSubject { get; set; }
    public string? RenderedBodyMasked { get; set; }
    public int Priority { get; set; }
    public NotificationQueueStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastReasonMasked { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class NotificationQueueEventDto : EntityDto<Guid>
{
    public Guid QueueItemId { get; set; }
    public NotificationQueueEventType EventType { get; set; }
    public NotificationQueueStatus? FromStatus { get; set; }
    public NotificationQueueStatus? ToStatus { get; set; }
    public string? ReasonMasked { get; set; }
    public string? ErrorCode { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class CreateBatchDto
{
    public string Code { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class NotificationBatchDto : EntityDto<Guid>
{
    public string Code { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public NotificationBatchStatus Status { get; set; }
    public int TotalItems { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
