using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace eCommerce.ChannelHub;

/// <summary>
/// Tầng application của ChannelHub: điều phối đồng bộ, đăng ký connector,
/// và chạy worker nền (supervisor + worker động cho từng shop).
/// </summary>
[DependsOn(
    typeof(ChannelHubDomainModule),
    typeof(ChannelHubApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundWorkersModule),
    // 🚨 BẮT BUỘC: cung cấp `IEventCoverageProbe` — cổng an toàn chống mất đơn (lỗi #22).
    // Thiếu dòng này thì `ChannelShopSyncer` không resolve được ⇒ ứng dụng KHÔNG khởi động (ồn ào),
    // thay vì âm thầm quay lại hành vi "publish cho không ai" làm mất đơn vĩnh viễn.
    typeof(eCommerce.Messaging.MessagingDomainModule)
)]
public class ChannelHubApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Connector được resolve qua DI theo mã kênh (xem IChannelConnectorFactory).
        // Thêm sàn mới = thêm một dòng ở đây + một class ShopeeConnector tương tự.
        context.Services.AddTransient<Connectors.ShopeeConnector>();

        // Factory là singleton vì chỉ tra cứu type; connector bên trong là transient.
        context.Services.AddSingleton<Connectors.ChannelConnectorFactory>();
        context.Services.AddSingleton<Connectors.IChannelConnectorFactory>(
            sp => sp.GetRequiredService<Connectors.ChannelConnectorFactory>());

        context.Services.AddTransient<Sync.ChannelTokenManager>();
        context.Services.AddTransient<Sync.ChannelShopSyncer>();

        // 🚨 Cổng đồng bộ cho tầng APP: cùng MỘT đối tượng, chỉ thêm giao diện.
        // Nhờ vậy `ChannelAppService.SyncNowAsync` không thể lệch hành vi so với worker nền
        // (biến thể thứ hai của lỗi #22 nằm đúng ở đường "đồng bộ ngay" thủ công).
        context.Services.AddTransient<Sync.IChannelShopSyncService>(
            sp => sp.GetRequiredService<Sync.ChannelShopSyncer>());
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // Supervisor là worker DUY NHẤT chạy cố định. Nó tự đăng ký/huỷ worker động cho từng shop.
        // Nhờ vậy không cần cấu hình tay khi shop mới được kết nối.
        await context.AddBackgroundWorkerAsync<Sync.ChannelSyncSupervisorWorker>();
    }
}
