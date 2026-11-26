using eCommerce.Notification;

namespace eCommerce.Tests.Notification;

/// <summary>
/// Dữ liệu mồi **dùng chung** cho test module `Notification` (`W22-2`, `N84`).
/// Dựng entity **không qua DB** — máy trạng thái 15 hàng, renderer và version đều là **hàm thuần tầng Domain**
/// (`NOT-001-arch.md` §13.1). Thời điểm cố định để test **tái lập** được.
/// </summary>
internal static class NotificationTestData
{
    /// <summary>Mốc thời gian cố định UTC — mọi test dùng chung để không phụ thuộc `DateTime.Now`.</summary>
    public static readonly DateTime Now = new(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>Chuỗi hex 64 ký tự (hợp lệ cho `recipientHash`/`contentFingerprint`).</summary>
    public static string Hex(char c) => new(c, 64);

    /// <summary>Tạo mục hàng đợi ở trạng thái `Moi` với dữ liệu hợp lệ tối thiểu.</summary>
    public static NotificationQueueItem QueueItem(
        DateTime? scheduledAtUtc = null,
        DateTime? expiresAtUtc = null,
        DateTime? coalesceBucketStartUtc = null,
        Guid? batchId = null,
        string idempotencyKey = "idem-1",
        int priority = NotificationPriorities.Normal,
        Guid? templateVersionId = null)
        => NotificationQueueItem.TaoMoi(
            Guid.NewGuid(),
            idempotencyKey,
            tenantId: null,
            rootItemId: null,
            batchId: batchId,
            sourceEventType: "Manual",
            sourceAggregateId: null,
            ChannelKind.Email,
            templateId: Guid.NewGuid(),
            templateVersionId: templateVersionId ?? Guid.NewGuid(),
            recipientEncrypted: "enc",
            recipientHash: Hex('a'),
            renderedSubject: "subject",
            renderedBodyMasked: "body",
            contentFingerprint: Hex('b'),
            coalesceBucketStartUtc: coalesceBucketStartUtc,
            priority: priority,
            scheduledAtUtc: scheduledAtUtc ?? Now,
            expiresAtUtc: expiresAtUtc,
            correlationId: "corr-1");

    /// <summary>Tạo mục rồi đưa về `ChoGui` qua hàm hàng 3.</summary>
    public static NotificationQueueItem ToChoGui(DateTime? expiresAtUtc = null)
    {
        var muc = QueueItem(expiresAtUtc: expiresAtUtc);
        muc.DanhDauSanSang(Now);
        return muc;
    }

    /// <summary>Tạo mục rồi đưa về `DangBanGiao` qua chuỗi hàng 3 → hàng 5.</summary>
    public static NotificationQueueItem ToDangBanGiao(
        DateTime? expiresAtUtc = null,
        DateTime? leaseExpiresAtUtc = null)
    {
        var muc = ToChoGui(expiresAtUtc: expiresAtUtc);
        muc.GianhBanGiao(leaseExpiresAtUtc ?? Now.AddSeconds(120));
        return muc;
    }

    /// <summary>Tạo mục đã `DaBanGiao` (trạng thái hấp thụ).</summary>
    public static NotificationQueueItem ToDaBanGiao()
    {
        var muc = ToDangBanGiao();
        muc.DanhDauDaBanGiao(Now);
        return muc;
    }

    public static NotificationTemplate Template(
        string code = "tpl",
        NotificationPurpose purpose = NotificationPurpose.Transactional,
        int coalesceWindowSeconds = 0,
        Guid? publishedVersionId = null,
        Guid? id = null)
    {
        var template = NotificationTemplate.TaoMoi(
            id ?? Guid.NewGuid(),
            code,
            "Tên mẫu",
            ChannelKind.Email,
            "vi",
            purpose,
            coalesceWindowSeconds,
            defaultExpiresAfterSeconds: null,
            isSystem: false,
            tenantId: null);

        if (publishedVersionId is { } versionId)
        {
            template.PublishVersion(versionId);
        }

        return template;
    }

    public static NotificationTemplateVersion Version(
        Guid templateId,
        string? subject = "Xin chào {{Ten}}",
        string bodyText = "Nội dung {{Ten}}",
        int version = 1,
        IReadOnlyList<NotificationPlaceholder>? placeholders = null,
        NotificationBodyFormat bodyFormat = NotificationBodyFormat.Text,
        Guid? id = null)
        => NotificationTemplateVersion.TaoMoi(
            id ?? Guid.NewGuid(),
            templateId,
            version,
            subject,
            bodyText,
            bodyFormat,
            placeholders ?? [new NotificationPlaceholder("Ten", NotificationPlaceholderKind.Text, true)],
            publishedAtUtc: Now,
            publishedByUserId: null,
            tenantId: null);
}
