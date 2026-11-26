using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Notification;

/// <summary>Một cạnh chuyển trạng thái — mang đủ dữ liệu để ghi **đúng một** `NotificationQueueEvent`.</summary>
public sealed record NotificationQueueTransition(
    NotificationQueueEventType EventType,
    NotificationQueueStatus? FromStatus,
    NotificationQueueStatus ToStatus);

/// <summary>Lô gửi hàng loạt (`NotificationBatches`, SRS §5.3).</summary>
public class NotificationBatch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Code { get; private set; } = default!;
    public Guid TemplateId { get; private set; }
    public Guid TemplateVersionId { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public NotificationBatchStatus Status { get; private set; }
    public int TotalItems { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = default!;

    protected NotificationBatch()
    {
    }

    public static NotificationBatch TaoMoi(
        Guid id,
        string code,
        Guid templateId,
        Guid templateVersionId,
        string idempotencyKey,
        int totalItems,
        Guid? createdByUserId,
        string correlationId,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > NotificationConsts.CodeToiDa)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "Code");
        }

        if (totalItems < 0)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "TotalItems");
        }

        return new NotificationBatch
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim(),
            TemplateId = templateId,
            TemplateVersionId = templateVersionId,
            IdempotencyKey = global::eCommerce.Notification.IdempotencyKey.Tao(idempotencyKey).Value,
            Status = NotificationBatchStatus.ChoDuyet,
            TotalItems = totalItems,
            CreatedByUserId = createdByUserId,
            CorrelationId = correlationId ?? string.Empty
        };
    }

    /// <summary>Duyệt lô. 🚨 Tách nhiệm vụ: người tạo **không** được tự duyệt (`TC-25`).</summary>
    public void Duyet(Guid userId, DateTime now)
    {
        if (Status != NotificationBatchStatus.ChoDuyet)
        {
            throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("from", Status.ToString())
                .WithData("to", NotificationBatchStatus.DaDuyet.ToString());
        }

        if (CreatedByUserId is { } nguoiTao && nguoiTao == userId)
        {
            // Tách nhiệm vụ — người tạo KHÔNG được tự duyệt (gửi hàng loạt ra người thật).
            throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("lyDo", "tu-duyet");
        }

        Status = NotificationBatchStatus.DaDuyet;
        ApprovedByUserId = userId;
        ApprovedAtUtc = now;
    }

    public void Huy(string lyDo)
    {
        _ = lyDo;

        if (Status is NotificationBatchStatus.DaBanGiao or NotificationBatchStatus.DaHuy)
        {
            throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("from", Status.ToString())
                .WithData("to", NotificationBatchStatus.DaHuy.ToString());
        }

        Status = NotificationBatchStatus.DaHuy;
    }
}

