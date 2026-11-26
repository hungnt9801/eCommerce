using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Volo.Abp.Validation;

namespace eCommerce.Notification;

/// <summary>Lô gửi hàng loạt (chỉ marketing) — tạo `ChoDuyet` + duyệt (tách nhiệm vụ).</summary>
[Authorize]
public class NotificationBatchAppService : ApplicationService, INotificationBatchAppService
{
    private readonly IRepository<NotificationBatch, Guid> _batches;
    private readonly IRepository<NotificationQueueItem, Guid> _items;
    private readonly IRepository<NotificationQueueEvent, Guid> _events;
    private readonly IRepository<NotificationTemplate, Guid> _templates;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAbpDistributedLock _lock;
    private readonly IDistributedEventBus _bus;
    private readonly IConfiguration _configuration;

    public NotificationBatchAppService(
        IRepository<NotificationBatch, Guid> batches,
        IRepository<NotificationQueueItem, Guid> items,
        IRepository<NotificationQueueEvent, Guid> events,
        IRepository<NotificationTemplate, Guid> templates,
        IGuidGenerator guid,
        IClock clock,
        ICurrentUser currentUser,
        IAbpDistributedLock lockService,
        IDistributedEventBus bus,
        IConfiguration configuration)
    {
        _batches = batches;
        _items = items;
        _events = events;
        _templates = templates;
        _guid = guid;
        _clock = clock;
        _currentUser = currentUser;
        _lock = lockService;
        _bus = bus;
        _configuration = configuration;
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.BatchesCreate)]
    public async Task<NotificationBatchDto> CreateBatchAsync(CreateBatchDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var template = await _templates.FindAsync(input.TemplateId)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "template");

        // D-259: tối đa `Notification:Batch:MaxRecipients` (mặc định 50.000); vượt ⇒ 400 (framework).
        var toiDa = _configuration.GetValue(
            NotificationSettingKeys.BatchMaxRecipients, NotificationSettingKeys.BatchMaxRecipientsMacDinh);

        if (toiDa <= 0)
        {
            toiDa = NotificationSettingKeys.BatchMaxRecipientsMacDinh;
        }

        if (input.TotalItems > toiDa)
        {
            throw new AbpValidationException(new List<ValidationResult>
            {
                new($"Số người nhận một lô ({input.TotalItems}) vượt tối đa ({toiDa}); hãy chia lô.")
            });
        }

        var batch = NotificationBatch.TaoMoi(
            _guid.Create(),
            input.Code,
            template.Id,
            input.TemplateVersionId,
            input.IdempotencyKey,
            input.TotalItems,
            _currentUser.Id,
            input.CorrelationId,
            CurrentTenant.Id);

        await _batches.InsertAsync(batch, autoSave: true);
        return batch.ToDto();
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.BatchesApprove)]
    public async Task<NotificationBatchDto> ApproveBatchAsync(Guid id)
    {
        await using var handle = await _lock.TryAcquireAsync(
            NotificationLockKeys.Batch(id), TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "notification:batch");
        }

        var batch = await _batches.FindAsync(id)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "batch");

        var now = _clock.Now.ToUniversalTime();
        var userId = _currentUser.Id ?? Guid.Empty;

        // 🚨 Người tạo KHÔNG được tự duyệt (tách nhiệm vụ — NÉM 0011).
        batch.Duyet(userId, now);
        await _batches.UpdateAsync(batch, autoSave: false);

        // Chuyển các mục `ChoDuyet` của lô sang `ChoGui` + ghi event (cùng UoW).
        var cacMuc = await _items.GetListAsync(x => x.BatchId == batch.Id && x.Status == NotificationQueueStatus.ChoDuyet);
        foreach (var muc in cacMuc)
        {
            var transition = muc.DuyetLo(userId);
            await _events.InsertAsync(
                NotificationQueueEvent.TaoMoi(
                    _guid.Create(), muc.Id, transition, muc.CorrelationId, userId, now, tenantId: muc.TenantId),
                autoSave: false);
        }

        await _bus.PublishAsync(
            new Events.NotificationBatchApprovedEto(
                batch.TenantId, batch.Id, batch.TotalItems, batch.ApprovedByUserId, batch.CorrelationId),
            onUnitOfWorkComplete: false, useOutbox: true);

        await CurrentUnitOfWork!.SaveChangesAsync();

        return batch.ToDto();
    }
}
