using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace eCommerce.Notification;

/// <summary>Hàng đợi: xếp thủ công (biên #2), xem, sổ sự kiện, phát lại (biên #4), huỷ.</summary>
[Authorize]
public class NotificationQueueAppService : ApplicationService, INotificationQueueAppService
{
    private readonly INotificationQueueAppender _appender;
    private readonly IRepository<NotificationQueueItem, Guid> _items;
    private readonly IRepository<NotificationQueueEvent, Guid> _events;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAbpDistributedLock _lock;

    public NotificationQueueAppService(
        INotificationQueueAppender appender,
        IRepository<NotificationQueueItem, Guid> items,
        IRepository<NotificationQueueEvent, Guid> events,
        IGuidGenerator guid,
        IClock clock,
        ICurrentUser currentUser,
        IAbpDistributedLock lockService)
    {
        _appender = appender;
        _items = items;
        _events = events;
        _guid = guid;
        _clock = clock;
        _currentUser = currentUser;
        _lock = lockService;
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.QueueCreate)]
    public async Task<EnqueueResult> EnqueueAsync(SendManualRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // 🚨 Thiếu `IdempotencyKey` ⇒ appender NÉM 0010 TRƯỚC mọi ghi (biên #2).
        var request = new EnqueueRequest(
            CurrentTenant.Id,
            input.IdempotencyKey ?? string.Empty,
            input.TemplateCode,
            input.ChannelKind,
            input.Locale,
            input.Recipient,
            input.Data,
            input.Priority,
            input.ScheduledAtUtc == default ? _clock.Now.ToUniversalTime() : input.ScheduledAtUtc,
            input.ExpiresAtUtc,
            BatchId: input.BatchId,
            SourceEventType: "Manual",
            input.SourceAggregateId,
            input.CorrelationId);

        return await _appender.EnqueueAsync(request);
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.QueueView)]
    public async Task<PagedResultDto<NotificationQueueItemDto>> GetQueueAsync(GetQueueRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var query = await _items.GetQueryableAsync();

        if (input.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (input.ChannelKind is { } channel)
        {
            query = query.Where(x => x.ChannelKind == channel);
        }

        if (input.TuNgayUtc is { } tu)
        {
            query = query.Where(x => x.ScheduledAtUtc >= tu);
        }

        if (input.DenNgayUtc is { } den)
        {
            query = query.Where(x => x.ScheduledAtUtc <= den);
        }

        var total = await _items.AsyncExecuter.CountAsync(query);
        var danhSach = await _items.AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.ScheduledAtUtc).Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<NotificationQueueItemDto>(
            total, danhSach.Select(NotificationMappings.ToDto).ToList());
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.QueueView)]
    public async Task<List<NotificationQueueEventDto>> GetEventsAsync(Guid id)
    {
        var list = await _events.GetListAsync(x => x.QueueItemId == id);
        return list.OrderBy(x => x.OccurredAtUtc).Select(NotificationMappings.ToDto).ToList();
    }

    /// <summary>Phát lại mục `Loi` — **tạo mục MỚI** với khoá `{gốc}:replay-{n}`, mục gốc ở lại `Loi`.</summary>
    [Authorize(eCommerceConsts.Permissions.Notifications.QueueRetry)]
    public async Task<NotificationQueueItemDto> RetryAsync(Guid id)
    {
        await using var handle = await _lock.TryAcquireAsync(
            NotificationLockKeys.QueueReplay(CurrentTenant.Id), TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "notification:queue-replay");
        }

        var goc = await _items.FindAsync(id)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "queue-item");

        var now = _clock.Now.ToUniversalTime();
        var transitionGoc = goc.DanhDauPhatLai(_currentUser.Id);

        var soLan = (await _items.GetListAsync(x => x.RootItemId == goc.Id)).Count;
        var khoaMoi = $"{goc.IdempotencyKey}:replay-{soLan + 1}";

        var moi = NotificationQueueItem.TaoMoi(
            _guid.Create(),
            khoaMoi,
            goc.TenantId,
            goc.Id,
            goc.BatchId,
            goc.SourceEventType,
            goc.SourceAggregateId,
            goc.ChannelKind,
            goc.TemplateId,
            goc.TemplateVersionId,
            goc.RecipientEncrypted,
            goc.RecipientHash,
            goc.RenderedSubject,
            goc.RenderedBodyMasked ?? string.Empty,
            goc.ContentFingerprint,
            coalesceBucketStartUtc: null,
            goc.Priority,
            now,
            goc.ExpiresAtUtc,
            goc.CorrelationId);

        await _items.InsertAsync(moi, autoSave: false);

        await _events.InsertAsync(
            NotificationQueueEvent.TaoMoi(
                _guid.Create(), moi.Id, moi.SuKienTaoMoi(), goc.CorrelationId, _currentUser.Id, now,
                tenantId: goc.TenantId),
            autoSave: false);

        await _events.InsertAsync(
            NotificationQueueEvent.TaoMoi(
                _guid.Create(), goc.Id, transitionGoc, goc.CorrelationId, _currentUser.Id, now,
                reasonMasked: "phat-lai", tenantId: goc.TenantId),
            autoSave: false);

        await _items.UpdateAsync(goc, autoSave: true);

        return moi.ToDto();
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.QueueCancel)]
    public async Task<NotificationQueueItemDto> CancelAsync(Guid id, CancelQueueItemRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var item = await _items.FindAsync(id)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "queue-item");

        var now = _clock.Now.ToUniversalTime();
        // 🔒 `D-269`: lý do người dùng gõ có thể chứa SĐT/email ⇒ mask MỘT lần, dùng chung cho mục + event.
        var lyDoMasked = NotificationMasker.CheBiMat(input.LyDo);
        var transition = item.Huy(lyDoMasked, _currentUser.Id);

        await _events.InsertAsync(
            NotificationQueueEvent.TaoMoi(
                _guid.Create(), item.Id, transition, item.CorrelationId, _currentUser.Id, now,
                reasonMasked: lyDoMasked, tenantId: item.TenantId),
            autoSave: false);

        await _items.UpdateAsync(item, autoSave: true);

        return item.ToDto();
    }
}