/// <summary>
/// Mục hàng đợi — trái tim chống gửi trùng (`NotificationQueueItems`, SRS §5.4).
/// Mỗi cạnh của máy trạng thái 15 hàng là **một hàm**, guard sai ⇒ **NÉM** `0011`.
/// </summary>
public class NotificationQueueItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public Guid? RootItemId { get; private set; }
    public Guid? BatchId { get; private set; }
    public string? SourceEventType { get; private set; }
    public string? SourceAggregateId { get; private set; }
    public ChannelKind ChannelKind { get; private set; }
    public Guid TemplateId { get; private set; }
    public Guid TemplateVersionId { get; private set; }
    public string RecipientEncrypted { get; private set; } = default!;
    public string RecipientHash { get; private set; } = default!;
    public string? RenderedSubject { get; private set; }
    public string? RenderedBodyMasked { get; private set; }
    public string ContentFingerprint { get; private set; } = default!;
    public DateTime? CoalesceBucketStartUtc { get; private set; }
    public int Priority { get; private set; }
    public NotificationQueueStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? DispatchLeaseExpiresAtUtc { get; private set; }
    public DateTime? DispatchedAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastReasonMasked { get; private set; }
    public string CorrelationId { get; private set; } = default!;

    /// <summary>Concurrency token (ánh xạ `xmin` của PostgreSQL).</summary>
    public uint RowVersion { get; private set; }

    protected NotificationQueueItem()
    {
    }

    public static NotificationQueueItem TaoMoi(
        Guid id,
        string idempotencyKey,
        Guid? tenantId,
        Guid? rootItemId,
        Guid? batchId,
        string? sourceEventType,
        string? sourceAggregateId,
        ChannelKind channelKind,
        Guid templateId,
        Guid templateVersionId,
        string recipientEncrypted,
        string recipientHash,
        string? renderedSubject,
        string renderedBodyMasked,
        string contentFingerprint,
        DateTime? coalesceBucketStartUtc,
        int priority,
        DateTime scheduledAtUtc,
        DateTime? expiresAtUtc,
        string correlationId)
    {
        if (priority is < NotificationPriorities.Low or > NotificationPriorities.Critical)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "Priority");
        }

        if (expiresAtUtc is { } han && han <= scheduledAtUtc)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc)
                .WithData("lyDo", "ExpiresAtUtc phải sau ScheduledAtUtc");
        }

        if (!NotificationHash.LaHex64(recipientHash) || !NotificationHash.LaHex64(contentFingerprint))
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("lyDo", "hash-khong-hop-le");
        }

        return new NotificationQueueItem
        {
            Id = id,
            TenantId = tenantId,
            IdempotencyKey = global::eCommerce.Notification.IdempotencyKey.Tao(idempotencyKey).Value,
            RootItemId = rootItemId,
            BatchId = batchId,
            SourceEventType = sourceEventType,
            SourceAggregateId = sourceAggregateId,
            ChannelKind = channelKind,
            TemplateId = templateId,
            TemplateVersionId = templateVersionId,
            RecipientEncrypted = recipientEncrypted,
            RecipientHash = recipientHash,
            // 🔒 MỘT CỬA (`D-269`): giá trị thô không bao giờ vào cột đã-che.
            RenderedSubject = renderedSubject is null ? null : NotificationMasker.CheBiMat(renderedSubject),
            RenderedBodyMasked = NotificationMasker.CheBiMat(renderedBodyMasked),
            ContentFingerprint = contentFingerprint,
            CoalesceBucketStartUtc = coalesceBucketStartUtc,
            Priority = priority,
            Status = NotificationQueueStatus.Moi,
            AttemptCount = 0,
            ScheduledAtUtc = scheduledAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            CorrelationId = correlationId ?? string.Empty
        };
    }

    // ── Máy trạng thái 15 hàng (§3 SRS) ─────────────────────────────────

    /// <summary>Hàng 1: mục mới ⇒ `Enqueued` (FromStatus = null).</summary>
    public NotificationQueueTransition SuKienTaoMoi()
        => new(NotificationQueueEventType.Enqueued, null, NotificationQueueStatus.Moi);

    /// <summary>Hàng 2: `Moi → ChoDuyet` (thuộc lô).</summary>
    public NotificationQueueTransition DanhDauChoDuyet(Guid batchId)
    {
        if (Status != NotificationQueueStatus.Moi)
        {
            Nem(Status, NotificationQueueStatus.ChoDuyet);
        }

        BatchId = batchId;
        return ChuyenSang(NotificationQueueStatus.ChoDuyet, NotificationQueueEventType.QueuedForApproval);
    }

    /// <summary>Hàng 3: `Moi → ChoGui` (đường không đua; đường nóng dùng `UPDATE … WHERE`).</summary>
    public NotificationQueueTransition DanhDauSanSang(DateTime now)
    {
        if (Status != NotificationQueueStatus.Moi || ScheduledAtUtc > now)
        {
            Nem(Status, NotificationQueueStatus.ChoGui);
        }

        return ChuyenSang(NotificationQueueStatus.ChoGui, NotificationQueueEventType.ReadyToDispatch);
    }

    /// <summary>Hàng 4: `ChoDuyet → ChoGui` (lô được duyệt).</summary>
    public NotificationQueueTransition DuyetLo(Guid userId)
    {
        _ = userId;

        if (Status != NotificationQueueStatus.ChoDuyet)
        {
            Nem(Status, NotificationQueueStatus.ChoGui);
        }

        return ChuyenSang(NotificationQueueStatus.ChoGui, NotificationQueueEventType.BatchApproved);
    }

    /// <summary>Hàng 5: `ChoGui → DangBanGiao` — chỉ dùng khi **đã thắng** UPDATE có điều kiện.</summary>
    public NotificationQueueTransition GianhBanGiao(DateTime leaseExpiresAtUtc)
    {
        if (Status != NotificationQueueStatus.ChoGui)
        {
            Nem(Status, NotificationQueueStatus.DangBanGiao);
        }

        DispatchLeaseExpiresAtUtc = leaseExpiresAtUtc;
        return ChuyenSang(NotificationQueueStatus.DangBanGiao, NotificationQueueEventType.Dispatching);
    }

    /// <summary>Hàng 6: `DangBanGiao → DaBanGiao` (sender nhận / `AlreadySent`).</summary>
    public NotificationQueueTransition DanhDauDaBanGiao(DateTime now)
    {
        if (LaHapThu(Status))
        {
            throw new BusinessException(NotificationErrorCodes.DaBanGiaoRoi).WithData("status", Status.ToString());
        }

        if (Status != NotificationQueueStatus.DangBanGiao)
        {
            Nem(Status, NotificationQueueStatus.DaBanGiao);
        }

        DispatchedAtUtc = now;
        DispatchLeaseExpiresAtUtc = null;
        return ChuyenSang(NotificationQueueStatus.DaBanGiao, NotificationQueueEventType.HandedOff);
    }

    /// <summary>Hàng 7: `DangBanGiao → ChoGui` khi lease hết hạn — **KHÔNG** tăng `AttemptCount`.</summary>
    public NotificationQueueTransition KhoiPhucLease(DateTime now)
    {
        if (Status != NotificationQueueStatus.DangBanGiao
            || DispatchLeaseExpiresAtUtc is not { } hetHan
            || now <= hetHan)
        {
            Nem(Status, NotificationQueueStatus.ChoGui);
        }

        DispatchLeaseExpiresAtUtc = null;
        return ChuyenSang(NotificationQueueStatus.ChoGui, NotificationQueueEventType.LeaseExpired);
    }

    /// <summary>Hàng 8: `DangBanGiao → Loi` (lỗi vĩnh viễn).</summary>
    public NotificationQueueTransition DanhDauLoi(string errorCode, string reasonMasked)
    {
        if (Status != NotificationQueueStatus.DangBanGiao)
        {
            Nem(Status, NotificationQueueStatus.Loi);
        }

        LastErrorCode = errorCode;
        LastReasonMasked = NotificationMasker.CheBiMat(reasonMasked);
        DispatchLeaseExpiresAtUtc = null;
        return ChuyenSang(NotificationQueueStatus.Loi, NotificationQueueEventType.Failed);
    }

    /// <summary>Hàng 9: `ChoGui → HetHan` khi quá `ExpiresAtUtc`.</summary>
    public NotificationQueueTransition HetHanTuChoGui(DateTime now)
    {
        if (Status != NotificationQueueStatus.ChoGui
            || ExpiresAtUtc is not { } han
            || now <= han)
        {
            Nem(Status, NotificationQueueStatus.HetHan);
        }

        return ChuyenSang(NotificationQueueStatus.HetHan, NotificationQueueEventType.Expired);
    }

    /// <summary>Hàng 10: `Moi`/`ChoGui → DaHuy` (huỷ có lý do).</summary>
    public NotificationQueueTransition Huy(string lyDo, Guid? userId)
    {
        _ = userId;

        if (Status is not (NotificationQueueStatus.Moi or NotificationQueueStatus.ChoGui))
        {
            Nem(Status, NotificationQueueStatus.DaHuy);
        }

        LastReasonMasked = NotificationMasker.CheBiMat(lyDo);
        return ChuyenSang(NotificationQueueStatus.DaHuy, NotificationQueueEventType.Cancelled);
    }

    /// <summary>Hàng 11: mục gốc ở lại `Loi`, ghi `Replayed` (không đổi trạng thái).</summary>
    public NotificationQueueTransition DanhDauPhatLai(Guid? userId)
    {
        _ = userId;

        if (Status != NotificationQueueStatus.Loi)
        {
            throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("from", Status.ToString())
                .WithData("to", NotificationQueueStatus.Loi.ToString());
        }

        return new NotificationQueueTransition(
            NotificationQueueEventType.Replayed,
            NotificationQueueStatus.Loi,
            NotificationQueueStatus.Loi);
    }

    /// <summary>Hàng 13: bị khử trùng cửa sổ ⇒ `DaHuy` với bucket `NULL` (ngoài partial index).</summary>
    public NotificationQueueTransition GhiNhanBiKhudup(string lyDo)
    {
        if (Status is not (NotificationQueueStatus.Moi or NotificationQueueStatus.ChoGui))
        {
            Nem(Status, NotificationQueueStatus.DaHuy);
        }

        CoalesceBucketStartUtc = null;
        LastReasonMasked = NotificationMasker.CheBiMat(lyDo);
        return ChuyenSang(NotificationQueueStatus.DaHuy, NotificationQueueEventType.Suppressed);
    }

    /// <summary>
    /// Hàng 14: `DangBanGiao → ChoGui` khi sender lỗi tạm thời **và** còn hạn hoặc không hạn
    /// (vế `ExpiresAtUtc IS NULL` — `D-261`). **Tăng** `AttemptCount`.
    /// </summary>
    public NotificationQueueTransition HoanLaiChoGui(DateTime now, string errorCode)
    {
        if (Status != NotificationQueueStatus.DangBanGiao)
        {
            Nem(Status, NotificationQueueStatus.ChoGui);
        }

        if (ExpiresAtUtc is { } han && now > han)
        {
            // Còn hạn mới được thử lại; quá hạn ⇒ đi hàng 15.
            Nem(Status, NotificationQueueStatus.ChoGui);
        }

        AttemptCount++;
        LastErrorCode = errorCode;
        DispatchLeaseExpiresAtUtc = null;
        return ChuyenSang(NotificationQueueStatus.ChoGui, NotificationQueueEventType.RetryScheduled);
    }

    /// <summary>Hàng 15: `DangBanGiao → HetHan` khi sender lỗi tạm thời **và** đã quá hạn.</summary>
    public NotificationQueueTransition HetHanTuDangBanGiao(DateTime now)
    {
        if (Status != NotificationQueueStatus.DangBanGiao
            || ExpiresAtUtc is not { } han
            || now <= han)
        {
            Nem(Status, NotificationQueueStatus.HetHan);
        }

        return ChuyenSang(NotificationQueueStatus.HetHan, NotificationQueueEventType.Expired);
    }

    private NotificationQueueTransition ChuyenSang(NotificationQueueStatus to, NotificationQueueEventType evt)
    {
        // Trạng thái kết thúc là HẤP THỤ — không có cạnh ra (hàng 12).
        if (LaHapThu(Status))
        {
            Nem(Status, to);
        }

        var from = Status;
        Status = to;
        return new NotificationQueueTransition(evt, from, to);
    }

    private void Nem(NotificationQueueStatus from, NotificationQueueStatus to)
        => throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
            .WithData("from", from.ToString())
            .WithData("to", to.ToString());

    public static bool LaHapThu(NotificationQueueStatus status)
        => status is NotificationQueueStatus.DaBanGiao
            or NotificationQueueStatus.DaHuy
            or NotificationQueueStatus.HetHan
            or NotificationQueueStatus.Loi;
}

