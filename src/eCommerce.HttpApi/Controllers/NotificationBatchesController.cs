using eCommerce.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// **`NOT-001` / `W22-3`** — lô gửi hàng loạt (chỉ tin `Marketing`), bề mặt
/// `/api/v1/notifications/batches` (SRS §10: form lô + nút duyệt).
///
/// 🔐 **Quyền:** module **không** có quyền đọc riêng cho lô, nên `[Authorize]` **cấp class** dùng
/// `Notification.Batches.Create` (khớp `CreateBatchAsync`); method **duyệt** khai thêm
/// `Notification.Batches.Approve` — 🚨 gửi hàng loạt ra người thật nên tách nhiệm vụ (`D-198`).
/// </summary>
[ApiController]
[Route("api/v1/notifications/batches")]
[Authorize(eCommerceConsts.Permissions.Notifications.BatchesCreate)]
public class NotificationBatchesController : AbpControllerBase
{
    private readonly INotificationBatchAppService _batches;

    public NotificationBatchesController(INotificationBatchAppService batches)
    {
        _batches = batches;
    }

    /// <summary>Tạo lô gửi ở trạng thái `ChoDuyet` (chờ người khác duyệt).</summary>
    [HttpPost]
    public Task<NotificationBatchDto> CreateAsync([FromBody] CreateBatchDto input)
        => _batches.CreateBatchAsync(input);

    /// <summary>
    /// Duyệt lô ⇒ các mục `ChoDuyet` chuyển `ChoGui`. 🔐 thêm `Notification.Batches.Approve`.
    /// 🚨 Người tạo **không** được tự duyệt — cửa chặn thật ở BE (`Duyet` ném `0011`).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(eCommerceConsts.Permissions.Notifications.BatchesApprove)]
    public Task<NotificationBatchDto> ApproveAsync(Guid id)
        => _batches.ApproveBatchAsync(id);
}
