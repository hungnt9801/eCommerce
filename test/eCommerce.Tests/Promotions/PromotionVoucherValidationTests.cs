using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Engine;
using eCommerce.Promotions.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using RepoGia = eCommerce.Tests.Pricing.HaTang.RepoGia<eCommerce.Promotions.Promotion>;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`TC-74`** (`D-291` ② P2 #3/#4) — `ValidateVoucherAsync` chỉ đọc **kết quả của CHÍNH voucher**:
/// mã **không tồn tại** + có auto khớp ⇒ `IsValid = false`, `RejectionReason = NoCandidate` (engine ném `0101`,
/// method **bắt** lại — không ra tới biên validate); voucher được nhận ⇒ `IsValid = true` + `PromotionId`/
/// `DiscountTotal` **của voucher**; voucher hết lượt mức promotion ⇒ `0119` **ném** như `evaluate`.
///
/// **`TC-75`** (`D-291` ③ P3 #6) — `?sort=` **whitelist**: `sort=Code`/`Priority desc` đổi thứ tự + tie-break `Id`;
/// trường lạ/không truyền ⇒ mặc định `CreationTime desc` + tie-break `Id` (**không** ném).
///
/// 📌 Khuôn `D-229` ①(A2): dựng `PromotionAppService` bằng `new` + repo in-memory + engine/registry thật từ
/// `HaTang`; **không** host ABP, **không** PostgreSQL.
/// </summary>
public class PromotionVoucherValidationTests
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
        public DataSourceGia Data { get; } = new();
        public RepoGia Promotions { get; }
        public RepoKhoaChuoiGia<PromotionRuleCost> RuleCosts { get; }

        public Bo()
        {
            Promotions = new RepoGia();
            var conditions = new eCommerce.Tests.Pricing.HaTang.RepoGia<PromotionCondition>();
            RuleCosts = new RepoKhoaChuoiGia<PromotionRuleCost>();
            var redemptions = new eCommerce.Tests.Pricing.HaTang.RepoGia<PromotionRedemption>();
            var guid = new GuidGia();

            Sut = new PromotionAppService(
                Promotions,
                conditions,
                RuleCosts,
                redemptions,
                HaTang.Engine(Data),
                HaTang.Registry(),
                new KhoaGia(),
                guid,
                new DongHoGia(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc)));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGuidGenerator>(guid);
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton<ISettingProvider>(new SettingGia(new Dictionary<string, string?>()));
            Sut.LazyServiceProvider = new AbpLazyServiceProvider(services.BuildServiceProvider());
        }

        public Guid ThemPromotion(string code, int priority = 0, Guid? id = null)
        {
            var p = Promotion.TaoMoi(
                id ?? Guid.NewGuid(), null, code, "KM " + code, PromotionKind.Discount,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, priority, isCumulative: false,
                channelScope: null, PromotionDiscountKind.FixedAmount, discountValue: 0m,
                maxDiscountAmount: null, minOrderAmount: null, currency: "VND",
                maxUsageTotal: null, maxUsagePerCustomer: null);
            Promotions.Them(p);
            return p.Id;
        }
    }

    private static ValidateVoucherInput Vao(string code)
        => new()
        {
            Code = code,
            ChannelCode = "web",
            Currency = "VND",
            Subtotal = 100_000m,
            Lines = new List<PromotionCartLine>()
        };

    // ── TC-74 ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TC74i_ma_khong_ton_tai_va_co_auto_khop__IsValid_false()
    {
        var bo = new Bo();
        bo.Data.Voucher = null; // mã không tồn tại
        // Một khuyến mãi tự động KHỚP — nếu method lấy `Applied` của engine thì sẽ sai thành `IsValid=true`.
        bo.Data.Autos.Add(HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: true, value: 20_000m));

        var kq = await bo.Sut.ValidateVoucherAsync(Vao("KHONG-CO"));

        Assert.False(kq.IsValid);
        Assert.Null(kq.PromotionId);
        Assert.Equal(0m, kq.DiscountTotal);
        Assert.Equal(PromotionRejectionReason.NoCandidate, kq.RejectionReason);
    }

    [Fact]
    public async Task TC74ii_voucher_duoc_nhan__IsValid_true_va_PromotionId_cua_voucher()
    {
        var bo = new Bo();
        var idVoucher = Guid.NewGuid();
        bo.Data.Voucher = HaTang.Candidate(
            idVoucher, HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10);
        // Auto khác cũng áp — `PromotionId`/`DiscountTotal` trả về phải là **của voucher**, không phải auto.
        bo.Data.Autos.Add(HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: true, priority: 1, value: 5_000m));

        var kq = await bo.Sut.ValidateVoucherAsync(Vao("ABC"));

        Assert.True(kq.IsValid);
        Assert.Equal(idVoucher, kq.PromotionId);
        Assert.Equal(10_000m, kq.DiscountTotal);
        Assert.Equal("VND", kq.Currency);
        Assert.Null(kq.RejectionReason);
    }

    [Fact]
    public async Task TC74_voucher_bi_tu_choi_nhung_auto_ap__ly_do_van_hien()
    {
        var bo = new Bo();
        bo.Data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa("CustomerGroup"), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10);
        bo.Data.Autos.Add(HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: true, value: 5_000m));

        var kq = await bo.Sut.ValidateVoucherAsync(Vao("ABC")); // không có CustomerId ⇒ lá khách false

        Assert.False(kq.IsValid);
        Assert.Null(kq.PromotionId);
        Assert.Equal(0m, kq.DiscountTotal);
        Assert.Equal(PromotionRejectionReason.ConditionFalse, kq.RejectionReason);
    }

    [Fact]
    public async Task TC74iii_voucher_het_luot_muc_promotion__nem_0119()
    {
        var bo = new Bo();
        bo.Data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10,
            maxUsageTotal: 1, used: 1);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => bo.Sut.ValidateVoucherAsync(Vao("ABC")));
        Assert.Equal(PromotionErrorCodes.PromotionUsageLimitExceeded, ex.Code);
    }

    // ── TC-75 ────────────────────────────────────────────────────────────────────────────────

    private static async Task<List<string>> MaTheoThuTu(Bo bo, string? sort)
        => (await bo.Sut.GetListAsync(new GetPromotionListInput { Sorting = sort }))
            .Items.Select(x => x.Code).ToList();

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    private static Bo BoSapXep()
    {
        var bo = new Bo();
        // Mã "B" có Priority 5, "A" Priority 1, "C" Priority 3; id tăng dần 1..3 theo B,A,C.
        bo.ThemPromotion("B", priority: 5, id: Id(1));
        bo.ThemPromotion("A", priority: 1, id: Id(2));
        bo.ThemPromotion("C", priority: 3, id: Id(3));
        return bo;
    }

    [Fact]
    public async Task TC75i_sort_Code__doi_thu_tu()
    {
        Assert.Equal(new[] { "A", "B", "C" }, await MaTheoThuTu(BoSapXep(), "Code"));
    }

    [Fact]
    public async Task TC75ii_sort_Priority_desc__doi_thu_tu()
    {
        Assert.Equal(new[] { "B", "C", "A" }, await MaTheoThuTu(BoSapXep(), "Priority desc"));
    }

    [Fact]
    public async Task TC75iii_truong_la__mac_dinh_CreationTime_desc_tie_break_Id()
    {
        // `CreationTime` bằng nhau (repo giả) ⇒ tie-break `Id` tăng dần: B(id1), A(id2), C(id3).
        Assert.Equal(new[] { "B", "A", "C" }, await MaTheoThuTu(BoSapXep(), "Foo desc"));
    }

    [Fact]
    public async Task TC75iv_khong_truyen_sort__mac_dinh_CreationTime_desc_tie_break_Id()
    {
        Assert.Equal(new[] { "B", "A", "C" }, await MaTheoThuTu(BoSapXep(), null));
    }

    [Fact]
    public async Task TC75_tie_break_Id_khi_cung_Priority()
    {
        var bo = new Bo();
        bo.ThemPromotion("X", priority: 7, id: Id(2));
        bo.ThemPromotion("Y", priority: 7, id: Id(1));

        Assert.Equal(new[] { "Y", "X" }, await MaTheoThuTu(bo, "Priority desc"));
    }
}