/// <summary>
/// Sổ sự kiện **APPEND-ONLY** của một mục (`NotificationQueueEvents`, SRS §5.5).
/// 🚨 **KHÔNG** có setter đổi; **KHÔNG** có API `UPDATE`/`DELETE` (BR-006).
/// `FromStatus`/`ToStatus` là `int?` (event `Enqueued` không có trạng thái nguồn — `D-261`).
/// </summary>
public class NotificationQueueEvent : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid QueueItemId { get; private set; }
    public NotificationQueueEventType EventType { get; private set; }
    public NotificationQueueStatus? FromStatus { get; private set; }
    public NotificationQueueStatus? ToStatus { get; private set; }
    public string? ReasonMasked { get; private set; }
    public string? ErrorCode { get; private set; }
    public string CorrelationId { get; private set; } = default!;
    public Guid? ActorUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    protected NotificationQueueEvent()
    {
    }

    public static NotificationQueueEvent TaoMoi(
        Guid id,
        Guid queueItemId,
        NotificationQueueTransition transition,
        string correlationId,
        Guid? actorUserId,
        DateTime occurredAtUtc,
        string? reasonMasked = null,
        string? errorCode = null,
        Guid? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(transition);

        return new NotificationQueueEvent
        {
            Id = id,
            TenantId = tenantId,
            QueueItemId = queueItemId,
            EventType = transition.EventType,
            FromStatus = transition.FromStatus,
            ToStatus = transition.ToStatus,
            ReasonMasked = reasonMasked is null ? null : NotificationMasker.CheBiMat(reasonMasked),
            ErrorCode = errorCode,
            CorrelationId = correlationId ?? string.Empty,
            ActorUserId = actorUserId,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
