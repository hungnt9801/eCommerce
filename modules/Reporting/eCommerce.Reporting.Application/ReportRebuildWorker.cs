using eCommerce.Kernel.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Reporting;

/// <summary>
/// **WORKER XỬ LÝ HÀNG ĐỢI REBUILD** (`WP-26`, `REP-001` §6.1).
///
/// 🔑 **VÌ SAO QUÉT DB (không `IBackgroundJobManager`):** hàng đợi = `RepRunLogs.Status = Queued` ⇒ bền qua
/// restart, không thêm `[DependsOn]`. Mỗi ngày **một UoW** (as-built `DELETE`+`INSERT`); `GhiCuaSo` ghi
/// **cùng** UoW với dữ liệu ⇒ đứt giữa chừng thì cả hai rollback ⇒ `WindowsDone` **không tiến** ⇒ chạy tiếp
/// từ `CheckpointDate + 1` **không** chạy lại cửa sổ cũ (`TC-10`/NFR-009).
///
/// 🚨 **BẮT BUỘC `ICurrentTenant.Change(run.TenantId)` TRƯỚC UoW** — background worker chạy ở host; không đổi
/// tenant thì query filter `IMultiTenant` chỉ thấy `TenantId = NULL` ⇒ rebuild **rỗng im lặng** (`NC-5`).
/// </summary>
public class ReportRebuildWorker : AsyncPeriodicBackgroundWorkerBase
{
    public const int NhipMacDinhGiay = 60;

    public ReportRebuildWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 LOG VÔ ĐIỀU KIỆN ở đầu mỗi lượt (`inv-001` §27.3 + `BackgroundWorkerCoCheTests`).
        Logger.LogInformation("JOB ReportRebuildWorker: bắt đầu quét hàng đợi rebuild.");

        List<Guid> hangDoi;

        using (var scope = workerContext.ServiceProvider.CreateScope())
        {
            var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<RepRunLog, Guid>>();

            // 🚨 Worker chạy ở HOST ⇒ phải TẮT filter tenant để thấy hàng đợi của MỌI tenant.
            using (dataFilter.Disable<IMultiTenant>())
            {
                hangDoi = (await repo.GetListAsync(x => x.Status == RepRunStatus.Queued))
                    .Select(x => x.Id)
                    .ToList();
            }
        }

        if (hangDoi.Count == 0)
        {
            Logger.LogInformation("JOB ReportRebuildWorker: không có lượt nào đang Queued.");
            return;
        }

