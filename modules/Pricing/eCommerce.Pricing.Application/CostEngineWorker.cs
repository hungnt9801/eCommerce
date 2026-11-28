using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — JOB QUÉT ĐỊNH KỲ ĐỘNG CƠ GIÁ VỐN** (`PRC-001-arch` §5.2; `D-323` chọn job quét + command).
///
/// 📌 **Theo đúng khuôn worker đã cơ chế hoá** (`BackgroundWorkerCoCheTests`):
///   • **log vô điều kiện** ở **CÂU LỆNH ĐẦU TIÊN** của `DoWorkAsync` ⇒ *"im lặng"* **luôn** nghĩa là *"không chạy"*;
///   • **bọc trong `UnitOfWork` + scope** (`AsyncPeriodicBackgroundWorkerBase` không tự mở UoW);
///   • **`Timer.RunOnStart = true`** ⇒ chạy ngay sau restart (bài học `CHN-002`).
///
/// 🚨 **KHÔNG gọi `CostEngineAppService`** (có `[Authorize]`): worker không có principal ⇒
/// `AbpAuthorizationException`. Worker resolve **`CostEngineService`** (dịch vụ miền) trực tiếp.
///
/// 🔒 **An toàn cho test/cổng:** mặc định `Pricing:GiaVonDongCo = false` ⇒ `ChayAsync` trả `null` (no-op);
/// lỗi bất kỳ **KHÔNG** làm chết worker/app (chỉ `LogWarning`).
/// 📌 **Khoảng quét: `[DauLichSu, hôm nay]`** (ngày kinh doanh UTC+7) — nạp **toàn bộ lịch sử** để bình quân
/// di động đúng; `from` của lượt chỉ là mốc dưới của **kỳ được ghi/đối soát** (`D-325`, `CostEngineService.DauLichSu`).
/// Mỗi ngày MỘT `RunKey` (`cost:{tenant}:2000-01-01:{hôm nay}:Schedule`) ⇒ idempotent trong ngày.
/// </summary>
public class CostEngineWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp quét mặc định: **30 phút** — đủ nhanh cho báo cáo trong ngày, không tạo tải liên tục.</summary>
    public const int NhipMacDinhPhut = 30;

    /// <summary>Múi giờ kinh doanh VN = UTC+7 (`D-177` ③).</summary>
    private const int LechGioVnSoVoiUtc = 7;

    public CostEngineWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhPhut * 60 * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("JOB GIÁ VỐN: bắt đầu một lượt quét (mốc {Moc}). [PRC001-COST-JOB-BAT-DAU]", DateTime.UtcNow);

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var dongCo = scope.ServiceProvider.GetRequiredService<CostEngineService>();

        // 🆕 `D-324`: `CostEngineService` TỰ mở 3 UoW riêng (P1 sổ run / P2 tính / P3 chốt) và **commit P1
        // TRƯỚC** khi tính ⇒ sổ run phải BỀN qua rollback của P2. Vì vậy UoW bao ngoài ở worker **KHÔNG** được
        // transactional (transactional ở đây sẽ bọc/rollback cả P1 ⇒ mất dấu `Loi`/`DangChay` — phá K06-08b).
        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);

        // Ngày kinh doanh UTC+7.
        var homNay = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(LechGioVnSoVoiUtc));

        // 📌 `D-325`: tính trên TOÀN BỘ lịch sử (`from` = mốc đầu lịch sử, KHÔNG phải "hôm qua") — bình quân
        //    di động phụ thuộc toàn bộ lịch sử; `from` chỉ là mốc dưới của kỳ được ghi/đối soát.
        var tu = CostEngineService.DauLichSu;

        try
        {
            var run = await dongCo.ChayAsync(tu, homNay, "Schedule");

            await uow.CompleteAsync();

            if (run is null)
            {
                Logger.LogInformation("JOB GIÁ VỐN: động cơ ĐANG TẮT ⇒ không có việc. [PRC001-COST-JOB-TAT]");
            }
            else
            {
                Logger.LogInformation(
                    "JOB GIÁ VỐN: xong lượt {RunKey} — {TrangThai}, +{Upserted} dòng, bỏ {Skipped} kỳ đã duyệt. [PRC001-COST-JOB-XONG]",
                    run.RunKey, run.TrangThai, run.RowsUpserted, run.RowsSkippedApproved);
            }
        }
        catch (Exception ex)
        {
            // 📌 KHÔNG ném tiếp: một lượt lỗi (vd cấu hình sai ⇒ `0014`) không được làm chết worker/app.
            //    Lỗi cấu hình đã fail-closed ở `CostEngineService`; ở đây chỉ ghi cảnh báo để người vận hành thấy.
            Logger.LogWarning(ex, "JOB GIÁ VỐN: lượt quét LỖI ⇒ bỏ qua lượt này (worker vẫn sống).");
        }
    }
}
