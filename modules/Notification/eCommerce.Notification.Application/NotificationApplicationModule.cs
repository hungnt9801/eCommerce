using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace eCommerce.Notification;

/// <summary>
/// Tầng application của module Notification: 3 app service, appender chống trùng, sender trung lập NCC
/// (fail-closed `0003` khi chưa cấu hình kênh) và worker bàn giao định kỳ.
/// </summary>
[DependsOn(
    typeof(NotificationDomainModule),
    typeof(NotificationApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundWorkersModule)
)]
public class NotificationApplicationModule : AbpModule
{
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // MỘT worker định kỳ, KHÔNG dynamic worker (bài học C10). Cờ `Notification:EnableDispatch`
        // mặc định `false` ⇒ worker chỉ log Warning, KHÔNG bàn giao.
        await context.AddBackgroundWorkerAsync<NotificationDispatchWorker>();
    }
}
