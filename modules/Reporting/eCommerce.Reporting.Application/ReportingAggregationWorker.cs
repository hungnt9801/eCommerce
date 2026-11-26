using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Reporting;

/// <summary>
/// **JOB TỔNG HỢP READ-MODEL ĐỊNH KỲ** (`WP-08`, `07 §WP-08`: *"Job tổng hợp theo watermark"*).
///
/// 🔑 **VÌ SAO LÀ JOB (không tính lúc mở báo cáo):** tổng hợp là việc **đọc toàn bộ chứng từ của một ngày**.
/// Tính trong request ⇒ mỗi lần mở Dashboard là một lần quét bảng chứng từ, chậm dần theo dữ liệu, và
/// **không** có chỗ nào trả lời *"số liệu mới tới ngày nào"*.
///
/// 🚨 **BA BẪY ĐÃ TRẢ GIÁ (được `BackgroundWorkerCoCheTests` chặn):**
/// <list type="number">
///   <item>**Đăng ký** (`AddBackgroundWorkerAsync`) — thiếu là job **không bao giờ chạy**, không lỗi nào.</item>
///   <item>**Log VÔ ĐIỀU KIỆN ở câu lệnh ĐẦU TIÊN** — thiếu là *"im lặng"* không phân biệt được *"job không
///     chạy"* với *"chạy mà không có việc"*.</item>
///   <item>**Worker KHÔNG gọi `*AppService`** (có `[Authorize]`): worker không có principal ⇒
///     `AbpAuthorizationException` ⇒ job **chết giữa đường** mà chỉ có log mới thấy. Worker gọi
///     <see cref="TongHopBaoCaoService"/> (dịch vụ miền, có `UnitOfWork` bọc ngoài).</item>
/// </list>
/// 📌 `RunOnStart = true`: sau mỗi lần deploy, số liệu được cập nhật **ngay** thay vì trễ một nhịp.
///
/// 🆕 **`D-275` (giữ `D-271`):** lượt `Aggregate` **có việc** ghi `RepRunLog` (`BanHang`/`TonKho`) **trong cùng
/// UoW**; lượt **lỗi** ghi **đúng 1** dòng `Failed` ở UoW **riêng** sau khi UoW chính rollback; lượt **rỗng**
/// **0 dòng**; **bỏ nhịp vì khoá** chỉ `LogWarning` (`Skipped` dự trữ).
/// </summary>
public class ReportingAggregationWorker : AsyncPeriodicBackgroundWorkerBase
{
    public const int NhipMacDinhPhut = 15;

