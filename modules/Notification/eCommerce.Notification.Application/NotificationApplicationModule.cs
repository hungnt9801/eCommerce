using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace eCommerce.Notification;

/// <summary>
/// Tầng application của module Notification: 3 app service, appender chống trùng, sender fail-closed
/// mặc định và worker bàn giao định kỳ.
/// </summary>
[DependsOn(
    typeof(NotificationDomainModule),
    typeof(NotificationApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundWorkersModule)
)]
public class NotificationApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 🚨 Mặc định WP-22: sender fail-closed — KHÔNG có kênh gửi thật (`D-263`).
        context.Services.AddTransient<INotificationSender, ChuaCoKenhGuiSender>();
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // MỘT worker định kỳ, KHÔNG dynamic worker (bài học C10). Cờ `Notification:EnableDispatch`
        // mặc định `false` ⇒ worker chỉ log Warning, KHÔNG bàn giao.
        await context.AddBackgroundWorkerAsync<NotificationDispatchWorker>();
    }
}

/// <summary>
/// Sender **fail-closed** cho tới khi `NOT-002` (WP-23) hiện thực sender thật.
/// 🚨 **NÉM** (không trả "thành công ma") ⇒ mục ở lại `DangBanGiao` → lease đưa về `ChoGui`;
/// **0** dấu `DaBanGiao` giả (`D-263`).
/// </summary>
public sealed class ChuaCoKenhGuiSender : INotificationSender, Volo.Abp.DependencyInjection.ITransientDependency
{
    public Task<NotificationSendResult> SendAsync(NotificationSendRequest request, CancellationToken ct = default)
    {
        _ = request;
        _ = ct;

        throw new InvalidOperationException(
            "Chua co kenh gui thuc (NOT-002/WP-23). Sender fail-closed: KHONG danh dau DaBanGiao gia. " +
            "Bat `Notification:EnableDispatch=false` cho toi khi co sender that.");
    }
}
