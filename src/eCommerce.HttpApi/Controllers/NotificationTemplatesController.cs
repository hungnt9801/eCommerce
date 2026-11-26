using System.ComponentModel.DataAnnotations;
using eCommerce.HttpApi.Dtos;
using eCommerce.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Validation;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// **`NOT-001` / `W22-3`** — template + version bất biến, bề mặt `/api/v1/notifications/templates`
/// cho màn Web Admin (SRS §10).
///
/// 🔐 **Quyền tách theo VIỆC** (`eCommerceConsts.Permissions.Notifications`, SRS §9):
/// <list type="bullet">
///   <item>`GET` · `POST …/preview` ⇒ `Notification.Templates.View` (cấp class);</item>
///   <item>`POST` ⇒ `Notification.Templates.Manage` (tạo template `Draft`);</item>
///   <item>`POST …/versions` · `POST …/archive` ⇒ `Notification.Templates.Publish`
///     (thao tác rủi ro cao — nội dung phát ra người thật, và archive cũng do app service
///     `[Authorize(TemplatesPublish)]` bảo vệ, SRS §8/§9).</item>
/// </list>
///
/// 🚨 **Mọi đường đọc đi qua app service** — controller không đụng repository, không lọc `Archived`
/// (SRS §10 buộc hiện cả bản ghi `Archived`).
/// </summary>
[ApiController]
[Route("api/v1/notifications/templates")]
[Authorize(eCommerceConsts.Permissions.Notifications.TemplatesView)]
public class NotificationTemplatesController : AbpControllerBase
{
    private readonly INotificationTemplateAppService _templates;

    public NotificationTemplatesController(INotificationTemplateAppService templates)
    {
        _templates = templates;
    }

    /// <summary>
    /// Danh sách template có phân trang. 🚨 **Trả cả** template `Archived` (tin cũ còn ghim version) —
    /// app service đã trả toàn bộ nên controller **không** lọc.
    /// </summary>
    [HttpGet]
    public async Task<ApiPage<NotificationTemplateDto>> GetListAsync(
        [FromQuery] NotificationTemplateListQuery input)
    {
        var (page, pageSize, skipCount, maxResultCount) =
            NotificationApiPaging.ChuanHoa(input.Page, input.PageSize);

        var result = await _templates.GetTemplatesAsync(new PagedAndSortedResultRequestDto
        {
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            Sorting = string.IsNullOrWhiteSpace(input.Sort) ? null : input.Sort
        });

        return new ApiPage<NotificationTemplateDto>(result.Items, result.TotalCount, page, pageSize);
    }

    /// <summary>Tạo template ở trạng thái `Draft` (chưa phát ra ai). 🔐 `Notification.Templates.Manage`.</summary>
    [HttpPost]
    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesManage)]
    public Task<NotificationTemplateDto> CreateAsync([FromBody] CreateTemplateDto input)
        => _templates.CreateAsync(input);

    /// <summary>
    /// Publish một version mới ⇒ **bất biến** (muốn đổi phải tạo version mới).
    /// 🔐 `Notification.Templates.Publish`.
    /// 📌 `id` trên route là nguồn sự thật: gán vào `TemplateId`; nếu body có `TemplateId` khác ⇒ **400**.
    /// </summary>
    [HttpPost("{id:guid}/versions")]
    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesPublish)]
    public Task<NotificationTemplateVersionDto> PublishVersionAsync(
        Guid id, [FromBody] PublishTemplateVersionDto input)
    {
        if (input.TemplateId != Guid.Empty && input.TemplateId != id)
        {
            throw new AbpValidationException(new List<ValidationResult>
            {
                new($"TemplateId trong body ({input.TemplateId}) khác id trên route ({id}).")
            });
        }

        input.TemplateId = id;
        return _templates.PublishVersionAsync(input);
    }

    /// <summary>
    /// Ngừng dùng template (**không** xoá — tin cũ còn ghim version). 🔐 `Notification.Templates.Publish`.
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesPublish)]
    public async Task<IActionResult> ArchiveAsync(Guid id)
    {
        await _templates.ArchiveAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Render thử với dữ liệu mẫu — **không** ghi DB, **không** gửi (SRS §10; `D-260`).
    /// 🔐 `Notification.Templates.View` (cấp class đã phủ; khai lại để hợp đồng hiển nhiên).
    /// </summary>
    [HttpPost("preview")]
    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesView)]
    public Task<PreviewResultDto> PreviewAsync([FromBody] PreviewTemplateDto input)
        => _templates.PreviewAsync(input);
}