    public ReportingAggregationWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ReportingOptions> tuyChon)
        : base(timer, serviceScopeFactory)
    {
        // ⚠️ KHÔNG khai lại `Logger` — lớp cơ sở đã có (bài học CS0108).
        var phut = tuyChon.Value.NhipJobPhut <= 0 ? NhipMacDinhPhut : tuyChon.Value.NhipJobPhut;

        Timer.Period = phut * 60 * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 **LOG VÔ ĐIỀU KIỆN Ở ĐẦU MỖI LƯỢT** — BẮT BUỘC cho mọi job (`inv-001` §27.3 + §43.4).
        Logger.LogInformation("JOB ReportingAggregationWorker: bắt đầu một lượt (mốc {Moc}).", DateTime.UtcNow);

        Exception? loiChinh = null;

        using (var scope = workerContext.ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

            using (uow)
            {
                try
                {
                    var tongHop = scope.ServiceProvider.GetRequiredService<TongHopBaoCaoService>();

                    var ketQua = await tongHop.ChayAsync(null, null, ghiAnhTonKho: true);

                    // 🆕 `D-275` — ghi `RepRunLog` cho projection có việc, TRONG CÙNG UoW trước `CompleteAsync`.
                    await GhiNhatKyThanhCongAsync(scope.ServiceProvider, ketQua);

                    await uow.CompleteAsync();

                    Logger.LogInformation(
                        "JOB ReportingAggregationWorker: xong — {SoNgay} ngày · {SoDongSuKien} dòng sự kiện · " +
                        "{SoDongTongHop} dòng tổng hợp · {SoAnh} ảnh tồn kho · rỗng={Rong} · mốc {MocBanLe}/{MocDonHang}.",
                        ketQua.SoNgay, ketQua.SoDongSuKien, ketQua.SoDongTongHop, ketQua.SoAnhTonKho,
                        ketQua.Rong, ketQua.MocBanLe, ketQua.MocDonHang);
                }
                catch (BusinessException ex) when (ex.Code == eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                {
                    // 📌 Một lượt khác đang chạy: **bỏ lượt này**, KHÔNG phải lỗi. Ghi `Warning` để vẫn nhìn thấy
                    // (nếu lặp liên tục nghĩa là nhịp quá ngắn so với thời gian tổng hợp). **0** dòng (`Skipped` dự trữ).
                    Logger.LogWarning(
                        "JOB ReportingAggregationWorker: bỏ lượt — một lượt tổng hợp khác đang chạy (khoá {Khoa}).",
                        TongHopBaoCaoService.TenKhoaPhanTan);
                }
                catch (Exception ex)
                {
                    // 🚨 KHÔNG nuốt lỗi thành "xong": nếu tổng hợp hỏng, read-model đứng ở ngày cũ và
                    // **mọi báo cáo** sẽ nói số liệu của hôm qua như hôm nay — không có dấu hiệu nào trên UI.
                    loiChinh = ex;
                    Logger.LogError(ex,
                        "JOB ReportingAggregationWorker: THẤT BẠI — read-model KHÔNG được cập nhật (báo cáo đang " +
                        "dừng ở mốc cũ). Xem log để sửa rồi chạy lại `POST /api/v1/reports/aggregate`.");
                }
            }

            // 🆕 `D-275` — dòng lỗi ghi ở UoW RIÊNG, SAU khi UoW chính đã dispose/rollback.
            if (loiChinh is not null)
            {
                await GhiDongLoiAsync(scope.ServiceProvider, uowManager, loiChinh);
            }
        }
    }

    private async Task GhiNhatKyThanhCongAsync(IServiceProvider sp, KetQuaTongHopDto ketQua)
    {
        var quyetDinh = NhatKyAggregate.QuyetDinh(ketQua, NgayHomNay(sp));

        if (quyetDinh.Count == 0)
        {
            return;   // 📌 Lượt RỖNG ⇒ 0 dòng (giữ log + metric as-built).
        }

        var repo = sp.GetRequiredService<IRepository<RepRunLog, Guid>>();
        var tenant = sp.GetRequiredService<ICurrentTenant>();
        var guid = sp.GetRequiredService<IGuidGenerator>();
        var luc = sp.GetRequiredService<IClock>().Now.ToUniversalTime();

        foreach (var q in quyetDinh)
        {
            var run = TaoRun(guid.Create(), tenant.Id, q, luc);

            run.BatDau(luc, Environment.MachineName);

            // `GhiCuaSo` từng ngày; dồn tổng số dòng vào cửa sổ cuối để bất biến RowsWritten = tổng.
            for (var ngay = q.FromDate; ngay <= q.ToDate; ngay = ngay.AddDays(1))
            {
                run.GhiCuaSo(ngay, ngay == q.ToDate ? q.RowsWritten : 0);
            }

            run.HoanTat(luc);
            await repo.InsertAsync(run);
        }
    }

    private async Task GhiDongLoiAsync(IServiceProvider sp, IUnitOfWorkManager uowManager, Exception loi)
    {
        try
        {
            using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

            var repo = sp.GetRequiredService<IRepository<RepRunLog, Guid>>();
            var tenant = sp.GetRequiredService<ICurrentTenant>();
            var guid = sp.GetRequiredService<IGuidGenerator>();
            var luc = sp.GetRequiredService<IClock>().Now.ToUniversalTime();
            var homNay = NgayHomNay(sp);

            // 📌 Lỗi là của CẢ lượt (không biết projection nào) ⇒ một dòng đại diện `BanHang`, phạm vi hôm nay.
            var q = new QuyetDinhNhatKy(RepProjection.BanHang, homNay, homNay, 1, 0);
            var run = TaoRun(guid.Create(), tenant.Id, q, luc);

            run.BatDau(luc, Environment.MachineName);
            run.ThatBai(NhatKyAggregate.MaLoi(loi), luc);

            await repo.InsertAsync(run);
            await uow.CompleteAsync();
        }
        catch (Exception loiGhi)
        {
            // 🚨 Ghi dòng lỗi cũng hỏng ⇒ LogError THỨ HAI, KHÔNG nuốt lỗi gốc (đã log ở trên).
            Logger.LogError(loiGhi,
                "JOB ReportingAggregationWorker: KHÔNG ghi được dòng RepRunLog lỗi cho lượt hỏng (xem lỗi gốc ở trên).");
        }
    }

    private static RepRunLog TaoRun(Guid runId, Guid? tenantId, QuyetDinhNhatKy q, DateTime luc)
        => new(
            runId,
            RepRunLog.TenJob.Aggregate,
            q.Projection,
            q.FromDate,
            q.ToDate,
            q.WindowsTotal,
            RepRunLog.LyDoHeThong.TongHopDinhKy,
            null,
            RepIdempotency.AggregateRun(tenantId, q.Projection, runId));

    private static DateOnly NgayHomNay(IServiceProvider sp)
    {
        var clock = sp.GetRequiredService<IClock>();
        var tuyChon = sp.GetRequiredService<IOptions<ReportingOptions>>().Value;

        return NgayKinhDoanh.TuUtc(clock.Now, tuyChon.GioLechUtc);
    }
}
