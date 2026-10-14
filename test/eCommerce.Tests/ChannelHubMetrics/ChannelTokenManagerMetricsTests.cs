using System.Diagnostics.Metrics;
using eCommerce.ChannelHub;
using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.Sync;
using eCommerce.Kernel.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.DistributedLocking;

namespace eCommerce.Tests.ChannelHubMetrics;

/// <summary>
/// Test cho **điểm phát metric trong `ChannelTokenManager`** — ca "không giành được khoá phân tán".
///
/// 🎯 Vì sao cần: `XC-003` BR-010 yêu cầu **có metric** cho ca không giành được khoá. Trước đây code chỉ
/// `LogWarning` — **log thì không đếm được**; muốn biết "khoá bị tranh chấp bao nhiêu lần" phải có metric.
/// Đây là ca **bình thường khi chạy nhiều instance**, nên nếu không đo được thì không biết hệ thống có
/// đang chạy đúng thiết kế hay không.
/// </summary>
public class ChannelTokenManagerMetricsTests
{
    private sealed class KhoaLuonBan : IAbpDistributedLock
    {
        public string? TenDaXin { get; private set; }

        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            TenDaXin = name;
            return Task.FromResult<IAbpDistributedLockHandle?>(null);   // luôn "bận"
        }
    }

    private sealed class BoGhi : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(string Ten, long GiaTri, Dictionary<string, object?> Nhan)> Ghi { get; } = [];

        public BoGhi(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var nhan = new Dictionary<string, object?>();
                foreach (var t in tags)
                {
                    nhan[t.Key] = t.Value;
                }

                Ghi.Add((instrument.Name, value, nhan));
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private static ChannelShop ShopCanRefresh()
    {
        // `AccessTokenExpiresAtUtc` ở QUÁ KHỨ ⇒ NeedsTokenRefresh = true ⇒ đi vào nhánh giành khoá.
        var shop = new ChannelShop(Guid.NewGuid(), "shopee", "SHOP-METRIC", "Shop thử metric")
        {
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        return shop;
    }

    [Fact]
    public async Task Khong_gianh_duoc_khoa_thi_PHAT_metric_lock_timeout_total()
    {
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var metrics = new eCommerceMetrics(meter);
        var ghi = new BoGhi(meter);
        using var _ = ghi;

        var khoa = new KhoaLuonBan();

        // Các phụ thuộc KHÔNG dùng tới trong nhánh này (thoát ngay khi không giành được khoá)
        // ⇒ truyền null! là chấp nhận được TRONG TEST, và tôi ghi rõ để không ai hiểu nhầm là thiết kế.
        var manager = new ChannelTokenManager(
            null!,                              // IRepository<ChannelShop, Guid>
            null!,                              // IChannelConnectorFactory
            null!,                              // IStringEncryptionService
            khoa,                               // IAbpDistributedLock  ← luôn bận
            null!,                              // IDistributedEventBus
            Options.Create(new ChannelHubOptions()),
            NullLogger<ChannelTokenManager>.Instance,
            metrics);

        var shop = ShopCanRefresh();

        var (context, refreshed) = await manager.GetValidContextAsync(shop);

        // Bỏ lượt này (không refresh song song), nhưng PHẢI đo được sự kiện.
        Assert.False(refreshed);
        Assert.Equal("SHOP-METRIC", context.ExternalShopId);

        var doDuoc = Assert.Single(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTimeoutTotal);
        Assert.Equal(1L, doDuoc.GiaTri);
        Assert.Equal("shopee", doDuoc.Nhan[eCommerceMetricTags.Channel]);
        Assert.Equal("dang-refresh", doDuoc.Nhan[eCommerceMetricTags.Reason]);

        // Khoá phải đúng quy ước (không tiền tố — bài học C13).
        Assert.False(khoa.TenDaXin!.StartsWith("ecommerce:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("token-refresh", khoa.TenDaXin);
    }

    [Fact]
    public async Task Nhan_metric_KHONG_chua_shopId_de_tranh_cardinality_cao()
    {
        using var meter = new Meter("eCommerce-Test-" + Guid.NewGuid());
        var metrics = new eCommerceMetrics(meter);
        var ghi = new BoGhi(meter);
        using var _ = ghi;

        var manager = new ChannelTokenManager(
            null!, null!, null!, new KhoaLuonBan(), null!,
            Options.Create(new ChannelHubOptions()),
            NullLogger<ChannelTokenManager>.Instance,
            metrics);

        await manager.GetValidContextAsync(ShopCanRefresh());

        var nhan = Assert.Single(ghi.Ghi, x => x.Ten == eCommerceMetricNames.LockTimeoutTotal).Nhan;
        var giaTriNhan = string.Join("|", nhan.Values.Select(v => v?.ToString()));

        // 🚨 `XC-005` BR-009: nhãn cardinality cao bị CẤM. `shopId`/`lockName` KHÔNG được có trong nhãn.
        Assert.DoesNotContain("SHOP-METRIC", giaTriNhan);
        Assert.DoesNotContain("token-refresh", giaTriNhan);
        Assert.All(nhan.Keys, k => Assert.Contains(k, eCommerceMetricTags.Allowed));
    }
}