        foreach (var runId in hangDoi)
        {
            try
            {
                await XuLyMotLuotAsync(workerContext, runId);
            }
            catch (Exception ex)
            {
                // 🚨 Không nuốt: nếu rebuild hỏng, read-model đứng ở mốc cũ mà không ai biết.
                Logger.LogError(ex, "JOB ReportRebuildWorker: THẤT BẠI khi xử lý lượt {RunId}.", runId);
            }
        }
    }

    private async Task XuLyMotLuotAsync(PeriodicBackgroundWorkerContext workerContext, Guid runId)
    {
        using var scope = workerContext.ServiceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var dataFilter = sp.GetRequiredService<IDataFilter>();
        var repo = sp.GetRequiredService<IRepository<RepRunLog, Guid>>();
        var tenant = sp.GetRequiredService<ICurrentTenant>();
        var dongHo = sp.GetRequiredService<IClock>();
        var metrics = sp.GetRequiredService<eCommerceMetrics>();
        var khoa = sp.GetRequiredService<IAbpDistributedLock>();
        var uowManager = sp.GetRequiredService<IUnitOfWorkManager>();

        RepRunLog run;

        using (dataFilter.Disable<IMultiTenant>())
        {
            run = await repo.GetAsync(runId);
        }

        // 🚨 ĐỔI TENANT TRƯỚC MỌI UoW — nếu không, mọi query filter lọc nhầm ⇒ rebuild rỗng im lặng.
        using (tenant.Change(run.TenantId))
        {
            await using var giuKhoa = await khoa.TryAcquireAsync(
                ReportingLockKeys.Rebuild(run.TenantId, run.Projection), TimeSpan.FromSeconds(30));

            if (giuKhoa is null)
            {
                // 📌 Lượt khác đang chạy ⇒ GIỮ NGUYÊN Queued, thử lại nhịp sau (Skipped dự trữ).
                Logger.LogWarning("JOB ReportRebuildWorker: bỏ lượt {RunId} — khoá rebuild đang bận.", runId);
                return;
            }

            var luc = dongHo.Now.ToUniversalTime();

            // Pha 1: Queued → Running.
            using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                var r = await repo.GetAsync(runId);
                r.BatDau(luc, Environment.MachineName);
                await repo.UpdateAsync(r);
                await uow.CompleteAsync();
            }

            var ngayDau = run.CheckpointDate?.AddDays(1) ?? run.FromDate;
            var soNgayLoi = 0;
            string? maLoi = null;

            // Pha 2: mỗi ngày một UoW — ghi dữ liệu + GhiCuaSo cùng commit.
            for (var ngay = ngayDau; ngay <= run.ToDate; ngay = ngay.AddDays(1))
            {
                try
                {
                    using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

                    var r = await repo.GetAsync(runId);
                    var tongHop = sp.GetRequiredService<TongHopBaoCaoService>();

                    var soDong = run.Projection == RepProjection.TonKho
                        ? await tongHop.ChupTonKhoAsync(ngay)
                        : await tongHop.ChayKhoangAsync(ngay);

                    r.GhiCuaSo(ngay, soDong);
                    await repo.UpdateAsync(r);
                    await uow.CompleteAsync();

                    metrics.ReportingRowsWritten.Add(soDong, Tag(run.Projection));
                    metrics.ReportingReplaceConflict.Add(1, Tag(run.Projection));
                }
                catch (Exception ex)
                {
                    soNgayLoi++;
                    maLoi = ex is BusinessException be ? be.Code : ex.GetType().Name;

                    metrics.ReportingProjectionFailed.Add(
                        1,
                        Tag(run.Projection),
                        new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, "error"));

                    Logger.LogError(ex,
                        "JOB ReportRebuildWorker: lượt {RunId} lỗi ở cửa sổ {Ngay} — dừng (đã commit {Done}/{Total} cửa sổ).",
                        runId, ngay, run.CheckpointDate, run.ToDate);
                    break;
                }
            }

            // Pha 3: trạng thái terminal trong UoW RIÊNG (không bị rollback cùng lỗi cửa sổ).
            using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                var r = await repo.GetAsync(runId);
                var now = dongHo.Now.ToUniversalTime();

                if (soNgayLoi == 0 && r.WindowsDone == r.WindowsTotal)
                {
                    r.HoanTat(now);
                    Logger.LogInformation("JOB ReportRebuildWorker: lượt {RunId} XONG ({Rows} dòng).", runId, r.RowsWritten);
                }
                else if (r.WindowsDone > 0)
                {
                    r.MotPhan(maLoi ?? ReportingErrorCodes.ChuyenTrangThaiKhongHopLe);
                    Logger.LogWarning("JOB ReportRebuildWorker: lượt {RunId} MỘT PHẦN ({Done}/{Total}).",
                        runId, r.WindowsDone, r.WindowsTotal);
                }
                else
                {
                    r.ThatBai(maLoi ?? ReportingErrorCodes.ChuyenTrangThaiKhongHopLe, now);
                    Logger.LogWarning("JOB ReportRebuildWorker: lượt {RunId} THẤT BẠI.", runId);
                }

                await repo.UpdateAsync(r);
                await uow.CompleteAsync();

                var mocRepo = sp.GetRequiredService<IRepository<RptWatermark, Guid>>();
                var mocs = await mocRepo.GetListAsync();
                var mocMoiNhat = mocs
                    .Where(x => x.LastRunAtUtc is not null)
                    .Select(x => x.LastRunAtUtc!.Value)
                    .DefaultIfEmpty(now)
                    .Max();

                var lag = (long)Math.Max(0, (now - mocMoiNhat).TotalSeconds);
                metrics.SetReportingWatermarkLagSeconds(lag, run.Projection.MaChuoi());
            }
        }
    }

    private static KeyValuePair<string, object?> Tag(RepProjection projection)
        => new(eCommerceMetricTags.Projection, projection.MaChuoi());
}
