using System.Reflection;
using eCommerce.Kernel;
using eCommerce.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `NOT-002` (`WP-23`, `W23-2` F4 QA) — registry kênh gửi + DI: `TC-REG-01/02`, `TC-DI-01/02`
/// (`NOT-002-arch.md` §13.1, §5.3, §5.4).
///
/// `TC-DI-02` dựng **container ABP thật** từ `NotificationApplicationModule` (qua `AbpApplicationFactory`) —
/// **không** Host, **không** DB: resolve xong gọi `SendAsync` để chứng minh chuỗi DI đang trỏ `ChuaCauHinhKenhProvider`
/// (luôn `null`) ⇒ fail-closed `0003` ngay, trước mọi lời gọi mạng.
/// </summary>
public class NotificationChannelRegistryDiTests
{
    private sealed class KenhGia : INotificationChannelSender
    {
        public string ProviderCode { get; init; } = "fake";
        public ChannelKind ChannelKind { get; init; } = ChannelKind.Email;
        public IReadOnlyCollection<string> RequiredSettingsKeys { get; init; } = [];

        public Task<SendOutcome> SendAsync(SendCommand command, CancellationToken ct = default)
            => Task.FromResult(new SendOutcome(true, "msg-1", NotificationFailureKind.None, null));
    }

    /// <summary>Module khởi động test — gộp module Notification + Kernel (như đồ thị Host) để `eCommerceMetrics` có mặt.</summary>
    [DependsOn(typeof(NotificationApplicationModule), typeof(eCommerceKernelModule))]
    public sealed class ModuleKhoiDongTest : AbpModule
    {
    }

    // ── TC-REG: registry một cửa ────────────────────────────────────────────────────────────────

    [Fact]
    public void TC_REG_01_trung_ChannelKind_ProviderCode_thi_NEM_khi_khoi_tao()
    {
        var a = new KenhGia { ChannelKind = ChannelKind.Email, ProviderCode = "smtp" };
        var b = new KenhGia { ChannelKind = ChannelKind.Email, ProviderCode = "smtp" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new NotificationChannelFactory(new INotificationChannelSender[] { a, b }));

        Assert.Contains("Trùng adapter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TC_REG_01_cung_ProviderCode_khac_ChannelKind_thi_KHONG_trung()
    {
        var email = new KenhGia { ChannelKind = ChannelKind.Email, ProviderCode = "smtp" };
        var sms = new KenhGia { ChannelKind = ChannelKind.Sms, ProviderCode = "smtp" };

        var factory = new NotificationChannelFactory(new INotificationChannelSender[] { email, sms });

        Assert.Equal(2, factory.All.Count);
        Assert.Same(email, factory.Find(ChannelKind.Email, "smtp"));
        Assert.Same(sms, factory.Find(ChannelKind.Sms, "smtp"));
    }

    [Fact]
    public void TC_REG_02_registry_rong_tra_null_va_KHONG_fallback_kenh_khac()
    {
        var rong = new NotificationChannelFactory(Array.Empty<INotificationChannelSender>());
        Assert.Empty(rong.All);
        Assert.Null(rong.Find(ChannelKind.Email, "smtp"));
        Assert.Null(rong.Find(ChannelKind.Sms, "smtp"));
        Assert.Null(rong.Find(ChannelKind.ZaloOa, "zalo-oa"));

        var chiEmail = new NotificationChannelFactory(
            new INotificationChannelSender[] { new KenhGia { ChannelKind = ChannelKind.Email, ProviderCode = "smtp" } });

        Assert.NotNull(chiEmail.Find(ChannelKind.Email, "smtp"));
        Assert.Null(chiEmail.Find(ChannelKind.Sms, "smtp"));        // KHÔNG hạ cấp sang kênh khác
        Assert.Null(chiEmail.Find(ChannelKind.Email, "provider-khac")); // KHÔNG đoán provider
    }

    // ── TC-DI: một hiện thực `INotificationSender` + resolve qua ABP ────────────────────────────

    [Fact]
    public void TC_DI_01_assembly_production_chi_co_mot_hien_thuc_INotificationSender()
    {
        var domain = typeof(NotificationQueueItem).Assembly;
        var contracts = typeof(INotificationSender).Assembly;
        var application = typeof(NotificationSender).Assembly;

        var hienThuc = application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(INotificationSender).IsAssignableFrom(t))
            .ToList();

        Assert.Single(hienThuc);
        Assert.Equal(typeof(NotificationSender), hienThuc[0]);

        // `ChuaCoKenhGuiSender` (WP-22) đã bị thay ở `W23-2` (`AQ6`) — không còn trong assembly production nào.
        foreach (var asm in new[] { domain, contracts, application })
        {
            Assert.DoesNotContain(asm.GetTypes(), t => t.Name == "ChuaCoKenhGuiSender");
        }
    }

    [Fact]
    public async Task TC_DI_02_AddApplication_resolve_tra_NotificationSender_va_fail_closed_0003()
    {
        using var app = AbpApplicationFactory.Create<ModuleKhoiDongTest>(
            options => options.Services.AddLogging());

        // Dựng `IServiceProvider` từ container ABP (chạy `ConfigureServices` của các module) nhưng KHÔNG
        // khởi động Host/worker ⇒ không cần DB. Đây đúng là "container của `NotificationApplicationModule`".
        app.CreateServiceProvider();

        // 🚨 BẪY ABP (phát hiện ở `W23-2`): quy ước DI chỉ phơi một interface khi **tên lớp = "I" + tên interface**.
        // `ChuaCauHinhKenhProvider` ≠ `INotificationChannelConfigProvider` ⇒ chỉ đăng ký dưới CHÍNH NÓ, KHÔNG có
        // `INotificationChannelConfigProvider` ⇒ `NotificationSender` không dựng được (Host thật dính y hệt khi
        // worker `GetRequiredService<INotificationSender>()`). Ca này PHẢI đỏ tới khi BE đăng ký tường minh.
        var configProvider = app.ServiceProvider.GetService<INotificationChannelConfigProvider>();
        Assert.True(
            configProvider is ChuaCauHinhKenhProvider,
            "DI THIẾU `INotificationChannelConfigProvider`: `ChuaCauHinhKenhProvider` chỉ được đăng ký dưới chính nó "
            + "(tên lớp không khớp quy ước ABP) ⇒ không dựng được `NotificationSender`. "
            + "Sửa: `[ExposeServices(typeof(INotificationChannelConfigProvider))]` hoặc đăng ký tường minh trong module.");

        var sender = app.ServiceProvider.GetRequiredService<INotificationSender>();
        Assert.IsType<NotificationSender>(sender);

        // Stub `ChuaCauHinhKenhProvider` luôn `null` ⇒ chuỗi DI đúng ⇒ NÉM `0003` trước mọi lời gọi mạng.
        var request = new NotificationSendRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "idem-di",
            ChannelKind.Email,
            "enc-recipient",
            "subject",
            "body",
            NotificationPriorities.Normal,
            "corr-di");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sender.SendAsync(request));
        Assert.Equal(NotificationErrorCodes.KenhGuiChuaSanSang, ex.Code);
    }
}
