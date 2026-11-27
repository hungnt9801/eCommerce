using eCommerce.Kernel.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Notification;

/// <summary>
/// Worker bàn giao định kỳ — **MỘT** worker, **0** dynamic worker (bài học C10, §7.1).
/// Ba pha mỗi nhịp (§7.2): (A) khôi phục lease; (B) hết hạn; (C) sẵn sàng + bàn giao.
/// 🚨 `Notification:EnableDispatch` mặc định **`false`** ⇒ chỉ log, KHÔNG bàn giao (`D-263`).
/// </summary>
public class NotificationDispatchWorker : AsyncPeriodicBackgroundWorkerBase
{
    public NotificationDispatchWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // Chạy ngay lần đầu (bài học CHN-002) để không trễ một nhịp sau deploy.
        Timer.RunOnStart = true;
        Timer.Period = 10_000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("[NOT-DISPATCH] Bắt đầu nhịp quét hàng đợi thông báo.");

        var sp = workerContext.ServiceProvider;

        var config = sp.GetRequiredService<IConfiguration>();
        if (!config.GetValue(NotificationSettingKeys.EnableDispatch, false))
        {
            Logger.LogWarning(
                "[NOT-DISPATCH] `{Key}=false` — KHÔNG bàn giao (chưa có kênh gửi thật, NOT-002/WP-23).",
                NotificationSettingKeys.EnableDispatch);
            return;
        }

        var items = sp.GetRequiredService<IRepository<NotificationQueueItem, Guid>>();
        var events = sp.GetRequiredService<IRepository<NotificationQueueEvent, Guid>>();
        var templates = sp.GetRequiredService<IRepository<NotificationTemplate, Guid>>();
        var sender = sp.GetRequiredService<INotificationSender>();
        var lockService = sp.GetRequiredService<IAbpDistributedLock>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var guid = sp.GetRequiredService<IGuidGenerator>();
        var clock = sp.GetRequiredService<IClock>();
        var bus = sp.GetRequiredService<IDistributedEventBus>();
        var metrics = sp.GetRequiredService<eCommerceMetrics>();
        var currentTenant = sp.GetRequiredService<ICurrentTenant>();

        var now = clock.Now.ToUniversalTime();
        var leaseSeconds = config.GetValue(
            NotificationSettingKeys.DispatchLeaseSeconds, NotificationSettingKeys.DispatchLeaseSecondsMacDinh);

        var tatCa = await items.GetListAsync(x =>
            x.Status == NotificationQueueStatus.Moi
            || x.Status == NotificationQueueStatus.ChoGui
            || x.Status == NotificationQueueStatus.DangBanGiao);

        var tenants = tatCa.Select(x => x.TenantId).Distinct().ToList();
        if (tenants.Count == 0)
        {
            tenants.Add(currentTenant.Id);
        }

        foreach (var tenantId in tenants)
        {
            await using var handle = await lockService.TryAcquireAsync(
                NotificationLockKeys.QueueDispatch(tenantId), TimeSpan.FromSeconds(5));

            if (handle is null)
            {
                metrics.LockSkipped.Add(
                    1, new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "notification"));
                Logger.LogDebug("[NOT-DISPATCH] Không giành được khoá của tenant {TenantId} — bỏ qua nhịp.", tenantId);
                continue;
            }

            await KhoiPhucLeaseAsync(items, events, uowManager, guid, metrics, Logger, tenantId, now);
            await HetHanAsync(items, events, uowManager, guid, metrics, tenantId, now);
            await SanSangAsync(items, events, uowManager, guid, tenantId, now);
            await BanGiaoAsync(items, events, templates, sender, uowManager, guid, bus, metrics, Logger, tenantId, now, leaseSeconds);

