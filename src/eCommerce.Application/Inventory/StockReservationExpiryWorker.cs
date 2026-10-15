using Volo.Abp.Timing;
using Volo.Abp.Uow;
using eCommerce.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace eCommerce.Application.Inventory;

/// <summary>
/// 🚨 **JOB NHẢ CHỖ GIỮ HÀNG QUÁ HẠN** (`INV-002`) — chạy **định kỳ**.
///
/// **VÌ SAO BẮT BUỘC PHẢI CÓ (không phải "tiện thì thêm"):** không có job này thì giữ chỗ **vô thời hạn trên thực tế**:
/// khách bỏ ngang giữa lúc thanh toán ⇒ hàng bị **giữ mãi** ⇒ người khác **không mua được** ⇒
/// **mất doanh thu mà KHÔNG có lỗi nào** để lần ra (không exception, không log, chỉ là *"hết hàng"*).
/// Trước vòng này, việc nhả chỗ chỉ xảy ra khi có người **gọi API thủ công** — ở production điều đó nghĩa là **không bao giờ**.
///
/// 📌 **Theo đúng mẫu đã kiểm chứng** `TaxUnclassifiedSkusWorker`:
///   • `Timer.RunOnStart = true` — 🚨 **QUAN TRỌNG**: sau mỗi lần deploy/restart, hàng đã quá hạn **từ trước** phải được
///     nhả **ngay**, không chờ hết nhịp (bài học `CHN-002`: supervisor thiếu `RunOnStart` ⇒ không chạy cho tới nhịp đầu);
///   • **khoá phân tán** để nhiều instance **không** nhả chồng (nhả hai lần sẽ bị `0030` chặn, nhưng vẫn tạo
///     **log nhiễu và transaction thừa**);
///   • **KHÔNG** khai lại `Logger` (lớp cơ sở đã có — bài học CS0108).
/// </summary>
public class StockReservationExpiryWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp quét: **1 phút**. TTL giữ chỗ mặc định là 15 phút ⇒ sai số tối đa ~7% thời gian giữ; đủ tốt để hàng về kho nhanh.</summary>
    public const int NhipMacDinhGiay = 60;

    /// <summary>
    /// Tên khoá phân tán. ⚠️ **KHÔNG** thêm tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` đã thêm
    /// (bài học C11/C13: tiền tố bị nhân đôi thành `ecommerce:ecommerce:…`).
    /// </summary>
    public const string TenKhoa = "inventory:reservation-expiry";

    public StockReservationExpiryWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 Chạy NGAY lần đầu: hàng đã quá hạn TỪ TRƯỚC lúc deploy phải được nhả ngay, không chờ 1 nhịp.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("JOB NHẢ CHỖ: bắt đầu quét lượt giữ chỗ quá hạn (mốc {Moc}).", DateTime.UtcNow);
        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await distributedLock.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(30));

        if (handle is null)
        {
            // Instance khác đang quét ⇒ bỏ lượt này (bình thường, KHÔNG phải lỗi).
            Logger.LogDebug("JOB NHẢ CHỖ: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", TenKhoa);
            return;
        }

        // 📌 LOG VÔ ĐIỀU KIỆN (mỗi lượt) — 🚨 **BÀI HỌC ĐÃ TRẢ GIÁ (vòng 35)**: nếu chỉ log khi `soDaNha > 0` thì
        // *"im lặng"* **KHÔNG** phân biệt được *"job không chạy"* với *"job chạy mà không có việc"*. Chính dòng log
        // này đã phát hiện worker **chạy rồi NÉM** `ObjectDisposedException` ⇒ đổi từ **im lặng** (không ai biết)
        // thành **ồn ào** (thấy ngay trong 1 lượt chạy).

        // 🚨🚨 **BẮT BUỘC: BỌC TRONG UNIT OF WORK + SCOPE RIÊNG.**
        // `AsyncPeriodicBackgroundWorkerBase` **KHÔNG** tự mở UoW (khác request HTTP — vốn đã có UoW của ABP).
        // Resolve `StockReservationService` (scoped, giữ repository → `DbContext`) mà **không** có UoW ⇒
        // `DbContext` bị **dispose** trước khi dùng ⇒ ném `ObjectDisposedException: Cannot access a disposed
        // context instance` ⇒ **job không làm gì cả mà không ai biết** (ĐÃ ĐO được ở vòng 35 — `inv-001` §26.3).
        // 📌 Đúng hình mẫu *"job im lặng"*: không exception nào tới người dùng, chỉ là **hàng bị giữ vĩnh viễn**.
        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var service = scope.ServiceProvider.GetRequiredService<StockReservationService>();

        var soDaNha = await service.NhaChoHetHanAsync(clock.Now);

        await uow.CompleteAsync();

        if (soDaNha > 0)
        {
            Logger.LogWarning("JOB NHẢ CHỖ: đã nhả {So} lượt giữ chỗ QUÁ HẠN ⇒ hàng trở lại kho bán được.", soDaNha);
        }
    }
}
