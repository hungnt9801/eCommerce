using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace eCommerce.Notification;

/// <summary>
/// Seed **3 template hệ thống** (`TenantId = NULL`) — idempotent theo `(Code, ChannelKind, Locale)`.
/// 🚨 **0 PII**: chỉ placeholder + dữ liệu mẫu tổng hợp; KHÔNG seed người nhận/lô/mục (`D-191`/`D-257`).
/// </summary>
public class NotificationDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly (string Code, ChannelKind Channel, NotificationPurpose Purpose)[] HeThong =
    [
        ("ops-alert", ChannelKind.Email, NotificationPurpose.OpsAlert),
        ("order-confirmation", ChannelKind.Email, NotificationPurpose.Transactional),
        ("order-shipped", ChannelKind.Email, NotificationPurpose.Transactional)
    ];

    private readonly IRepository<NotificationTemplate, Guid> _templateRepo;
    private readonly IRepository<NotificationTemplateVersion, Guid> _versionRepo;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;

    public NotificationDataSeedContributor(
        IRepository<NotificationTemplate, Guid> templateRepo,
        IRepository<NotificationTemplateVersion, Guid> versionRepo,
        IGuidGenerator guid,
        IClock clock)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _guid = guid;
        _clock = clock;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Chỉ seed host (template hệ thống TenantId = NULL).
        if (context.TenantId is not null)
        {
            return;
        }

        foreach (var (code, channel, purpose) in HeThong)
        {
            // Kiểm TỪNG MÃ (không `count()`), chạy lại bao nhiêu lần cũng không nhân bản.
            if (await _templateRepo.AnyAsync(x => x.Code == code && x.ChannelKind == channel && x.Locale == "vi"))
            {
                continue;
            }

            var (subject, body, placeholders) = MauCua(code);

            // Bật khử trùng cửa sổ CHỈ cho Marketing (`ADR-12`); 3 mẫu hệ thống đều không phải Marketing.
            var template = NotificationTemplate.TaoMoi(
                Guid.Parse(DanhTinhId(code)),
                code,
                TenHienThi(code),
                channel,
                "vi",
                purpose,
                coalesceWindowSeconds: 0,
                defaultExpiresAfterSeconds: 86_400,
                isSystem: true,
                tenantId: null);

            var version = NotificationTemplateVersion.TaoMoi(
                _guid.Create(),
                template.Id,
                1,
                subject,
                body,
                NotificationBodyFormat.Text,
                placeholders,
                _clock.Now.ToUniversalTime(),
                publishedByUserId: null,
                tenantId: null);

            template.PublishVersion(version.Id);

            await _templateRepo.InsertAsync(template, autoSave: true);
            await _versionRepo.InsertAsync(version, autoSave: true);
        }
    }

    private static string TenHienThi(string code) => code switch
    {
        "ops-alert" => "Cảnh báo vận hành",
        "order-confirmation" => "Xác nhận đơn hàng",
        "order-shipped" => "Đơn hàng đã giao vận",
        _ => code
    };

    /// <summary>
    /// `GUID` hằng cho template hệ thống — ổn định giữa các lần seed/chạy để tham chiếu chéo an toàn.
    /// </summary>
    private static string DanhTinhId(string code) => code switch
    {
        "ops-alert" => "0193a000-0000-7000-8000-000000000001",
        "order-confirmation" => "0193a000-0000-7000-8000-000000000002",
        "order-shipped" => "0193a000-0000-7000-8000-000000000003",
        _ => throw new InvalidOperationException($"Template hệ thống không xác định: {code}")
    };

    private static (string Subject, string Body, IReadOnlyList<NotificationPlaceholder> Placeholders) MauCua(string code)
    {
        switch (code)
        {
            case "ops-alert":
                return (
                    "[Cảnh báo vận hành] {{TieuDe}}",
                    "Hệ thống ghi nhận sự kiện: {{TieuDe}}\nMức độ: {{MucDo}}\nThời điểm: {{ThoiDiem}}",
                    [
                        new NotificationPlaceholder("TieuDe", NotificationPlaceholderKind.Text, true),
                        new NotificationPlaceholder("MucDo", NotificationPlaceholderKind.Text, true),
                        new NotificationPlaceholder("ThoiDiem", NotificationPlaceholderKind.DateTime, true)
                    ]);

            case "order-confirmation":
                return (
                    "Xác nhận đơn {{MaDon}}",
                    "Cảm ơn bạn đã đặt hàng {{MaDon}}.\nTổng tiền: {{TongTien}}",
                    [
                        new NotificationPlaceholder("MaDon", NotificationPlaceholderKind.Text, true),
                        new NotificationPlaceholder("TongTien", NotificationPlaceholderKind.Money, true)
                    ]);

            case "order-shipped":
                return (
                    "Đơn {{MaDon}} đã giao vận",
                    "Đơn {{MaDon}} đang trên đường tới bạn.\nDự kiến giao: {{NgayDuKien}}",
                    [
                        new NotificationPlaceholder("MaDon", NotificationPlaceholderKind.Text, true),
                        new NotificationPlaceholder("NgayDuKien", NotificationPlaceholderKind.DateTime, true)
                    ]);

            default:
                throw new InvalidOperationException($"Template hệ thống không xác định: {code}");
        }
    }
}
