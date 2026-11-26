using eCommerce.HttpApi.Dtos;
using eCommerce.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// **`NOT-001` / `W22-3`** — theo dõi hàng đợi thông báo, bề mặt `/api/v1/notifications/queue`
/// (SRS §10: bảng hàng đợi + timeline một mục).
///
/// 🔐 `[Authorize]` **cấp class** = `Notification.Queue.View` (quyền **đọc**) — đúng khuôn `/api/v1`.
/// 🚨 DTO trả về **đã che** người nhận (`RecipientMasked`/`RenderedBodyMasked`) — controller **không**
/// mở ra trường thô nào.
/// </summary>
[ApiController]
[Route("api/v1/notifications/queue")]
[Authorize(eCommerceConsts.Permissions.Notifications.QueueView)]
public class NotificationQueueController : AbpControllerBase
{
    private readonly INotificationQueueAppService _queue;

    public NotificationQueueController(INotificationQueueAppService queue)
    {
        _queue = queue;
    }

    /// <summary>
    /// Danh sách mục hàng đợi có phân trang — lọc trạng thái/kênh/khoảng `ScheduledAtUtc` (UTC).
    /// </summary>
    [HttpGet]
    public async Task<ApiPage<NotificationQueueItemDto>> GetListAsync(
        [FromQuery] NotificationQueueListQuery input)
    {
        var (page, pageSize, skipCount, maxResultCount) =
            NotificationApiPaging.ChuanHoa(input.Page, input.PageSize);

        var result = await _queue.GetQueueAsync(new GetQueueRequest
        {
            Status = input.Status,
            ChannelKind = input.ChannelKind,
            TuNgayUtc = input.TuNgayUtc,
            DenNgayUtc = input.DenNgayUtc,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            Sorting = string.IsNullOrWhiteSpace(input.Sort) ? null : input.Sort
        });

        return new ApiPage<NotificationQueueItemDto>(result.Items, result.TotalCount, page, pageSize);
    }

    /// <summary>
    /// Sổ sự kiện **append-only** của một mục (timeline) — chỉ đọc; có `ErrorCode` + `CorrelationId`.
    /// </summary>
    [HttpGet("{id:guid}/events")]
    public Task<List<NotificationQueueEventDto>> GetEventsAsync(Guid id)
        => _queue.GetEventsAsync(id);
}
