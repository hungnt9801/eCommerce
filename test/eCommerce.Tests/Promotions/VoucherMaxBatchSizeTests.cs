using eCommerce.Promotions.Contracts;
using eCommerce.Tests.Pricing.HaTang;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Tracing;
using Volo.Abp.Validation;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`MaxBatchSize` — `D-302`** (`PRO-001-arch` §3.6): trần sinh lô mã.
/// <list type="bullet">
///   <item>hàm THUẦN `KepMaxBatchSize` kẹp `1…100000`;</item>
///   <item>`DocMaxBatchSize` mặc định `10000`, giá trị rỗng/gõ sai ⇒ mặc định (không ném);</item>
///   <item>`GenerateAsync` vượt trần ⇒ **NÉM `0108` TRƯỚC** khi ghi dòng nào.</item>
/// </list>
/// Dựng `VoucherAdminAppService` bằng `new` + repo in-memory (`Pricing/HaTang/RepoGia`) — KHÔNG host ABP/DB.
/// </summary>
public class VoucherMaxBatchSizeTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime T0 = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class CorrGia : ICorrelationIdProvider
    {
        public string Get() => "test-correlation";
        public IDisposable Change(string? correlationId) => new Khong();
        private sealed class Khong : IDisposable { public void Dispose() { } }
    }

    private sealed class TenantGia : ICurrentTenant
    {
        public bool IsAvailable => true;
        public Guid? Id { get; set; } = TenantId;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => new Khong();
        private sealed class Khong : IDisposable { public void Dispose() { } }
    }

    private sealed class Bo
    {
        public RepoGia<Promotion> Promotions { get; } = new();
        public RepoGia<VoucherCode> Vouchers { get; } = new();
        public VoucherAdminAppService Sut { get; }
        public Guid PromotionId { get; }

        public Bo(string? maxBatchSetting)
        {
            var promotion = Promotion.TaoMoi(
                Guid.NewGuid(), TenantId, "KM1", "Khuyến mãi", PromotionKind.Discount,
                T0, null, 0, false, null,
                PromotionDiscountKind.FixedAmount, 0m, null,
                null, "VND", null, null);
            promotion.Approve();
            Promotions.Them(promotion);
            PromotionId = promotion.Id;

            var guid = new GuidGia();
            Sut = new VoucherAdminAppService(
                Vouchers, Promotions, new RepoGia<PromotionRedemption>(), guid, new DongHoGia(T0), new CorrGia());

            var cauHinh = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (maxBatchSetting is not null)
            {
                cauHinh[PromotionSettingNames.MaxBatchSize] = maxBatchSetting;
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGuidGenerator>(guid);
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<ICurrentTenant>(new TenantGia());
            services.AddSingleton<ISettingProvider>(new SettingGia(cauHinh));
            Sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());
        }

        public GenerateVouchersDto Vao(int quantity, int maxUsage = 1)
            => new() { PromotionId = PromotionId, Quantity = quantity, MaxUsage = maxUsage, Length = 12 };
    }

    // ── Hàm THUẦN ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-100, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(100000, 100000)]
    [InlineData(100001, 100000)]
    [InlineData(999999, 100000)]
    public void D302_KepMaxBatchSize_kep_1_100000(int tho, int mongDoi)
        => Assert.Equal(mongDoi, PromotionSettings.KepMaxBatchSize(tho));

    [Theory]
    [InlineData(null, 10000)]
    [InlineData("", 10000)]
    [InlineData("   ", 10000)]
    [InlineData("abc", 10000)]
    [InlineData("-5", 10000)]
    [InlineData("0", 1)]
    [InlineData("1", 1)]
    [InlineData("50", 50)]
    [InlineData("10000", 10000)]
    [InlineData("200000", 100000)]
    [InlineData("50.9", 50)] // floor
    public void D302_DocMaxBatchSize_doc_setting_an_toan(string? tho, int mongDoi)
        => Assert.Equal(mongDoi, PromotionSettings.DocMaxBatchSize(tho));

    [Fact]
    public void D302_hang_so_tran_CUNG_100000()
        => Assert.Equal(100000, PromotionSettings.MaxBatchSizeCung);

    // ── GenerateAsync — 0108 TRƯỚC khi ghi ─────────────────────────────────────────────────────

    [Fact]
    public async Task D302_vuot_tran_mac_dinh_10000__nem_0108_va_0_dong_ghi()
    {
        var bo = new Bo(maxBatchSetting: null); // mặc định 10000

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.GenerateAsync(bo.Vao(quantity: 10_001)));

        Assert.Equal(PromotionErrorCodes.BatchSizeExceeded, ex.Code); // 0108
        Assert.Equal(0, bo.Vouchers.SoLanChen);                       // 0 dòng ghi
    }

    [Fact]
    public async Task D302_setting_1_quantity_2__nem_0108_va_0_dong_ghi()
    {
        var bo = new Bo(maxBatchSetting: "1");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.GenerateAsync(bo.Vao(quantity: 2)));

        Assert.Equal(PromotionErrorCodes.BatchSizeExceeded, ex.Code);
        Assert.Equal(0, bo.Vouchers.SoLanChen);
    }

    [Fact]
    public async Task D302_setting_go_sai__dung_mac_dinh_10000__quantity_10001_nem_0108()
    {
        var bo = new Bo(maxBatchSetting: "khong-phai-so");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.GenerateAsync(bo.Vao(quantity: 10_001)));

        Assert.Equal(PromotionErrorCodes.BatchSizeExceeded, ex.Code);
        Assert.Equal(0, bo.Vouchers.SoLanChen);
    }

    [Fact]
    public async Task D302_quantity_trong_tran__sinh_du_so_dong()
    {
        var bo = new Bo(maxBatchSetting: "5");

        var kq = await bo.Sut.GenerateAsync(bo.Vao(quantity: 3));

        Assert.Equal(3, kq.Count);
        Assert.Equal(3, bo.Vouchers.SoLanChen);
    }

    [Fact]
    public async Task D302_quantity_0__nem_AbpValidationException_0_dong()
    {
        var bo = new Bo(maxBatchSetting: "5");

        await Assert.ThrowsAsync<AbpValidationException>(
            () => bo.Sut.GenerateAsync(bo.Vao(quantity: 0)));

        Assert.Equal(0, bo.Vouchers.SoLanChen);
    }
}
