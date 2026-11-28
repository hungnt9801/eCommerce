using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using eCommerce.Tests.Pricing.HaTang;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🆕 **`W17-4` (`N118`) — IDEMPOTENCY ĐƯỜNG GHI GIÁ VỐN THỦ CÔNG** (`BR-PRC-001-012`; `PRC-001-arch` §5.6).
///
/// Dựng `CostPriceAdminAppService` thật bằng `new` + `RepoGia` (`D-229`), **KHÔNG** host ABP.
/// 🚨 Phần **hàng rào cuối ở DB** (unique `(TenantId, IdempotencyKey)` `NULLS NOT DISTINCT`) đo ở
/// `PostgreSqlPricingCostEngineTests` — file này đo **pre-check** ở tầng ứng dụng + fail-closed thiếu khoá.
/// </summary>
public class CostPriceAdminAppServiceTests
{
    private static readonly DateOnly Ngay1 = new(2026, 1, 1);

    private sealed class Bo
    {
        public RepoGia<CostPrice> GiaVon { get; } = new();
        public CostPriceAdminAppService Sut { get; }

        public Bo()
        {
            Sut = new CostPriceAdminAppService(GiaVon);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGuidGenerator>(new GuidGia());
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<ICurrentTenant>(new TenantGia());
            Sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());
        }
    }

    private sealed class GuidGia : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class TenantGia : ICurrentTenant
    {
        public bool IsAvailable => true;
        public Guid? Id => null;
        public string? Name => null;
        public IDisposable Change(Guid? id, string? name = null) => new KhongDoi();

        private sealed class KhongDoi : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private static CreateCostPriceDto Tao(string khoa) => new()
    {
        Sku = "SKU-1",
        AmountValue = 100_000m,
        CurrencyCode = "VND",
        EffectiveFrom = Ngay1,
        IdempotencyKey = khoa,
    };

    /// <summary>BR-012 — cùng `IdempotencyKey` ⇒ trả dòng ĐÃ tạo, **không** tạo bản ghi thứ hai (chỉ 1 lần chèn).</summary>
    [Fact]
    public async Task BR_012_cung_khoa__tra_dong_cu_khong_tao_thu_hai()
    {
        var bo = new Bo();
        var input = Tao("idem-1");

        var lan1 = await bo.Sut.CreateAsync(input);
        var lan2 = await bo.Sut.CreateAsync(input);

        Assert.Equal(lan1.Id, lan2.Id);
        Assert.Single(bo.GiaVon.TatCa);
        Assert.Equal(1, bo.GiaVon.SoLanChen);
        Assert.False(lan1.DaDuyet); // tạo ra CHƯA duyệt (giữ 2 bước — Q4 còn Open)
    }

    /// <summary>BR-012 — thiếu khoá idempotency ⇒ NÉM `0006`, **không** ghi dòng nào.</summary>
    [Fact]
    public async Task BR_012_thieu_khoa__nem_0006_khong_ghi()
    {
        var bo = new Bo();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.CreateAsync(Tao(string.Empty)));

        Assert.Equal(PricingErrorCodes.ThieuKhoaIdempotency, ex.Code);
        Assert.Empty(bo.GiaVon.TatCa);
        Assert.Equal(0, bo.GiaVon.SoLanChen);
    }

    /// <summary>BR-012 — khoá được **trim** trước khi so ⇒ `" idem-1 "` trùng `"idem-1"`.</summary>
    [Fact]
    public async Task BR_012_khoa_duoc_trim__cung_dong()
    {
        var bo = new Bo();

        var lan1 = await bo.Sut.CreateAsync(Tao(" idem-1 "));
        var lan2 = await bo.Sut.CreateAsync(Tao("idem-1"));

        Assert.Equal(lan1.Id, lan2.Id);
        Assert.Single(bo.GiaVon.TatCa);
    }
}
