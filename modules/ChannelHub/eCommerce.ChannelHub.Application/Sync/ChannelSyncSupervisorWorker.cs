using System.Collections.Concurrent;
using eCommerce.ChannelHub.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;

namespace eCommerce.ChannelHub.Sync;

/// <summary>
/// Supervisor: worker DUY NHẤT chạy cố định. Mỗi nhịp nó:
///   1. đọc danh sách shop đang hoạt động,
///   2. đăng ký / cập nhật một <b>dynamic background worker</b> cho mỗi shop,
///   3. huỷ worker của shop đã ngắt kết nối.
///
/// 🎯 Vì sao dùng dynamic worker thay vì một worker quét tất cả:
///   • mỗi shop có nhịp đồng bộ và quota riêng (Shopee/Lazada giới hạn theo shop),
///   • một shop lỗi/treo không chặn các shop khác,
///   • shop mới kết nối được nhận việc ngay mà không cần deploy lại.
///
/// ⚠️ Lưu ý đã biết của ABP: handler của dynamic worker chỉ nằm TRONG BỘ NHỚ, không được
/// persist qua lần restart ứng dụng. Vì supervisor chạy định kỳ và AddAsync ghi đè theo tên,
/// việc đăng ký lại sau restart là tự động — đây chính là lý do thiết kế này an toàn.
/// </summary>
public class ChannelSyncSupervisorWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Sổ đăng ký: tên worker → NHỊP (ms) đã đăng ký, để biết worker nào cần tạo lại khi nhịp đổi.</summary>
    private static readonly ConcurrentDictionary<string, int> RegisteredWorkers = new();

    private readonly ILogger<ChannelSyncSupervisorWorker> _logger;

    public ChannelSyncSupervisorWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChannelSyncSupervisorWorker> logger)
        : base(timer, serviceScopeFactory)
    {
        _logger = logger;

        // Giá trị mặc định; mỗi nhịp sẽ đọc lại từ options.
        Timer.Period = 60_000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var options = workerContext.ServiceProvider.GetRequiredService<IOptions<ChannelHubOptions>>().Value;
        Timer.Period = Math.Max(5, options.SupervisorPeriodSeconds) * 1000;

        var shopRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<ChannelShop, Guid>>();
        var workerManager = workerContext.ServiceProvider.GetRequiredService<IDynamicBackgroundWorkerManager>();

        var activeShops = await shopRepository.GetListAsync(x => x.IsActive);

        var activeWorkerNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var shop in activeShops)
        {
            var workerName = BuildWorkerName(shop);
            activeWorkerNames.Add(workerName);

            var periodSeconds = shop.SyncIntervalSeconds ?? options.DefaultShopSyncIntervalSeconds;
            var periodMs = Math.Max(10, periodSeconds) * 1000;

            var shopId = shop.Id;

            // 🚨 C10 — SỬA LỖI STARVATION (ĐÃ ĐO BẰNG CHẠY THẬT, 13/10/2026):
            //
            // Trước đây gọi AddAsync **MỖI NHỊP** với suy nghĩ "ghi đè là idempotent". Thực tế ABP
            // **THAY THẾ** worker (log: "Replaced existing dynamic worker") ⇒ **timer bị reset về 0**
            // mỗi lần ⇒ nếu nhịp supervisor ≤ nhịp worker thì worker **KHÔNG BAO GIỜ chạy**.
            //
            // Đo thực tế: supervisor 5s + worker 10s ⇒ 8 lần replace, **0 lần worker chạy**;
            //             supervisor 600s + worker 10s ⇒ worker chạy **ngay**.
            // ⚠️ Với cấu hình MẶC ĐỊNH (SupervisorPeriodSeconds=60, DefaultShopSyncIntervalSeconds=300)
            //    worker sẽ **KHÔNG BAO GIỜ** chạy ⇒ đồng bộ sàn hoàn toàn không hoạt động.
            //
            // Nay CHỈ đăng ký khi worker CHƯA có, hoặc NHỊP ĐÃ ĐỔI (để admin đổi SyncIntervalSeconds
            // vẫn có hiệu lực).
            if (RegisteredWorkers.TryGetValue(workerName, out var registeredPeriodMs)
                && registeredPeriodMs == periodMs)
            {
                continue;
            }

            await workerManager.AddAsync(
                workerName,
                new DynamicBackgroundWorkerSchedule { Period = periodMs },
                async (executionContext, cancellationToken) =>
                {
                    var syncer = executionContext.ServiceProvider.GetRequiredService<ChannelShopSyncer>();

                    try
                    {
                        await syncer.SyncAsync(shopId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // Handler của dynamic worker không có ai bắt lỗi phía trên ⇒ phải tự log.
                        _logger.LogError(ex, "Worker đồng bộ shop {ChannelShopId} lỗi.", shopId);
                    }
                });

            RegisteredWorkers[workerName] = periodMs;
        }

        // Huỷ worker của shop đã ngắt kết nối / bị tắt.
        foreach (var workerName in RegisteredWorkers.Keys)
        {
            if (activeWorkerNames.Contains(workerName))
            {
                continue;
            }

            await workerManager.RemoveAsync(workerName);
            RegisteredWorkers.TryRemove(workerName, out _);

            _logger.LogInformation("Đã huỷ worker {WorkerName} (shop không còn hoạt động).", workerName);
        }
    }

    private static string BuildWorkerName(ChannelShop shop) => $"channel-sync:{shop.ChannelCode}:{shop.ExternalShopId}";
}
