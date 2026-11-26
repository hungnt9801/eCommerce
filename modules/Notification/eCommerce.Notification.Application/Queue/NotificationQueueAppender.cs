using eCommerce.Kernel.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Notification;

/// <summary>
/// Cổng xếp hàng — hiện thực biên vào #2 (API thủ công) và #3 (job dùng lại khoá).
/// Chống trùng **5 lớp** (`D-197`): pre-check + UNIQUE `(TenantId, IdempotencyKey)` + partial UNIQUE
/// (khử trùng cửa sổ). Trùng khoá cùng payload ⇒ `AlreadyQueued`; payload khác ⇒ **NÉM** `0010`.
/// </summary>
public class NotificationQueueAppender : INotificationQueueAppender, ITransientDependency
{
    private readonly IRepository<NotificationQueueItem, Guid> _items;
    private readonly IRepository<NotificationQueueEvent, Guid> _events;
    private readonly IRepository<NotificationTemplate, Guid> _templates;
    private readonly IRepository<NotificationTemplateVersion, Guid> _versions;
    private readonly INotificationRenderer _renderer;
    private readonly INotificationMoneyFormatter _money;
    private readonly IStringEncryptionService _encryption;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _uow;
    private readonly IDistributedEventBus _bus;
    private readonly eCommerceMetrics _metrics;
    private readonly ILogger<NotificationQueueAppender> _logger;

