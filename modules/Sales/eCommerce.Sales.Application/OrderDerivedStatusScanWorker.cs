using eCommerce.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.Application.Sales;

/// <summary>
/// 🚨 **JOB QUÉT `DerivedStatus`** (`SAL-003` NFR-011) — chạy **hằng ngày**, đối chiếu cột `Order.Status`
/// (giá trị đã lưu) với giá trị **suy ra** từ 4 trục.
///
/// 🔑 **VÌ SAO CẦN (không đủ test):** `Status` là giá trị **dẫn xuất** — nếu ai đó UPDATE SQL tay, hoặc một
/// đường ghi cũ còn sót, thì `Status` **lệch** với 4 trục. 📌 Test chỉ chứng minh đường code *hiện tại* đúng;
/// nó **không** chứng minh **dữ liệu đang nằm trong DB** đúng. Job này là phép đo trên **dữ liệu thật**.
///
/// 🔒 **Luật suy ra nằm ở `OrderStateMachine.TinhTrangThaiSuyRa` (hàm THUẦN)** — job **không** chép lại luật;
/// nếu chép, hai bản sẽ lệch khi ai đó sửa một bên, và lệch ở đây là **báo động giả** hoặc **bỏ sót**.
///
/// 📌 **Theo khuôn worker đã cơ chế hoá** (`BackgroundWorkerCoCheTests`):
///   • log vô điều kiện ở **câu lệnh đầu tiên**;
///   • `Timer.RunOnStart = true`;
///   • khoá phân tán (dấu hai instance quét cùng lúc — vô hại nhưng tốn DB);
///   • scope + `UnitOfWork` vì đọc repository.
/// ⚠️ Job **chỉ ĐỌC** — không sửa `Status` (sửa tự động sẽ che mất lỗi gốc; người vận hành phải xem log).
/// </summary>
public class OrderDerivedStatusScanWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp quét: **24 giờ** (NFR-011: *"job quét hằng ngày"*).</summary>
    public const int NhipMacDinhGiay = 86_400;

    /// <summary>Tên khoá phân tán (**không** tiền tố `ecommerce:` — `KeyPrefix` đã thêm).</summary>
    public const string TenKhoa = "order:derived-status-scan";

    /// <summary>Trần số dòng lệch **in ra log** một lượt — tránh log phình khi dữ liệu hỏng diện rộng.</summary>
    public const int TranInLog = 50;

    public OrderDerivedStatusScanWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 LOG VÔ ĐIỀU KIỆN — CÂU LỆNH ĐẦU TIÊN (`BackgroundWorkerCoCheTests`).
        Logger.LogInformation("JOB SAL-003: bắt đầu quét DerivedStatus (mốc {Moc}). [ORDER-SCAN-BAT-DAU]", DateTime.UtcNow);

        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await distributedLock.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(60));

        if (handle is null)
        {
            Logger.LogDebug("JOB SAL-003: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", TenKhoa);
            return;
        }

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(requiresNew: true, isTransactional: false);

        var donHang = scope.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>();

        var tatCa = await donHang.GetListAsync();

        var lech = tatCa
            .Select(don => new
            {
                don.Id,
                don.Number,
                Luu = don.Status,
                Dung = OrderStateMachine.TinhTrangThaiSuyRa(don.FulfillmentStatus, don.ReturnStatus, don.IsCancelled)
            })
            .Where(x => x.Luu != x.Dung)
            .ToList();

        if (lech.Count == 0)
        {
            Logger.LogInformation(
                "JOB SAL-003: đã quét {Tong} đơn — KHÔNG có đơn nào lệch. [ORDER-SCAN-KHONG-LECH]", tatCa.Count);
        }
        else
        {
            // 🚨 `Warning` (không `Information`): lệch nghĩa là **dữ liệu đã bị ghi sai** ở một đường nào đó
            // — hoặc có UPDATE SQL tay. 📌 Log phải nêu **số đơn**, không chỉ "có lệch".
            foreach (var dong in lech.Take(TranInLog))
            {
                Logger.LogWarning(
                    "JOB SAL-003: ĐƠN LỆCH DerivedStatus — {Number} ({Id}): lưu = {Luu}, đúng = {Dung}. [ORDER-SCAN-LECH]",
                    dong.Number, dong.Id, dong.Luu, dong.Dung);
            }

            if (lech.Count > TranInLog)
            {
                Logger.LogWarning(
                    "JOB SAL-003: còn {Con} đơn lệch KHÔNG in (trần log {Tran}). [ORDER-SCAN-CAT-BOT]",
                    lech.Count - TranInLog, TranInLog);
            }

            Logger.LogWarning(
                "JOB SAL-003: quét {Tong} đơn ⇒ {Lech} đơn LỆCH DerivedStatus. " +
                "🔒 Job chỉ ĐỌC: sửa dữ liệu phải có người quyết định, không tự sửa. [ORDER-SCAN-CO-LECH]",
                tatCa.Count, lech.Count);
        }

        await uow.CompleteAsync();

        // 🚨 NEO KẾT THÚC — phân biệt *"job chạy XONG"* với *"job chạy rồi NÉM"* (bài học `SAL-005` §19.5).
        Logger.LogInformation("JOB SAL-003: xong lượt quét (mốc {Moc}). [ORDER-SCAN-XONG]", DateTime.UtcNow);
    }
}
