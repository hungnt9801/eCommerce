using eCommerce.Kernel.Observability;
using eCommerce.Promotions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Promotions.Workers;

/// <summary>
/// **JOB `voucher-expiry` — nhả lượt voucher hết TTL** (`PRO-001-arch` §6.4, `D-306`).
///
/// 🔒 Chỉ nhả `Status = 0` (`Reserved`) + `ExpiresAtUtc &lt;= now`; guard `UPDATE … WHERE "Status" = 0`
/// (0 dòng ⇒ bỏ qua, **không** phải lỗi — hàng rào vẫn là `UPDATE` có điều kiện). Mỗi **lô 500** trong **một**
/// UoW `requiresNew`. Cập nhật **mọi dòng mã trước**, rồi **dòng promotion sau**, sắp `Id` ⇒ tránh `40P01`.
/// Bắt `40P01` ⇒ thử lại **có trần** (`SoLanThuDeadlock`). **Không** phát ETO ở v1.
///
/// 📌 Chạy ở host ⇒ `DISTINCT TenantId` (tắt tenant filter CÓ LÝ DO) rồi `ICurrentTenant.Change`; chống chạy chồng
/// bằng khoá `promotion:job:voucher-expiry:{tenant}` `SkipIfBusy` (TTL 300 s). Cờ `Promotions:Enabled = false` ⇒ 0 việc.
/// </summary>
public class VoucherExpiryWorker : AsyncPeriodicBackgroundWorkerBase
{
    public const int NhipMacDinhPhut = 5;
    public const int LoToiDa = 500;
    public const int SoLanThuDeadlock = 3;

    public VoucherExpiryWorker(
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
        Logger.LogInformation("JOB voucher-expiry: bắt đầu một lượt.");

        using var scope = workerContext.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var settings = sp.GetRequiredService<ISettingProvider>();
        if (!PromotionSettings.DocBat(await settings.GetOrNullAsync(PromotionContractConsts.EnabledSettingName)))
        {
            Logger.LogInformation("JOB voucher-expiry: Promotions:Enabled=false ⇒ không làm gì.");
            return;
        }

        var redemptions = sp.GetRequiredService<IRepository<PromotionRedemption, Guid>>();
        var promotions = sp.GetRequiredService<IRepository<Promotion, Guid>>();
        var dataFilter = sp.GetRequiredService<IDataFilter>();
        var currentTenant = sp.GetRequiredService<ICurrentTenant>();
        var khoa = sp.GetRequiredService<IAbpDistributedLock>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();
        var clock = sp.GetRequiredService<IClock>();
        var metrics = sp.GetRequiredService<eCommerceMetrics>();

        List<Guid?> tenants;

        // 🚨 Tắt tenant filter CÓ CHỦ: job hệ thống phục vụ MỌI tenant, không trả dữ liệu cho client.
        using (dataFilter.Disable<IMultiTenant>())
        {
            var tatCa = await redemptions.GetListAsync(x => x.Status == PromotionRedemptionStatus.Reserved);
            tenants = tatCa.Select(x => x.TenantId).Distinct().ToList();
        }

        if (tenants.Count == 0)
        {
            Logger.LogInformation("JOB voucher-expiry: không có lượt đang giữ nào — 0 thay đổi.");
            return;
        }

        var now = clock.Now.ToUniversalTime();
        var tongNha = 0;

        foreach (var tenantId in tenants)
        {
            await using var giuKhoa = await khoa.TryAcquireAsync(
                PromotionLockKeys.Job("voucher-expiry", tenantId), TimeSpan.FromSeconds(300));

            if (giuKhoa is null)
            {
                metrics.LockSkipped.Add(1,
                    new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "promotions"));
                Logger.LogWarning("JOB voucher-expiry: bỏ lượt tenant {TenantId} — khoá đang bận.", tenantId);
                continue;
            }

            using (currentTenant.Change(tenantId))
            {
                tongNha += await NhaTheoTenantAsync(
                    redemptions, promotions, uowManager, metrics, tenantId, now, workerContext.CancellationToken);
            }
        }

