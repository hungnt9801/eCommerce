using System.Diagnostics;
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

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  `D-309` (P2 #1, `TC-02`/A5/NFR-13) — PRE-CHECK dung lượng mã `36^(Length−len(Prefix))`
    //  Input HỢP LỆ theo guard độ dài vẫn có thể bất khả thi ⇒ phải NÉM `0107`, KHÔNG treo vòng sinh.
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    private static GenerateVouchersDto VaoMa(Bo bo, int length, string prefix, int quantity)
        => new()
        {
            PromotionId = bo.PromotionId,
            Quantity = quantity,
            MaxUsage = 1,
            Length = length,
            Prefix = prefix,
        };

    [Fact]
    public async Task D309_codespace_thieu__nem_0107_khong_treo()
    {
        // `{Length:3, Prefix:"AB", Quantity:100}`: guard độ dài qua (`3 ≥ 3`, `len(AB)=2 < 3`) nhưng chỉ có
        // `36^(3−2) = 36` mã phân biệt < 100 ⇒ TRƯỚC `D-309` vòng `while` treo VÔ HẠN. Nay phải NÉM `0107` < 1 s.
        var bo = new Bo(maxBatchSetting: null); // mặc định 10000 ⇒ 0108 KHÔNG chen ngang
        var dongHo = Stopwatch.StartNew();

        // ⏱️ `WaitAsync` biến "treo" thành ĐỎ sau 5 s — không để bộ test treo vô hạn khi guard bị gỡ.
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.GenerateAsync(VaoMa(bo, length: 3, prefix: "AB", quantity: 100))
                .WaitAsync(TimeSpan.FromSeconds(5)));

        dongHo.Stop();

        Assert.Equal(PromotionErrorCodes.VoucherCodeInvalid, ex.Code);
        Assert.Equal("eCommerce:Promotion:0107", ex.Code); // D-309 ①(2): ĐÚNG số 0107 (không 0108)
        Assert.Equal(36L, Convert.ToInt64(ex.Data["codespace"]));
        Assert.Equal(100, Convert.ToInt32(ex.Data["quantity"]));
        Assert.Equal(0, bo.Vouchers.SoLanChen);            // không thêm mã nào
        Assert.True(dongHo.Elapsed < TimeSpan.FromSeconds(1),
            $"Phải NÉM trong < 1 s, đo được {dongHo.Elapsed.TotalMilliseconds:F0} ms.");
    }

    [Fact]
    public async Task D309_codespace_du__sinh_du_so_dong()
    {
        // `{Length:12, Quantity:10000}`: `36^12 ≫ 10000` ⇒ pre-check KHÔNG được chặn oan.
        var bo = new Bo(maxBatchSetting: null);

        var kq = await bo.Sut.GenerateAsync(VaoMa(bo, length: 12, prefix: string.Empty, quantity: 10_000))
            .WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(10_000, kq.Count);
        Assert.Equal(10_000, bo.Vouchers.SoLanChen);
    }

    [Fact]
    public async Task D309_codespace_bang_dung_Quantity__KHONG_nem()
    {
        // Biên `space == Quantity`: `{Length:4, Prefix:"AB", Quantity:1296}` = `36^2` ⇒ điều kiện là `space < Quantity`.
        var bo = new Bo(maxBatchSetting: null);

        var kq = await bo.Sut.GenerateAsync(VaoMa(bo, length: 4, prefix: "AB", quantity: 1296))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1296, kq.Count);
    }

    [Fact]
    public async Task D309_thu_tu__quantity_10001_van_0108_truoc_0107()
    {
        // Thứ tự cố định (`D-309` ①(4)): vượt `MaxBatchSize` ⇒ `0108` (lỗi trần lô) TRƯỚC pre-check dung lượng
        // ⇒ KHÔNG được báo `0107` (nói sai nguyên nhân) dù `{Length:3, Prefix:"AB"}` cũng thiếu chỗ.
        var bo = new Bo(maxBatchSetting: null); // mặc định 10000

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.GenerateAsync(VaoMa(bo, length: 3, prefix: "AB", quantity: 10_001))
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(PromotionErrorCodes.BatchSizeExceeded, ex.Code); // 0108
        Assert.Equal(0, bo.Vouchers.SoLanChen);
    }
}