    public NotificationQueueAppender(
        IRepository<NotificationQueueItem, Guid> items,
        IRepository<NotificationQueueEvent, Guid> events,
        IRepository<NotificationTemplate, Guid> templates,
        IRepository<NotificationTemplateVersion, Guid> versions,
        INotificationRenderer renderer,
        INotificationMoneyFormatter money,
        IStringEncryptionService encryption,
        IGuidGenerator guid,
        IClock clock,
        IUnitOfWorkManager uow,
        IDistributedEventBus bus,
        eCommerceMetrics metrics,
        ILogger<NotificationQueueAppender> logger)
    {
        _items = items;
        _events = events;
        _templates = templates;
        _versions = versions;
        _renderer = renderer;
        _money = money;
        _encryption = encryption;
        _guid = guid;
        _clock = clock;
        _uow = uow;
        _bus = bus;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<EnqueueResult> EnqueueAsync(EnqueueRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ① Khoá idempotency bắt buộc — kiểm TRƯỚC mọi ghi (biên #2).
        var key = IdempotencyKey.Tao(request.IdempotencyKey).Value;
        var now = _clock.Now.ToUniversalTime();

        // ② Resolve template + version ĐÃ GHIM.
        var template = await _templates.FirstOrDefaultAsync(
            x => x.Code == request.TemplateCode
                 && x.ChannelKind == request.ChannelKind
                 && x.Locale == request.Locale
                 && x.Status == NotificationTemplateStatus.Published,
            ct);

        if (template?.CurrentPublishedVersionId is not { } versionId)
        {
            throw new BusinessException(NotificationErrorCodes.TemplateChuaSanSang)
                .WithData("code", request.TemplateCode)
                .WithData("channel", request.ChannelKind.ToString())
                .WithData("locale", request.Locale);
        }

        var version = await _versions.GetAsync(versionId, cancellationToken: ct);

        if (version.BodyFormat != NotificationBodyFormat.Text)
        {
            // ③ GĐ3 chỉ Text — NÉM 0020 TRƯỚC mọi ghi (`D-199`).
            throw new BusinessException(NotificationErrorCodes.BodyFormatKhongHoTro)
                .WithData("bodyFormat", (int)version.BodyFormat);
        }

        // ④ Render dry-run — hàm thuần; lỗi 0013/0015 ném từ đây TRƯỚC khi ghi.
        RenderResult rendered;
        try
        {
            rendered = _renderer.Render(version.ToContent(), request.Data, request.Locale, _money);
        }
        catch (BusinessException ex)
        {
            _metrics.NotificationRenderFailed.Add(
                1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, ex.Code ?? "unknown"));
            _logger.LogWarning("Render mẫu tin {Code} thất bại: {CodeLoi}", request.TemplateCode, ex.Code);
            throw;
        }

        // ⑤ Người nhận: mã hoá + băm MỘT cửa; plaintext KHÔNG rời bộ nhớ.
        //    🔒 `D-269`: mask **một lần** sau render rồi dùng **cùng** giá trị đã mask cho fingerprint
        //    (fingerprint trên giá trị **đã lưu**), `TaoMuc` và `GhiKhudupAsync` — không có cửa ghi thứ hai.
        var recipientHash = NotificationRecipient.TinhHash(request.Recipient);
        var recipientEncrypted = _encryption.Encrypt(request.Recipient) ?? string.Empty;
        var subjectMasked = NotificationMasker.CheBiMat(rendered.Subject);
        var bodyMasked = NotificationMasker.CheBiMat(rendered.Body);
        var fingerprint = ContentFingerprint.TinhMuc(version.Id, recipientHash, subjectMasked, bodyMasked).Sha256;

        // ⑥ Trùng khoá: cùng fingerprint ⇒ AlreadyQueued; khác ⇒ 0010 (TC-03).
        var existing = await _items.FirstOrDefaultAsync(
            x => x.IdempotencyKey == key && x.TenantId == request.TenantId, ct);

        if (existing is not null)
        {
            return SoSanhTrungKhoa(existing, fingerprint);
        }

        // ⑦ Khử trùng cửa sổ (chỉ Marketing): pre-check theo partial UNIQUE index (ADR-12).
        DateTime? bucketStart = template.CoalesceWindowSeconds > 0
            ? LamTronCuaSo(now, template.CoalesceWindowSeconds)
            : null;

        if (bucketStart is { } bucket)
        {
            var trung = await _items.FirstOrDefaultAsync(
                x => x.TenantId == request.TenantId
                     && x.RecipientHash == recipientHash
                     && x.ChannelKind == request.ChannelKind
                     && x.ContentFingerprint == fingerprint
                     && x.CoalesceBucketStartUtc == bucket,
                ct);

            if (trung is not null)
            {
                return await GhiKhudupAsync(request, key, template, version, recipientEncrypted, recipientHash, subjectMasked, bodyMasked, fingerprint, now, ct);
            }
        }

        // ⑧ Ghi mục + event + outbox TRONG CÙNG một UoW (BR-007). UoW riêng `requiresNew` để có thể
        //    phục hồi khi DB chặn trùng khoá (23505) mà không làm hỏng UoW của bên gọi.
        try
        {
            Guid itemId;
            string status;

            using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
            {
                var item = TaoMuc(request, key, template, version, recipientEncrypted, recipientHash, subjectMasked, bodyMasked, fingerprint, bucketStart, now);
                await _items.InsertAsync(item, autoSave: false, cancellationToken: ct);

                var transition = item.SuKienTaoMoi();
                await _events.InsertAsync(
                    NotificationQueueEvent.TaoMoi(
                        _guid.Create(), item.Id, transition, request.CorrelationId, actorUserId: null, now,
                        tenantId: request.TenantId),
                    autoSave: false, cancellationToken: ct);

                // 🚨 Hàng 2 (§3 SRS): mục thuộc lô ⇒ `Moi → ChoDuyet` NGAY trong CÙNG UoW, ghi **một**
                // event `QueuedForApproval` (sau `Enqueued`). Nhờ vậy `ApproveBatchAsync` (lọc `ChoDuyet`)
                // thấy được mục của lô — thiếu bước này thì duyệt lô vô tác dụng.
                if (request.BatchId is { } batchId)
                {
                    var transitionDuyet = item.DanhDauChoDuyet(batchId);
                    await _events.InsertAsync(
                        NotificationQueueEvent.TaoMoi(
                            _guid.Create(), item.Id, transitionDuyet, request.CorrelationId, actorUserId: null, now,
                            tenantId: request.TenantId),
                        autoSave: false, cancellationToken: ct);
                }

                await _bus.PublishAsync(
                    new Events.NotificationEnqueuedEto(
                        request.TenantId, item.Id, request.ChannelKind, template.Code, key,
                        item.ScheduledAtUtc, request.CorrelationId),
                    onUnitOfWorkComplete: false, useOutbox: true);

                await uow.CompleteAsync(ct);

                itemId = item.Id;
                status = item.Status.ToString();
            }

            _metrics.NotificationQueued.Add(
                1, new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, KenhNhan(request.ChannelKind)));

            return new EnqueueResult(itemId, false, status, null);
        }
        catch (DbUpdateException ex) when (LaTrungKhoa(ex))
        {
            // ⑨ Đua hai luồng: bên thua bắt 23505 ⇒ UoW mới `requiresNew` đọc lại; KHÔNG dùng lại
            //    transaction đã abort (PostgreSQL trả `25P02`).
            return await XuLyTrungKhoaAsync(request.TenantId, key, fingerprint, ct);
        }
    }