            var backlog = (await items.GetListAsync(x => x.TenantId == tenantId && x.Status == NotificationQueueStatus.ChoGui)).Count;
            metrics.SetNotificationQueueBacklog(backlog);
        }
    }

    private static async Task KhoiPhucLeaseAsync(
        IRepository<NotificationQueueItem, Guid> items, IRepository<NotificationQueueEvent, Guid> events,
        IUnitOfWorkManager uowManager, IGuidGenerator guid, eCommerceMetrics metrics,
        ILogger logger, Guid? tenantId, DateTime now)
    {
        var ids = (await items.GetListAsync(x =>
                x.TenantId == tenantId
                && x.Status == NotificationQueueStatus.DangBanGiao
                && x.DispatchLeaseExpiresAtUtc != null
                && x.DispatchLeaseExpiresAtUtc < now))
            .Select(x => x.Id).ToList();

        foreach (var id in ids)
        {
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);
            var muc = await items.FindAsync(id);
            if (muc is null || muc.Status != NotificationQueueStatus.DangBanGiao)
            {
                await uow.CompleteAsync();
                continue;
            }

            var transition = muc.KhoiPhucLease(now);
            await events.InsertAsync(
                NotificationQueueEvent.TaoMoi(guid.Create(), muc.Id, transition, muc.CorrelationId, null, now, tenantId: muc.TenantId),
                autoSave: false);
            await items.UpdateAsync(muc, autoSave: false);
            await uow.CompleteAsync();

            metrics.NotificationDispatchLeaseExpired.Add(
                1, new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "notification"));
            logger.LogWarning("[NOT-DISPATCH] Khôi phục lease mục {ItemId} — KHÔNG tăng AttemptCount.", muc.Id);
        }
    }

    private static async Task HetHanAsync(
        IRepository<NotificationQueueItem, Guid> items, IRepository<NotificationQueueEvent, Guid> events,
        IUnitOfWorkManager uowManager, IGuidGenerator guid, eCommerceMetrics metrics,
        Guid? tenantId, DateTime now)
    {
        var ids = (await items.GetListAsync(x =>
                x.TenantId == tenantId
                && x.Status == NotificationQueueStatus.ChoGui
                && x.ExpiresAtUtc != null
                && x.ExpiresAtUtc < now))
            .Select(x => x.Id).ToList();

        foreach (var id in ids)
        {
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);
            var muc = await items.FindAsync(id);
            if (muc is null || muc.Status != NotificationQueueStatus.ChoGui)
            {
                await uow.CompleteAsync();
                continue;
            }

            var transition = muc.HetHanTuChoGui(now);
            await events.InsertAsync(
                NotificationQueueEvent.TaoMoi(guid.Create(), muc.Id, transition, muc.CorrelationId, null, now, tenantId: muc.TenantId),
                autoSave: false);
            await items.UpdateAsync(muc, autoSave: false);
            await uow.CompleteAsync();

            metrics.NotificationQueueFailed.Add(
                1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, NotificationQueueAppender.KenhNhan(muc.ChannelKind)),
                new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, "het_han"));
        }
    }

    /// <summary>Pha (C1): `Moi → ChoGui` bằng `UPDATE … WHERE Status=Moi` (0 dòng ⇒ bỏ qua, không lỗi).</summary>
    private static async Task SanSangAsync(
        IRepository<NotificationQueueItem, Guid> items, IRepository<NotificationQueueEvent, Guid> events,
        IUnitOfWorkManager uowManager, IGuidGenerator guid, Guid? tenantId, DateTime now)
    {
        var ids = (await items.GetListAsync(x =>
                x.TenantId == tenantId
                && x.Status == NotificationQueueStatus.Moi
                && x.ScheduledAtUtc <= now))
            .Select(x => x.Id).ToList();

        foreach (var id in ids)
        {
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);
            var query = await items.GetQueryableAsync();

            // Chỉ chuyển khi mục KHÔNG thuộc lô, hoặc lô đã duyệt (kiểm lô ở tầng gọi; mục trong lô
            // `ChoDuyet` không ở `Moi` nên không lọt đường này).
            var soDong = await query
                .Where(x => x.Id == id && x.Status == NotificationQueueStatus.Moi)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationQueueStatus.ChoGui));

            if (soDong == 1)
            {
                var muc = await items.GetAsync(id);
                var transition = new NotificationQueueTransition(
                    NotificationQueueEventType.ReadyToDispatch,
                    NotificationQueueStatus.Moi,
                    NotificationQueueStatus.ChoGui);
                await events.InsertAsync(
                    NotificationQueueEvent.TaoMoi(guid.Create(), id, transition, muc.CorrelationId, null, now, tenantId: muc.TenantId),
                    autoSave: false);
            }

            await uow.CompleteAsync();
        }
    }

    /// <summary>Pha (C2): giành bàn giao + gọi sender NGOÀI transaction + ghi kết quả (§7.2).</summary>
    private static async Task BanGiaoAsync(
        IRepository<NotificationQueueItem, Guid> items, IRepository<NotificationQueueEvent, Guid> events,
        IRepository<NotificationTemplate, Guid> templates, INotificationSender sender,
        IUnitOfWorkManager uowManager, IGuidGenerator guid, IDistributedEventBus bus,
        eCommerceMetrics metrics, ILogger logger, Guid? tenantId, DateTime now, int leaseSeconds)
    {
        var ids = (await items.GetListAsync(x =>
                x.TenantId == tenantId
                && x.Status == NotificationQueueStatus.ChoGui
                && x.ScheduledAtUtc <= now))
            .Select(x => x.Id).ToList();

        foreach (var id in ids)
        {
            // (C2a) Giành bàn giao — UPDATE có điều kiện + event `Dispatching` CÙNG UoW.
            var daGianh = false;
            using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                var query = await items.GetQueryableAsync();
                var soDong = await query
                    .Where(x => x.Id == id && x.Status == NotificationQueueStatus.ChoGui)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, NotificationQueueStatus.DangBanGiao)
                        .SetProperty(x => x.DispatchLeaseExpiresAtUtc, now.AddSeconds(leaseSeconds)));

                if (soDong == 1)
                {
                    var muc = await items.GetAsync(id);
                    var transition = new NotificationQueueTransition(
                        NotificationQueueEventType.Dispatching,
                        NotificationQueueStatus.ChoGui,
                        NotificationQueueStatus.DangBanGiao);
                    await events.InsertAsync(
                        NotificationQueueEvent.TaoMoi(guid.Create(), id, transition, muc.CorrelationId, null, now, tenantId: muc.TenantId),
                        autoSave: false);
                    daGianh = true;
                }

                await uow.CompleteAsync();
            }

            if (!daGianh)
            {
                continue; // 0 dòng ⇒ luồng khác đã lấy — bỏ qua, KHÔNG lỗi.
            }

            // (C2b) Đọc dữ liệu gửi rồi gọi sender NGOÀI transaction.
            NotificationSendRequest yeuCau;
            using (var uowDoc = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                var muc = await items.FindAsync(id);
                if (muc is null)
                {
                    await uowDoc.CompleteAsync();
                    continue;
                }

                var template = await templates.FindAsync(muc.TemplateId);
                yeuCau = new NotificationSendRequest(
                    muc.TenantId, muc.Id, muc.IdempotencyKey, muc.ChannelKind,
                    muc.RecipientEncrypted, muc.RenderedSubject ?? string.Empty, muc.RenderedBodyMasked ?? string.Empty,
                    muc.Priority, muc.CorrelationId);
                _ = template;
                await uowDoc.CompleteAsync();
            }

            NotificationSendResult ketQua;
            try
            {
                ketQua = await sender.SendAsync(yeuCau);
            }
            catch (Exception ex)
            {
                // Sender fail-closed (chưa có kênh) ⇒ để lease đưa về `ChoGui`; KHÔNG đánh dấu DaBanGiao.
                logger.LogWarning(ex, "[NOT-DISPATCH] Sender ném cho mục {ItemId} — giữ DangBanGiao cho lease khôi phục.", id);
                continue;
            }

            await GhiKetQuaBanGiaoAsync(items, events, uowManager, guid, bus, metrics, logger, id, yeuCau, ketQua, now);
        }
    }

    private static async Task GhiKetQuaBanGiaoAsync(
        IRepository<NotificationQueueItem, Guid> items, IRepository<NotificationQueueEvent, Guid> events,
        IUnitOfWorkManager uowManager, IGuidGenerator guid, IDistributedEventBus bus,
        eCommerceMetrics metrics, ILogger logger, Guid id, NotificationSendRequest yeuCau,
        NotificationSendResult ketQua, DateTime now)
    {
        using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);
        var muc = await items.FindAsync(id);
        if (muc is null || muc.Status != NotificationQueueStatus.DangBanGiao)
        {
            await uow.CompleteAsync();
            return;
        }

        NotificationQueueTransition transition;

        if (ketQua.Accepted || ketQua.AlreadySent)
        {
            transition = muc.DanhDauDaBanGiao(now);
            await bus.PublishAsync(
                new Events.NotificationHandedOffEto(
                    muc.TenantId, muc.Id, muc.ChannelKind, string.Empty, muc.IdempotencyKey,
                    muc.ScheduledAtUtc, now, muc.Priority, muc.CorrelationId),
                onUnitOfWorkComplete: false, useOutbox: true);
            metrics.NotificationDispatched.Add(
                1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, NotificationQueueAppender.KenhNhan(muc.ChannelKind)),
                new KeyValuePair<string, object?>(eCommerceMetricTags.Result, ketQua.AlreadySent ? "already_sent" : "accepted"));
        }
        else if (ketQua.IsTransient)
        {
            if (muc.ExpiresAtUtc is null || now <= muc.ExpiresAtUtc)
            {
                transition = muc.HoanLaiChoGui(now, ketQua.ErrorCode ?? "loi-tam-thoi");
            }
            else
            {
                transition = muc.HetHanTuDangBanGiao(now);
            }

            metrics.NotificationQueueFailed.Add(
                1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, NotificationQueueAppender.KenhNhan(muc.ChannelKind)),
                new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, ketQua.ErrorCode ?? "loi-tam-thoi"));
        }
        else
        {
            transition = muc.DanhDauLoi(ketQua.ErrorCode ?? "loi-vinh-vien", ketQua.ErrorCode ?? "loi-vinh-vien");
            await bus.PublishAsync(
                new Events.NotificationQueueFailedEto(
                    muc.TenantId, muc.Id, muc.ChannelKind, string.Empty, muc.IdempotencyKey,
                    muc.ScheduledAtUtc, muc.LastErrorCode, muc.LastReasonMasked, muc.CorrelationId),
                onUnitOfWorkComplete: false, useOutbox: true);
            metrics.NotificationQueueFailed.Add(
                1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, NotificationQueueAppender.KenhNhan(muc.ChannelKind)),
                new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, muc.LastErrorCode ?? "loi-vinh-vien"));
            logger.LogError("[NOT-DISPATCH] Mục {ItemId} lỗi vĩnh viễn khi bàn giao: {Code}", id, ketQua.ErrorCode);
        }

        await events.InsertAsync(
            NotificationQueueEvent.TaoMoi(guid.Create(), muc.Id, transition, muc.CorrelationId, null, now,
                reasonMasked: muc.LastReasonMasked, errorCode: muc.LastErrorCode, tenantId: muc.TenantId),
            autoSave: false);
        await items.UpdateAsync(muc, autoSave: false);
        await uow.CompleteAsync();

        _ = yeuCau;
    }
}
