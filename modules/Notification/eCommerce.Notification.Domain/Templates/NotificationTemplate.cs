using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Notification;

/// <summary>Aggregate mẫu tin (`NotificationTemplates`, SRS §5.1). Trạng thái chỉ đổi qua hàm của entity.</summary>
public class NotificationTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ChannelKind ChannelKind { get; private set; }
    public string Locale { get; private set; } = default!;
    public NotificationPurpose Purpose { get; private set; }
    public int CoalesceWindowSeconds { get; private set; }
    public int? DefaultExpiresAfterSeconds { get; private set; }
    public NotificationTemplateStatus Status { get; private set; }
    public Guid? CurrentPublishedVersionId { get; private set; }
    public bool IsSystem { get; private set; }

    protected NotificationTemplate()
    {
    }

    public static NotificationTemplate TaoMoi(
        Guid id,
        string code,
        string name,
        ChannelKind channelKind,
        string locale,
        NotificationPurpose purpose,
        int coalesceWindowSeconds,
        int? defaultExpiresAfterSeconds,
        bool isSystem,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > NotificationConsts.CodeToiDa)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "Code");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > NotificationConsts.NameToiDa)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "Name");
        }

        if (string.IsNullOrWhiteSpace(locale) || locale.Length > NotificationConsts.LocaleToiDa)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "Locale");
        }

        if (coalesceWindowSeconds < 0)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "CoalesceWindowSeconds");
        }

        return new NotificationTemplate
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim(),
            Name = name.Trim(),
            ChannelKind = channelKind,
            Locale = locale.Trim(),
            Purpose = purpose,
            CoalesceWindowSeconds = coalesceWindowSeconds,
            DefaultExpiresAfterSeconds = defaultExpiresAfterSeconds,
            Status = NotificationTemplateStatus.Draft,
            IsSystem = isSystem
        };
    }

    public void PublishVersion(Guid versionId)
    {
        if (Status == NotificationTemplateStatus.Archived)
        {
            throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("from", Status.ToString())
                .WithData("to", NotificationTemplateStatus.Published.ToString());
        }

        CurrentPublishedVersionId = versionId;
        Status = NotificationTemplateStatus.Published;
    }

    public void Archive()
    {
        if (Status == NotificationTemplateStatus.Archived)
        {
            throw new BusinessException(NotificationErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("from", Status.ToString())
                .WithData("to", NotificationTemplateStatus.Archived.ToString());
        }

        Status = NotificationTemplateStatus.Archived;
    }
}

/// <summary>
/// Phiên bản **BẤT BIẾN** của template (SRS §5.2). Sau `PublishedAtUtc`, không có setter công khai cho
/// `Subject`/`BodyText`/`Placeholders`/`ContentHash`; sửa ⇒ NÉM `0016` (ADR-02).
/// </summary>
public class NotificationTemplateVersion : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string? Subject { get; private set; }
    public string BodyText { get; private set; } = default!;
    public NotificationBodyFormat BodyFormat { get; private set; }

    /// <summary>Whitelist placeholder dạng `jsonb` — cột DB tên `Placeholders`.</summary>
    public string PlaceholdersJson { get; private set; } = "[]";

    public string ContentHash { get; private set; } = default!;
    public DateTime PublishedAtUtc { get; private set; }
    public Guid? PublishedByUserId { get; private set; }

    /// <summary>Concurrency token (ánh xạ `xmin` của PostgreSQL).</summary>
    public uint RowVersion { get; private set; }

    protected NotificationTemplateVersion()
    {
    }

    public static NotificationTemplateVersion TaoMoi(
        Guid id,
        Guid templateId,
        int version,
        string? subject,
        string bodyText,
        NotificationBodyFormat bodyFormat,
        IReadOnlyList<NotificationPlaceholder> placeholders,
        DateTime publishedAtUtc,
        Guid? publishedByUserId,
        Guid? tenantId = null)
    {
        // GĐ3 chỉ `Text`; `Html` ⇒ NÉM 0020 TRƯỚC mọi ghi (`D-199`).
        if (bodyFormat != NotificationBodyFormat.Text)
        {
            throw new BusinessException(NotificationErrorCodes.BodyFormatKhongHoTro)
                .WithData("bodyFormat", (int)bodyFormat);
        }

        if (version < 1)
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "Version");
        }

        if (string.IsNullOrWhiteSpace(bodyText))
        {
            throw new BusinessException(NotificationErrorCodes.ThieuTruongBatBuoc).WithData("truong", "BodyText");
        }

        var placeholdersJson = NotificationPlaceholderJson.Serialize(placeholders);

        return new NotificationTemplateVersion
        {
            Id = id,
            TenantId = tenantId,
            TemplateId = templateId,
            Version = version,
            Subject = subject,
            BodyText = bodyText,
            BodyFormat = bodyFormat,
            PlaceholdersJson = placeholdersJson,
            ContentHash = TinhContentHash(subject, bodyText, placeholders),
            PublishedAtUtc = publishedAtUtc,
            PublishedByUserId = publishedByUserId
        };
    }

    /// <summary>🚨 Version đã publish là bất biến ⇒ luôn NÉM `0016` (TC-07).</summary>
    public void SuaNoiDung(string? subject, string bodyText, IReadOnlyList<NotificationPlaceholder> placeholders)
    {
        _ = subject;
        _ = bodyText;
        _ = placeholders;

        throw new BusinessException(NotificationErrorCodes.PhienBanBatBien)
            .WithData("templateVersionId", Id);
    }

    public IReadOnlyList<NotificationPlaceholder> GetPlaceholders()
        => NotificationPlaceholderJson.Deserialize(PlaceholdersJson);

    public NotificationTemplateContent ToContent()
        => new(Subject, BodyText, GetPlaceholders());

    /// <summary>SHA-256 canonical của Subject + Body + Placeholders (sắp `Ordinal`).</summary>
    public static string TinhContentHash(string? subject, string bodyText, IReadOnlyList<NotificationPlaceholder> placeholders)
    {
        var danhSach = placeholders
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}:{(int)p.Kind}:{(p.Required ? 1 : 0)}");

        var canonical = $"{subject ?? string.Empty}\u001f{bodyText}\u001f{string.Join(";", danhSach)}";
        return NotificationHash.Sha256Hex(canonical);
    }
}