    private NotificationQueueItem TaoMuc(
        EnqueueRequest request,
        string key,
        NotificationTemplate template,
        NotificationTemplateVersion version,
        string recipientEncrypted,
        string recipientHash,
        string subjectMasked,
        string bodyMasked,
        string fingerprint,
        DateTime? bucketStart,
        DateTime now)
        => NotificationQueueItem.TaoMoi(
            _guid.Create(),
            key,
            request.TenantId,
            rootItemId: null,
            request.BatchId,
            request.SourceEventType,
            request.SourceAggregateId,
            request.ChannelKind,
            template.Id,
            version.Id,
            recipientEncrypted,
            recipientHash,
            subjectMasked,
            bodyMasked,
            fingerprint,
            bucketStart,
            request.Priority,
            request.ScheduledAtUtc,
            request.ExpiresAtUtc,
            request.CorrelationId);

    private async Task<EnqueueResult> GhiKhudupAsync(
        EnqueueRequest request, string key, NotificationTemplate template, NotificationTemplateVersion version,
        string recipientEncrypted, string recipientHash, string subjectMasked, string bodyMasked, string fingerprint,
        DateTime now, CancellationToken ct)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

        var item = TaoMuc(request, key, template, version, recipientEncrypted, recipientHash, subjectMasked, bodyMasked, fingerprint, bucketStart: null, now);
        var transition = item.GhiNhanBiKhudup("cua-so-khu-trung");

        await _items.InsertAsync(item, autoSave: false, cancellationToken: ct);
        await _events.InsertAsync(
            NotificationQueueEvent.TaoMoi(
                _guid.Create(), item.Id, transition, request.CorrelationId, actorUserId: null, now,
                reasonMasked: "cua-so-khu-trung", tenantId: request.TenantId),
            autoSave: false, cancellationToken: ct);

        await uow.CompleteAsync(ct);

        _metrics.NotificationWindowSuppressed.Add(
            1, new KeyValuePair<string, object?>(eCommerceMetricTags.Channel, KenhNhan(request.ChannelKind)));

        return new EnqueueResult(item.Id, false, item.Status.ToString(), NotificationErrorCodes.BiKhudupCuaSo);
    }

    private async Task<EnqueueResult> XuLyTrungKhoaAsync(Guid? tenantId, string key, string fingerprint, CancellationToken ct)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var existing = await _items.FirstOrDefaultAsync(x => x.IdempotencyKey == key && x.TenantId == tenantId, ct);
        await uow.CompleteAsync(ct);

        if (existing is not null)
        {
            return SoSanhTrungKhoa(existing, fingerprint);
        }

        // Không thấy bản ghi (hiếm: bên thắng chưa commit) ⇒ coi là xung đột khoá.
        throw new BusinessException(NotificationErrorCodes.KhoaXungDot).WithData("lyDo", "trung-khoa");
    }

    private static EnqueueResult SoSanhTrungKhoa(NotificationQueueItem existing, string fingerprint)
    {
        if (string.Equals(existing.ContentFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return new EnqueueResult(existing.Id, true, existing.Status.ToString(), null);
        }

        throw new BusinessException(NotificationErrorCodes.KhoaXungDot)
            .WithData("lyDo", "payload-khac")
            .WithData("idempotencyKey", existing.IdempotencyKey);
    }

    private static DateTime LamTronCuaSo(DateTime now, int cuaSoGiay)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var giay = (long)(now.ToUniversalTime() - epoch).TotalSeconds;
        var bucket = giay - (giay % cuaSoGiay);
        return epoch.AddSeconds(bucket);
    }

    internal static string KenhNhan(ChannelKind channel) => channel switch
    {
        ChannelKind.Email => "email",
        ChannelKind.Sms => "sms",
        ChannelKind.ZaloOa => "zalo_oa",
        _ => "other"
    };

    /// <summary>Phát hiện vi phạm unique của PostgreSQL (`23505`) mà KHÔNG phụ thuộc trực tiếp Npgsql.</summary>
    private static bool LaTrungKhoa(Exception ex)
    {
        for (var e = (Exception?)ex; e is not null; e = e.InnerException)
        {
            var type = e.GetType();
            if (type.Name == "PostgresException"
                && type.GetProperty("SqlState")?.GetValue(e) as string == "23505")
            {
                return true;
            }
        }

        return false;
    }
}
