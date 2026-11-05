using eCommerce.Kernel.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.EntityFrameworkCore.Workers;

/// <summary>
/// **ĐO SỐ BẢN GHI OUTBOX ĐANG TỒN ĐỌNG** (`XC-002` NFR-003 · `messaging_outbox_pending`).
///
/// 🔑 **VÌ SAO ĐẶT Ở PROJECT EF (không phải Application):** kiểu `OutgoingEventRecord` nằm trong
/// `Volo.Abp.EntityFrameworkCore`; đưa nó lên Application sẽ buộc Application → EntityFrameworkCore
/// (**vi phạm phân tầng**). 📌 Worker này là **hạ tầng** (đọc bảng outbox), không phải nghiệp vụ.
///
/// 🔑 **VÌ SAO CẦN METRIC (nợ từ 13/10/2026):** metric đã đăng ký nhưng *"chưa có nơi phát"* vì chưa có
/// outbox. Từ 24/10/2026 outbox **có thật** (`XC-002` §17) ⇒ chỉ còn thiếu **phép đếm**. 📌 Không có nó,
/// câu hỏi *"relay có tụt lại không?"* chỉ trả lời được bằng SQL tay; con số `0` trên biểu đồ có thể là
/// **lời nói dối** (`0` vì **chưa ai đo**).
///
/// 🔒 **GAUGE:** giá trị = số **hiện tại** (tăng/giảm theo tải). ⚠️ Tồn đọng **tăng đều** ⇒ relay chết/chậm.
/// 🚨 Metric **không** thay thế test "publish lỗi ⇒ phát lại" (vẫn là nợ riêng).
/// </summary>
public class OutboxPendingWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp **60 giây** — đủ nhanh để thấy relay tụt lại, đủ thưa để không tốn DB.</summary>
    public const int NhipMacDinhGiay = 60;

    public OutboxPendingWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 LOG VÔ ĐIỀU KIỆN — câu lệnh đầu tiên (`BackgroundWorkerCoCheTests`).
        Logger.LogInformation("JOB OUTBOX: bắt đầu đếm bản ghi tồn đọng (mốc {Moc}). [OUTBOX-PENDING-BAT-DAU]", DateTime.UtcNow);

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);

        var outbox = scope.ServiceProvider.GetRequiredService<IRepository<OutgoingEventRecord, Guid>>();
        var metrics = scope.ServiceProvider.GetService<eCommerceMetrics>();

        var soBanGhi = await outbox.GetCountAsync();

        // 📊 Ghi gauge — `null` khi test/không đăng ký metric ⇒ bỏ qua, không ném.
        metrics?.SetMessagingOutboxPending(soBanGhi);

        if (soBanGhi > 0)
        {
            // 🚨 `Warning`: outbox tồn đọng nghĩa relay **chưa phát**; kéo dài ⇒ sự kiện không đến consumer
            // dù dữ liệu nghiệp vụ đã commit. 📌 Không phải "bình thường".
            Logger.LogWarning(
                "JOB OUTBOX: {So} bản ghi ĐANG TỒN ĐỌNG ⇒ relay chưa phát hết. [OUTBOX-PENDING-CO-TON]",
                soBanGhi);
        }
        else
        {
            Logger.LogInformation("JOB OUTBOX: 0 bản ghi tồn đọng (relay theo kịp). [OUTBOX-PENDING-RONG]");
        }

        await uow.CompleteAsync();

        Logger.LogInformation("JOB OUTBOX: xong lượt đếm (mốc {Moc}). [OUTBOX-PENDING-XONG]", DateTime.UtcNow);
    }
}
