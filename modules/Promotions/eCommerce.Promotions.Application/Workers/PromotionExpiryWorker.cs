using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Promotions.Workers;

/// <summary>Tùy chọn job khuyến mãi (`section Promotions`).</summary>
public class PromotionOptions
{
    public int JobPeriodMinutes { get; set; } = 5;
    public string ExpiryJobName { get; set; } = "expiry";

    /// <summary>
    /// 🚨 `D-295` — số lần **THỬ** cấp `UsageSequence` (1 ghi + 4 thử lại). Hết trần ⇒ `0104` (`PRO-001` §6.2).
    /// </summary>
    public int MaxUsageSequenceAttempts { get; set; } = 5;
}

/// <summary>
/// **JOB `promotion-expiry` — kích hoạt + hết hạn theo GIỜ** (`PRO-002` §6.3 / §7.2 #5).
///
/// 🔒 Một job làm **cả hai chiều**: `Approved → Active` khi `Now ≥ StartsAtUtc` **và** `Active`/`Paused → Expired`
/// khi `Now > EndsAtUtc`. **KHÔNG** khoá theo kỳ (mỗi chuyển idempotent nhờ guard trạng thái + `RowVersion`);
/// chống chạy chồng bằng khoá `promotion:job:expiry:{tenantId}` (`SkipIfBusy`).
/// 🚨 Worker chạy ở host ⇒ lấy `DISTINCT TenantId` (tắt tenant filter **có ghi lý do**) rồi `ICurrentTenant.Change`.
/// </summary>
public class PromotionExpiryWorker : AsyncPeriodicBackgroundWorkerBase
{
    public const int NhipMacDinhPhut = 5;

    public PromotionExpiryWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PromotionOptions> tuyChon)
        : base(timer, serviceScopeFactory)
    {
        var phut = tuyChon.Value.JobPeriodMinutes <= 0 ? NhipMacDinhPhut : tuyChon.Value.JobPeriodMinutes;
        Timer.Period = phut * 60 * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 LOG VÔ ĐIỀU KIỆN ở đầu mỗi lượt — phân biệt "job không chạy" với "chạy mà không có việc".
        Logger.LogInformation("JOB promotion-expiry: bắt đầu một lượt.");

        using var scope = workerContext.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var repo = sp.GetRequiredService<IRepository<Promotion, Guid>>();
        var dataFilter = sp.GetRequiredService<IDataFilter>();
        var currentTenant = sp.GetRequiredService<ICurrentTenant>();
        var khoa = sp.GetRequiredService<IAbpDistributedLock>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var clock = sp.GetRequiredService<IClock>();
        var metrics = sp.GetRequiredService<Kernel.Observability.eCommerceMetrics>();

        List<Guid?> tenants;

        // 🚨 Tắt tenant filter CÓ CHỦ: job hệ thống phục vụ MỌI tenant, không trả dữ liệu cho client.
        using (dataFilter.Disable<IMultiTenant>())
        {
            var tatCa = await repo.GetListAsync();
            tenants = tatCa.Select(x => x.TenantId).Distinct().ToList();
        }

        if (tenants.Count == 0)
        {
            Logger.LogInformation("JOB promotion-expiry: không có promotion nào — 0 thay đổi.");
            return;
        }

        var now = clock.Now.ToUniversalTime();
        var soDoi = 0;

        foreach (var tenantId in tenants)
        {
            await using var giuKhoa = await khoa.TryAcquireAsync(
                PromotionLockKeys.Job("expiry", tenantId), TimeSpan.FromSeconds(300));

            if (giuKhoa is null)
            {
                // Bỏ lượt của tenant này — instance khác đang chạy. LOG + METRIC (XC-003 D10).
                metrics.LockSkipped.Add(1, new KeyValuePair<string, object?>(
                    Kernel.Observability.eCommerceMetricTags.Module, "promotions"));
                Logger.LogWarning("JOB promotion-expiry: bỏ lượt tenant {TenantId} — khoá đang bận.", tenantId);
                continue;
            }

            using (currentTenant.Change(tenantId))
            {
                using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

                var ds = await repo.GetListAsync(x =>
                    x.Status == PromotionStatus.Approved
                    || x.Status == PromotionStatus.Active
                    || x.Status == PromotionStatus.Paused);

                foreach (var p in ds)
                {
                    var doi = p.Status == PromotionStatus.Approved
                        ? p.ActivateByJob(now)
                        : p.ExpireByJob(now);

                    if (doi)
                    {
                        await repo.UpdateAsync(p, autoSave: false);
                        soDoi++;
                    }
                }

                await uow.CompleteAsync();
            }
        }

        Logger.LogInformation("JOB promotion-expiry: xong — {SoDoi} promotion đổi trạng thái.", soDoi);
    }
}