        Logger.LogInformation("JOB voucher-expiry: xong — {Tong} lượt được nhả.", tongNha);
    }

    private async Task<int> NhaTheoTenantAsync(
        IRepository<PromotionRedemption, Guid> redemptions,
        IRepository<Promotion, Guid> promotions,
        IUnitOfWorkManager uowManager,
        eCommerceMetrics metrics,
        Guid? tenantId,
        DateTime now,
        CancellationToken ct)
    {
        var tong = 0;

        while (!ct.IsCancellationRequested)
        {
            List<Guid> ids;

            // Đọc lô kế tiếp trong một UoW NGẮN (chỉ đọc) — tránh giữ transaction khi quét.
            using (var uowDoc = uowManager.Begin(requiresNew: true, isTransactional: false))
            {
                var lo = await redemptions.GetListAsync(
                    x => x.Status == PromotionRedemptionStatus.Reserved
                         && x.ExpiresAtUtc != null
                         && x.ExpiresAtUtc <= now,
                    cancellationToken: ct);

                ids = lo.OrderBy(x => x.Id).Take(LoToiDa).Select(x => x.Id).ToList();
                await uowDoc.CompleteAsync();
            }

            if (ids.Count == 0)
            {
                break;
            }

            var (ok, daNha) = await NhaMotLoAsync(redemptions, promotions, uowManager, metrics, tenantId, ids, now, ct);
            tong += daNha;

            if (!ok || ids.Count < LoToiDa)
            {
                break;
            }
        }

        return tong;
    }

    /// <summary>Nhả MỘT lô trong UoW `requiresNew`; bắt `40P01` ⇒ thử lại có trần (mỗi lần một UoW MỚI).</summary>
    private async Task<(bool Ok, int DaNha)> NhaMotLoAsync(
        IRepository<PromotionRedemption, Guid> redemptions,
        IRepository<Promotion, Guid> promotions,
        IUnitOfWorkManager uowManager,
        eCommerceMetrics metrics,
        Guid? tenantId,
        IReadOnlyList<Guid> ids,
        DateTime now,
        CancellationToken ct)
    {
        for (var lan = 1; lan <= SoLanThuDeadlock; lan++)
        {
            try
            {
                int daNha;

                using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
                {
                    daNha = await CapNhatLoAsync(redemptions, promotions, tenantId, ids, now, ct);
                    await uow.CompleteAsync();
                }

                // Phát metric SAU commit (tránh đếm khi UoW rollback do deadlock).
                if (daNha > 0)
                {
                    metrics.PromotionVoucherReservationExpired.Add(daNha,
                        new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, "voucher"));
                    metrics.PromotionVoucherReleased.Add(daNha,
                        new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, "TTL_EXPIRED"));
                }

                return (true, daNha);
            }
            catch (Exception ex) when (LaDeadlock(ex))
            {
                if (lan == SoLanThuDeadlock)
                {
                    Logger.LogError("JOB voucher-expiry: deadlock 40P01 sau {Lan} lần — bỏ lô (thử nhịp sau).", lan);
                    return (false, 0);
                }

                Logger.LogWarning("JOB voucher-expiry: deadlock 40P01 lần {Lan}/{Max} — thử lại lô với UoW mới.", lan, SoLanThuDeadlock);
            }
        }

        return (false, 0);
    }

    /// <summary>
    /// Cập nhật trong MỘT transaction: lượt (sắp `Id`) → **mã** (sắp `Id`) → **promotion** (sắp `Id`, chỉ khi
    /// `MaxUsageTotal` có giá trị). Thứ tự cố định để tránh chu trình khoá (`40P01`).
    /// </summary>
    private async Task<int> CapNhatLoAsync(
        IRepository<PromotionRedemption, Guid> redemptions,
        IRepository<Promotion, Guid> promotions,
        Guid? tenantId,
        IReadOnlyList<Guid> ids,
        DateTime now,
        CancellationToken ct)
    {
        var db = await redemptions.GetDbContextAsync();
        var lo = await redemptions.GetListAsync(x => ids.Contains(x.Id), cancellationToken: ct);

        var daNha = 0;
        var voucherIds = new HashSet<Guid>();
        var promotionIds = new HashSet<Guid>();

        foreach (var r in lo.OrderBy(x => x.Id))
        {
            var soDong = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotionRedemptions""
   SET ""Status"" = 2, ""ReleasedAtUtc"" = {now}, ""ReasonCode"" = 'TTL_EXPIRED'
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {r.Id}
   AND ""Status"" = 0", ct);

            if (soDong == 0)
            {
                // Đua với commit/instance khác ⇒ bỏ qua (không phải lỗi).
                Logger.LogDebug("JOB voucher-expiry: bỏ qua lượt {RedemptionId} (đã đổi trạng thái).", r.Id);
                continue;
            }

            daNha++;
            if (r.VoucherCodeId is Guid voucherId)
            {
                voucherIds.Add(voucherId);
            }

            promotionIds.Add(r.PromotionId);
        }

        // MÃ trước — sắp Id.
        foreach (var voucherId in voucherIds.OrderBy(x => x))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProVoucherCodes""
   SET ""ReservedCount"" = ""ReservedCount"" - 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {voucherId}
   AND ""ReservedCount"" > 0", ct);
        }

        // PROMOTION sau — sắp Id; chỉ chạm dòng có trần (MaxUsageTotal).
        if (promotionIds.Count > 0)
        {
            var proms = await promotions.GetListAsync(
                x => promotionIds.Contains(x.Id) && x.MaxUsageTotal != null,
                cancellationToken: ct);

            foreach (var pid in proms.Select(x => x.Id).OrderBy(x => x))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotions""
   SET ""ReservedCount"" = ""ReservedCount"" - 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {pid}
   AND ""ReservedCount"" > 0", ct);
            }
        }

        return daNha;
    }

    /// <summary>Phát hiện `40P01` (deadlock) mà **không** phụ thuộc trực tiếp `Npgsql`.</summary>
    private static bool LaDeadlock(Exception ex)
    {
        for (var e = (Exception?)ex; e is not null; e = e.InnerException)
        {
            var type = e.GetType();
            if (type.Name == "PostgresException"
                && type.GetProperty("SqlState")?.GetValue(e) as string == "40P01")
            {
                return true;
            }
        }

        return false;
    }
}
