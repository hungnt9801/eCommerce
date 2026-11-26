using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;
using eCommerce.Promotions.Workers;

namespace eCommerce.Promotions;

/// <summary>
/// **MODULE `Promotions` — APPLICATION** (`PRO-002`): engine in-process, app service quản trị cây,
/// job `promotion-expiry`.
/// </summary>
[DependsOn(
    typeof(PromotionsDomainModule),
    typeof(Contracts.PromotionsApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpEventBusModule)
)]
public class PromotionsApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<PromotionOptions>(context.Services.GetConfiguration().GetSection("Promotions"));
    }

    /// <summary>
    /// 🚨 **ĐĂNG KÝ WORKER — THIẾU DÒNG NÀY THÌ JOB KHÔNG BAO GIỜ CHẠY** và không có lỗi biên dịch.
    /// </summary>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<PromotionExpiryWorker>();
    }
}
