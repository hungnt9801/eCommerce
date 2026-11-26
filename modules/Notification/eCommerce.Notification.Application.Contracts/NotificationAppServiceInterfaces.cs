using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Notification;

/// <summary>Quản lý template + version bất biến. `[Authorize]` ở CẤP CLASS ở lớp hiện thực.</summary>
public interface INotificationTemplateAppService : IApplicationService
{
    Task<PagedResultDto<NotificationTemplateDto>> GetTemplatesAsync(PagedAndSortedResultRequestDto input);

    Task<NotificationTemplateDto> CreateAsync(CreateTemplateDto input);

    Task<NotificationTemplateVersionDto> PublishVersionAsync(PublishTemplateVersionDto input);

    Task<PreviewResultDto> PreviewAsync(PreviewTemplateDto input);

    Task ArchiveAsync(Guid id);
}

/// <summary>Hàng đợi: xếp thủ công, xem, sổ sự kiện, phát lại, huỷ.</summary>
public interface INotificationQueueAppService : IApplicationService
{
    Task<EnqueueResult> EnqueueAsync(SendManualRequest input);

    Task<PagedResultDto<NotificationQueueItemDto>> GetQueueAsync(GetQueueRequest input);

    Task<List<NotificationQueueEventDto>> GetEventsAsync(Guid id);

    Task<NotificationQueueItemDto> RetryAsync(Guid id);

    Task<NotificationQueueItemDto> CancelAsync(Guid id, CancelQueueItemRequest input);
}

/// <summary>Lô gửi hàng loạt (chỉ marketing) — tạo `ChoDuyet` + duyệt.</summary>
public interface INotificationBatchAppService : IApplicationService
{
    Task<NotificationBatchDto> CreateBatchAsync(CreateBatchDto input);

    Task<NotificationBatchDto> ApproveBatchAsync(Guid id);
}
