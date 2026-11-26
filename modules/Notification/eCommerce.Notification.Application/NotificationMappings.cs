namespace eCommerce.Notification;

/// <summary>Ánh xạ entity → DTO (map tay — không dùng AutoMapper).</summary>
internal static class NotificationMappings
{
    public static NotificationTemplateDto ToDto(this NotificationTemplate t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        Code = t.Code,
        Name = t.Name,
        ChannelKind = t.ChannelKind,
        Locale = t.Locale,
        Purpose = t.Purpose,
        CoalesceWindowSeconds = t.CoalesceWindowSeconds,
        DefaultExpiresAfterSeconds = t.DefaultExpiresAfterSeconds,
        Status = t.Status,
        CurrentPublishedVersionId = t.CurrentPublishedVersionId,
        IsSystem = t.IsSystem
    };

    public static NotificationTemplateVersionDto ToDto(this NotificationTemplateVersion v) => new()
    {
        Id = v.Id,
        TemplateId = v.TemplateId,
        Version = v.Version,
        Subject = v.Subject,
        BodyText = v.BodyText,
        BodyFormat = v.BodyFormat,
        Placeholders = v.GetPlaceholders()
            .Select(p => new NotificationPlaceholderDto { Name = p.Name, Kind = p.Kind, Required = p.Required })
            .ToList(),
        ContentHash = v.ContentHash,
        PublishedAtUtc = v.PublishedAtUtc,
        PublishedByUserId = v.PublishedByUserId
    };

    public static NotificationQueueItemDto ToDto(this NotificationQueueItem i) => new()
    {
        Id = i.Id,
        TenantId = i.TenantId,
        IdempotencyKey = i.IdempotencyKey,
        RootItemId = i.RootItemId,
        BatchId = i.BatchId,
        SourceEventType = i.SourceEventType,
        SourceAggregateId = i.SourceAggregateId,
        ChannelKind = i.ChannelKind,
        TemplateId = i.TemplateId,
        TemplateVersionId = i.TemplateVersionId,
        // 🚨 Chỉ hiển thị bản che — CẤM hiển thị người nhận đầy đủ (`D-191`).
        RecipientMasked = "***",
        RenderedSubject = i.RenderedSubject,
        RenderedBodyMasked = i.RenderedBodyMasked,
        Priority = i.Priority,
        Status = i.Status,
        AttemptCount = i.AttemptCount,
        ScheduledAtUtc = i.ScheduledAtUtc,
        ExpiresAtUtc = i.ExpiresAtUtc,
        DispatchedAtUtc = i.DispatchedAtUtc,
        LastErrorCode = i.LastErrorCode,
        LastReasonMasked = i.LastReasonMasked,
        CorrelationId = i.CorrelationId
    };

    public static NotificationQueueEventDto ToDto(this NotificationQueueEvent e) => new()
    {
        Id = e.Id,
        QueueItemId = e.QueueItemId,
        EventType = e.EventType,
        FromStatus = e.FromStatus,
        ToStatus = e.ToStatus,
        ReasonMasked = e.ReasonMasked,
        ErrorCode = e.ErrorCode,
        CorrelationId = e.CorrelationId,
        ActorUserId = e.ActorUserId,
        OccurredAtUtc = e.OccurredAtUtc
    };

    public static NotificationBatchDto ToDto(this NotificationBatch b) => new()
    {
        Id = b.Id,
        Code = b.Code,
        TemplateId = b.TemplateId,
        TemplateVersionId = b.TemplateVersionId,
        Status = b.Status,
        TotalItems = b.TotalItems,
        CreatedByUserId = b.CreatedByUserId,
        ApprovedByUserId = b.ApprovedByUserId,
        ApprovedAtUtc = b.ApprovedAtUtc,
        CorrelationId = b.CorrelationId
    };
}
