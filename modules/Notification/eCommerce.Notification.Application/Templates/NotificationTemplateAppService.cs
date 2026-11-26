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

/// <summary>
/// Template + version bất biến. `[Authorize]` ở **CẤP CLASS** + permission từng method (quy ước #6).
/// </summary>
[Authorize]
public class NotificationTemplateAppService : ApplicationService, INotificationTemplateAppService
{
    private readonly IRepository<NotificationTemplate, Guid> _templates;
    private readonly IRepository<NotificationTemplateVersion, Guid> _versions;
    private readonly INotificationRenderer _renderer;
    private readonly INotificationMoneyFormatter _money;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAbpDistributedLock _lock;

    public NotificationTemplateAppService(
        IRepository<NotificationTemplate, Guid> templates,
        IRepository<NotificationTemplateVersion, Guid> versions,
        INotificationRenderer renderer,
        INotificationMoneyFormatter money,
        IGuidGenerator guid,
        IClock clock,
        ICurrentUser currentUser,
        IAbpDistributedLock lockService)
    {
        _templates = templates;
        _versions = versions;
        _renderer = renderer;
        _money = money;
        _guid = guid;
        _clock = clock;
        _currentUser = currentUser;
        _lock = lockService;
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesView)]
    public async Task<PagedResultDto<NotificationTemplateDto>> GetTemplatesAsync(PagedAndSortedResultRequestDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var all = await _templates.GetListAsync();
        var sapXep = all.OrderBy(x => x.Code, StringComparer.Ordinal).ToList();
        var total = sapXep.Count;

        var items = sapXep
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount <= 0 ? NotificationConsts.CodeToiDa : input.MaxResultCount)
            .Select(NotificationMappings.ToDto)
            .ToList();

        return new PagedResultDto<NotificationTemplateDto>(total, items);
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesManage)]
    public async Task<NotificationTemplateDto> CreateAsync(CreateTemplateDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // GĐ3 chỉ Text — `Html` ⇒ NÉM 0020 TRƯỚC mọi ghi (`D-199`).
        if (input.BodyFormat != NotificationBodyFormat.Text)
        {
            throw new BusinessException(NotificationErrorCodes.BodyFormatKhongHoTro)
                .WithData("bodyFormat", (int)input.BodyFormat);
        }

        // `CoalesceWindowSeconds` chỉ bật cho Marketing (`ADR-12`).
        var cuaSo = input.Purpose == NotificationPurpose.Marketing ? 300 : 0;

        var template = NotificationTemplate.TaoMoi(
            _guid.Create(),
            input.Code,
            input.Name,
            input.ChannelKind,
            input.Locale,
            input.Purpose,
            cuaSo,
            defaultExpiresAfterSeconds: input.Purpose == NotificationPurpose.Marketing ? 259_200 : 86_400,
            isSystem: false,
            tenantId: CurrentTenant.Id);

        await _templates.InsertAsync(template, autoSave: true);
        return template.ToDto();
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesPublish)]
    public async Task<NotificationTemplateVersionDto> PublishVersionAsync(PublishTemplateVersionDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.BodyFormat != NotificationBodyFormat.Text)
        {
            throw new BusinessException(NotificationErrorCodes.BodyFormatKhongHoTro)
                .WithData("bodyFormat", (int)input.BodyFormat);
        }

        await using var handle = await _lock.TryAcquireAsync(
            NotificationLockKeys.Template(input.TemplateId), TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "notification:template");
        }

        var template = await _templates.FindAsync(input.TemplateId)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "template");

        var daCo = await _versions.GetListAsync(x => x.TemplateId == input.TemplateId);
        var soTiepTheo = (daCo.Count == 0 ? 0 : daCo.Max(x => x.Version)) + 1;

        var placeholders = input.Placeholders
            .Select(p => new NotificationPlaceholder(p.Name, p.Kind, p.Required))
            .ToList();

        var version = NotificationTemplateVersion.TaoMoi(
            _guid.Create(),
            template.Id,
            soTiepTheo,
            input.Subject,
            input.BodyText,
            input.BodyFormat,
            placeholders,
            _clock.Now.ToUniversalTime(),
            _currentUser.Id,
            template.TenantId);

        await _versions.InsertAsync(version, autoSave: false);

        template.PublishVersion(version.Id);
        await _templates.UpdateAsync(template, autoSave: true);

        return version.ToDto();
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesView)]
    public async Task<PreviewResultDto> PreviewAsync(PreviewTemplateDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Chỉ dữ liệu mẫu — KHÔNG ghi DB, KHÔNG gửi (`D-260`).
        var template = await _templates.FindAsync(input.TemplateId)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "template");

        var versionId = input.TemplateVersionId ?? template.CurrentPublishedVersionId
            ?? throw new BusinessException(NotificationErrorCodes.TemplateChuaSanSang)
                .WithData("code", template.Code);

        var version = await _versions.FindAsync(versionId)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "version");

        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in input.SampleData)
        {
            data[kvp.Key] = kvp.Value;
        }

        var rendered = _renderer.Render(version.ToContent(), data, template.Locale, _money);

        return new PreviewResultDto
        {
            Subject = rendered.Subject,
            Body = rendered.Body,
            BodyFingerprint = rendered.BodyFingerprint
        };
    }

    [Authorize(eCommerceConsts.Permissions.Notifications.TemplatesPublish)]
    public async Task ArchiveAsync(Guid id)
    {
        var template = await _templates.FindAsync(id)
            ?? throw new BusinessException(NotificationErrorCodes.KhongTimThay).WithData("loai", "template");

        template.Archive();
        await _templates.UpdateAsync(template, autoSave: true);
    }
}
