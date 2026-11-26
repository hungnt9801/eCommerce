using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Rules;
using eCommerce.Promotions.Validation;
using Volo.Abp;

namespace eCommerce.Promotions.Tests;

public class PromotionDomainTests
{
    private static Promotion TaoPromo(
        PromotionKind kind = PromotionKind.Discount,
        DateTime? starts = null,
        DateTime? ends = null,
        PromotionDiscountKind discountKind = PromotionDiscountKind.FixedAmount,
        decimal value = 10_000m,
        decimal? maxDiscount = null)
        => Promotion.TaoMoi(
            Guid.NewGuid(), Guid.NewGuid(), "KM-1", "Khuyến mãi test", kind,
            starts ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), ends,
            0, true, null, discountKind, value, maxDiscount, null, "VND", null, null);

    // ── Máy trạng thái ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_chi_tu_Draft__sai_nem_0006()
    {
        var p = TaoPromo();
        p.Approve();
        Assert.Equal(PromotionStatus.Approved, p.Status);

        var ex = Assert.Throws<BusinessException>(() => p.Approve());
        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition, ex.Code);
    }

    [Fact]
    public void Kich_hoat_tay_truoc_StartsAtUtc__nem_0006()
    {
        var p = TaoPromo(starts: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        p.Approve();

        var ex = Assert.Throws<BusinessException>(
            () => p.ActivateManual(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(PromotionStatus.Approved, p.Status);
    }

    [Fact]
    public void Job_kich_hoat__chua_toi_gio_thi_giu_nguyen_khong_loi()
    {
        var p = TaoPromo(starts: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        p.Approve();

        var doi = p.ActivateByJob(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(doi);
        Assert.Equal(PromotionStatus.Approved, p.Status);

        Assert.True(p.ActivateByJob(new DateTime(2026, 12, 2, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(PromotionStatus.Active, p.Status);
    }

    [Fact]
    public void Job_het_han__chi_khi_qua_EndsAtUtc()
    {
        var p = TaoPromo(starts: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ends: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        p.Approve();
        p.ActivateManual(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(p.ExpireByJob(new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(p.ExpireByJob(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(PromotionStatus.Expired, p.Status);
    }

    [Fact]
    public void Expired_va_Cancelled_la_hap_thu__moi_chuyen_nem_0006()
    {
        var p = TaoPromo(starts: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ends: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        p.Approve();
        p.ActivateManual(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        p.ExpireByJob(new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition,
            Assert.Throws<BusinessException>(() => p.Cancel()).Code);
        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition,
            Assert.Throws<BusinessException>(() => p.Pause()).Code);

        var c = TaoPromo();
        c.Cancel();
        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition, Assert.Throws<BusinessException>(() => c.Approve()).Code);
    }

    [Fact]
    public void Pause_chi_tu_Active__Resume_chi_tu_Paused()
    {
        var p = TaoPromo();
        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition, Assert.Throws<BusinessException>(() => p.Pause()).Code);

        p.Approve();
        p.ActivateManual(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        p.Pause();
        Assert.Equal(PromotionStatus.Paused, p.Status);
        p.Resume(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(PromotionStatus.Active, p.Status);
    }

    [Fact]
    public void TangTreeVersion_chi_o_Draft__khac_nem_0006()
    {
        var p = TaoPromo();
        p.TangTreeVersionKhiLuuCay();
        Assert.Equal(1, p.TreeVersion);

        p.Approve();
        var ex = Assert.Throws<BusinessException>(() => p.TangTreeVersionKhiLuuCay());
        Assert.Equal(PromotionErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(1, p.TreeVersion);
    }

    [Fact]
    public void Tao_FlashSale_hoac_FreeShipping__bi_tu_choi_validation()
    {
        Assert.ThrowsAny<Exception>(() => Promotion.TaoMoi(
            Guid.NewGuid(), null, "X", "Y", PromotionKind.FlashSale,
            DateTime.UtcNow, null, 0, true, null, PromotionDiscountKind.FixedAmount, 1m, null, null, "VND", null, null));

        Assert.ThrowsAny<Exception>(() => Promotion.TaoMoi(
            Guid.NewGuid(), null, "X", "Y", PromotionKind.Voucher,
            DateTime.UtcNow, null, 0, true, null, PromotionDiscountKind.FreeShipping, 0m, null, null, "VND", null, null));
    }

    // ── Validator ────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyCollection<string> Registry() => HaTang.Registry().SystemNames;

    [Fact]
    public void Validator_vuot_tran_node__0001()
    {
        var root = Guid.NewGuid();
        var nodes = new List<PromotionConditionNode> { new(root, null, true, PromotionConditionGroupType.And, null, 0, 1) };
        for (var i = 0; i < 64; i++)
        {
            nodes.Add(new PromotionConditionNode(Guid.NewGuid(), root, false, null, "MinOrderAmount", i, 2));
        }

        var ex = Assert.Throws<BusinessException>(() => PromotionTreeValidator.Validate(
            nodes, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), new Dictionary<string, int>()));
        Assert.Equal(PromotionErrorCodes.TreeTooLarge, ex.Code);
    }

    [Fact]
    public void Validator_do_sau_9__0001()
    {
        var nodes = new[] { new PromotionConditionNode(Guid.NewGuid(), null, false, null, "MinOrderAmount", 0, 9) };
        var ex = Assert.Throws<BusinessException>(() => PromotionTreeValidator.Validate(
            nodes, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), new Dictionary<string, int>()));
        Assert.Equal(PromotionErrorCodes.TreeTooLarge, ex.Code);
    }

    [Fact]
    public void Validator_vuot_tran_to_hop__0002_khong_phai_0001()
    {
        var root = Guid.NewGuid();
        var nodes = new List<PromotionConditionNode> { new(root, null, true, PromotionConditionGroupType.And, null, 0, 1) };
        for (var i = 0; i < 30; i++)
        {
            nodes.Add(new PromotionConditionNode(Guid.NewGuid(), root, false, null, "MinOrderAmount", i, 2));
        }

        var costs = new Dictionary<string, int>(StringComparer.Ordinal) { ["MinOrderAmount"] = 40 };
        var ex = Assert.Throws<BusinessException>(() => PromotionTreeValidator.Validate(
            nodes, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), costs));
        Assert.Equal(PromotionErrorCodes.CombinationExplosion, ex.Code);
    }

    [Fact]
    public void Validator_hai_goc_hoac_node_lai__0004()
    {
        var twoRoots = new[]
        {
            new PromotionConditionNode(Guid.NewGuid(), null, false, null, "MinOrderAmount", 0, 1),
            new PromotionConditionNode(Guid.NewGuid(), null, false, null, "MinOrderAmount", 1, 1)
        };
        Assert.Equal(PromotionErrorCodes.InvalidConditionNode, Assert.Throws<BusinessException>(() =>
            PromotionTreeValidator.Validate(twoRoots, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), new Dictionary<string, int>())).Code);

        var lai = new[] { new PromotionConditionNode(Guid.NewGuid(), null, true, PromotionConditionGroupType.And, "MinOrderAmount", 0, 1) };
        Assert.Equal(PromotionErrorCodes.InvalidConditionNode, Assert.Throws<BusinessException>(() =>
            PromotionTreeValidator.Validate(lai, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), new Dictionary<string, int>())).Code);
    }

    [Fact]
    public void Validator_rule_chua_tung_dang_ky__0004__da_go__0010()
    {
        var root = Guid.NewGuid();
        var nodes = new[] { new PromotionConditionNode(root, null, false, null, "KhongTonTai", 0, 1) };

        Assert.Equal(PromotionErrorCodes.InvalidConditionNode, Assert.Throws<BusinessException>(() =>
            PromotionTreeValidator.Validate(nodes, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), new Dictionary<string, int>())).Code);

        Assert.Equal(PromotionErrorCodes.RuleNotRegistered, Assert.Throws<BusinessException>(() =>
            PromotionTreeValidator.Validate(nodes, PromotionLimits.MacDinh, Registry(),
                new HashSet<string>(StringComparer.Ordinal) { "KhongTonTai" }, new Dictionary<string, int>())).Code);
    }

    [Fact]
    public void Validator_chu_trinh__0003()
    {
        var root = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var nodes = new[]
        {
            new PromotionConditionNode(root, null, false, null, "MinOrderAmount", 0, 1),
            new PromotionConditionNode(a, b, false, null, "MinOrderAmount", 1, 2),
            new PromotionConditionNode(b, a, false, null, "MinOrderAmount", 2, 2)
        };

        Assert.Equal(PromotionErrorCodes.ConditionCycleDetected, Assert.Throws<BusinessException>(() =>
            PromotionTreeValidator.Validate(nodes, PromotionLimits.MacDinh, Registry(), new HashSet<string>(), new Dictionary<string, int>())).Code);
    }

    // ── Rule lá ──────────────────────────────────────────────────────────────────────────────────

    private static PromotionRuleContext Ctx(Guid? customer = null, Guid? group = null, bool? firstOrder = null,
        decimal subtotal = 100_000m, string channel = "web", string? channelScope = null, decimal? minOrder = null)
        => new(Array.Empty<PromotionCartLine>(), channel, "VND", subtotal, customer, group, firstOrder,
            new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc), minOrder, channelScope);

    [Fact]
    public void Rule_MinOrderAmount()
    {
        var r = new MinOrderAmountRule();
        Assert.True(r.IsSatisfied(Ctx(subtotal: 100_000m, minOrder: 100_000m)));
        Assert.True(r.IsSatisfied(Ctx(subtotal: 100_001m, minOrder: 100_000m)));
        Assert.False(r.IsSatisfied(Ctx(subtotal: 99_999m, minOrder: 100_000m)));
        Assert.True(r.IsSatisfied(Ctx(minOrder: null)));
    }

    [Fact]
    public void Rule_ChannelScope()
    {
        var r = new ChannelScopeRule();
        Assert.True(r.IsSatisfied(Ctx(channel: "web", channelScope: null)));
        Assert.True(r.IsSatisfied(Ctx(channel: "web", channelScope: "web")));
        Assert.False(r.IsSatisfied(Ctx(channel: "shopee", channelScope: "web")));
    }

    [Fact]
    public void Rule_khach__CustomerId_null_thi_false()
    {
        var group = new CustomerGroupRule();
        var first = new FirstOrderOnlyRule();

        Assert.False(group.IsSatisfied(Ctx(customer: null, group: Guid.NewGuid())));
        Assert.False(first.IsSatisfied(Ctx(customer: null, firstOrder: true)));

        Assert.True(group.IsSatisfied(Ctx(customer: Guid.NewGuid(), group: Guid.NewGuid())));
        Assert.False(group.IsSatisfied(Ctx(customer: Guid.NewGuid(), group: null)));
        Assert.True(first.IsSatisfied(Ctx(customer: Guid.NewGuid(), firstOrder: true)));
        Assert.False(first.IsSatisfied(Ctx(customer: Guid.NewGuid(), firstOrder: false)));
    }

    // ── Registry ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Registry_tat_dinh_va_chan_trung()
    {
        var r1 = HaTang.Registry().SystemNames.ToList();
        var r2 = HaTang.Registry().SystemNames.ToList();
        Assert.Equal(r1, r2);
        Assert.Equal(new[] { "ChannelScope", "CustomerGroup", "FirstOrderOnly", "MinOrderAmount" }, r1);

        Assert.ThrowsAny<Exception>(() => new PromotionRuleRegistry(new IPromotionConditionRule[]
        {
            new MinOrderAmountRule(),
            new TrungMinOrderAmountRule()
        }));
    }

    private sealed class TrungMinOrderAmountRule : IPromotionConditionRule
    {
        public string SystemName => "MinOrderAmount";
        public bool IsSatisfied(PromotionRuleContext context) => true;
    }

    // ── Lock key / mã lỗi / ordinal / setting ───────────────────────────────────────────────────

    [Fact]
    public void LockKey_va_Ma_Loi_dung_quy_uoc()
    {
        var t = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var p = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal($"promotion:tree:{t}:{p}", PromotionLockKeys.Tree(t, p));
        Assert.Equal($"promotion:job:expiry:{t}", PromotionLockKeys.Job("expiry", t));
        Assert.Equal("promotion:job:expiry:host", PromotionLockKeys.Job("expiry", null));
        Assert.Equal($"promotion:import:{t}", PromotionLockKeys.Import(t));
        Assert.Equal($"promotion:voucher:{t}:ABC", PromotionLockKeys.VoucherUsage(t, "ABC"));

        Assert.Equal("eCommerce:Promotion:0001", PromotionErrorCodes.TreeTooLarge);
        Assert.Equal("eCommerce:Promotion:0010", PromotionErrorCodes.RuleNotRegistered);
    }

    [Fact]
    public void RejectionReason_ghim_ordinal_0_11()
    {
        Assert.Equal(0, (int)PromotionRejectionReason.NoCandidate);
        Assert.Equal(11, (int)PromotionRejectionReason.CustomerRequired);
        Assert.Equal(12, Enum.GetValues<PromotionRejectionReason>().Length);
        Assert.Equal(8, (int)PromotionRejectionReason.SupersededByVoucher);
        Assert.Equal(9, (int)PromotionRejectionReason.DiscountCapExceeded);
        Assert.Equal(10, (int)PromotionRejectionReason.ExcludedByNonCumulative);
    }

    [Fact]
    public void Settings_mac_dinh_va_kep_MaxDepth()
    {
        var d = PromotionSettings.Doc(null, null, null, null, null, null, null, out var hopLe);
        Assert.True(hopLe);
        Assert.Equal(64, d.MaxNodes);
        Assert.Equal(8, d.MaxDepth);
        Assert.Equal(32, d.MaxLeaves);
        Assert.Equal(1024, d.MaxDeclaredCombinations);
        Assert.Equal(50, d.MaxCandidatesPerCart);
        Assert.Equal(512, d.LeafVisitBudget);
        Assert.Equal(5000, d.ComparisonBudget);

        var cao = PromotionSettings.Doc(null, "9", null, null, null, null, null, out var hopLe9);
        Assert.False(hopLe9);
        Assert.Equal(8, cao.MaxDepth);

        Assert.False(PromotionSettings.DocBat("false"));
        Assert.True(PromotionSettings.DocBat(null));
        Assert.True(PromotionSettings.DocBat("true"));
    }
}
