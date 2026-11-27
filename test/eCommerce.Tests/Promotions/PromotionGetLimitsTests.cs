using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Engine;
using eCommerce.Promotions.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Volo.Abp.Timing;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// `D-288` — `GetAsync` trả `PromotionDetailDto.Limits` (**4 trần cây**) đọc từ ABP Setting qua **một cửa**
/// `DocLimitsAsync`. `D-33`: FE dùng nguồn này, **không** chép hằng số `64/8/32/1024`.
///
/// 📌 Dựng `PromotionAppService` bằng `new` + repo in-memory (`RepoGia` khoá `Guid`, `RepoKhoaChuoiGia` khoá chuỗi)
/// + `SettingGia` (đăng ký `ISettingProvider` trên `LazyServiceProvider`), engine/registry thật từ `HaTang`,
/// khoá phân tán giả. KHÔNG host ABP, KHÔNG PostgreSQL (khuôn `D-229`).
/// </summary>
public class PromotionGetLimitsTests
{
    private sealed class KhoaGia : IAbpDistributedLock
    {
        public Task<IAbpDistributedLockHandle?> TryAcquireAsync(
            string name, TimeSpan timeout, CancellationToken cancellationToken = default)
            => Task.FromResult<IAbpDistributedLockHandle?>(new Handle());

        private sealed class Handle : IAbpDistributedLockHandle
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class Bo
    {
        public PromotionAppService Sut { get; }

        public Bo(Dictionary<string, string?>? settings)
        {
            var promotions = new eCommerce.Tests.Pricing.HaTang.RepoGia<Promotion>();
            var conditions = new eCommerce.Tests.Pricing.HaTang.RepoGia<PromotionCondition>();
            var ruleCosts = new RepoKhoaChuoiGia<PromotionRuleCost>();
            var redemptions = new eCommerce.Tests.Pricing.HaTang.RepoGia<PromotionRedemption>();
            var guid = new GuidGia();

            Sut = new PromotionAppService(
                promotions,
                conditions,
                ruleCosts,
                redemptions,
                HaTang.Engine(new DataSourceGia()),
                HaTang.Registry(),
                new KhoaGia(),
                guid,
                new DongHoGia(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc)));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGuidGenerator>(guid);
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<ISettingProvider>(new SettingGia(settings));
            Sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());

            // Nạp sẵn MỘT promotion để `GetAsync` tìm thấy (trạng thái Draft, giá trị hợp lệ).
            promotions.Them(Promotion.TaoMoi(
                Guid.NewGuid(), null, "GET-LIMITS", "KM giới hạn", PromotionKind.Discount,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, priority: 0, isCumulative: false,
                channelScope: null, PromotionDiscountKind.FixedAmount, discountValue: 0m,
                maxDiscountAmount: null, minOrderAmount: null, currency: "VND",
                maxUsageTotal: null, maxUsagePerCustomer: null));

            PromotionId = promotions.TatCa.Single().Id;
        }

        public Guid PromotionId { get; }
    }

    [Fact]
    public async Task D288_setting_rong__Limits_bang_mac_dinh_64_8_32_1024()
    {
        var bo = new Bo(settings: null);

        var dto = await bo.Sut.GetAsync(bo.PromotionId);

        Assert.Equal(64, dto.Limits.MaxNodes);
        Assert.Equal(8, dto.Limits.MaxDepth);
        Assert.Equal(32, dto.Limits.MaxLeaves);
        Assert.Equal(1024, dto.Limits.MaxDeclaredCombinations);
    }

    [Fact]
    public async Task D288_setting_MaxNodes_10_MaxDepth_5__Limits_phan_anh_dung_con_lai_mac_dinh()
    {
        var bo = new Bo(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PromotionSettingNames.MaxNodes] = "10",
            [PromotionSettingNames.MaxDepth] = "5",
        });

        var dto = await bo.Sut.GetAsync(bo.PromotionId);

        Assert.Equal(10, dto.Limits.MaxNodes);
        Assert.Equal(5, dto.Limits.MaxDepth);
        Assert.Equal(32, dto.Limits.MaxLeaves);
        Assert.Equal(1024, dto.Limits.MaxDeclaredCombinations);
    }
}
