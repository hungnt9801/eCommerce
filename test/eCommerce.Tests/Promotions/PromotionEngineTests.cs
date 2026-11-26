using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Engine;
using Volo.Abp;

namespace eCommerce.Promotions.Tests;

public class PromotionEngineTests
{
    private static PromotionEvaluationRequest Req(
        string? voucher = null, decimal subtotal = 100_000m, Guid? customer = null, string channel = "web")
        => new(
            Guid.NewGuid(),
            Array.Empty<PromotionCartLine>(),
            channel,
            "VND",
            subtotal,
            customer,
            CustomerGroupId: null,
            IsFirstOrder: null,
            voucher,
            string.IsNullOrWhiteSpace(voucher) ? PromotionApplyMode.AutoApply : PromotionApplyMode.CustomerRequested);

    [Fact]
    public async Task Tat_dinh__hai_lan_cung_ket_qua()
    {
        var data = new DataSourceGia();
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), priority: 1,
            kind: PromotionDiscountKind.FixedAmount, value: 10_000m));

        var engine = HaTang.Engine(data);
        var kq1 = await engine.EvaluateAsync(Req());
        var kq2 = await engine.EvaluateAsync(Req());

        Assert.True(kq1.Applied);
        Assert.Equal(10_000m, kq1.DiscountTotal);
        Assert.Equal(kq1.Applied, kq2.Applied);
        Assert.Equal(kq1.DiscountTotal, kq2.DiscountTotal);
        Assert.Equal(kq1.Breakdown.Count, kq2.Breakdown.Count);
    }

    [Fact]
    public async Task Short_circuit_AND__chi_tham_1_la()
    {
        var data = new DataSourceGia();
        // AND(CustomerGroup=false, ChannelScope x4). CustomerId null ⇒ lá đầu false ⇒ dừng.
        var root = Guid.NewGuid();
        var conditions = new List<PromotionConditionData>
        {
            new(root, null, true, PromotionConditionGroupType.And, null, 0, 1),
            new(Guid.NewGuid(), root, false, null, "CustomerGroup", 0, 2),
            new(Guid.NewGuid(), root, false, null, "ChannelScope", 1, 2),
            new(Guid.NewGuid(), root, false, null, "ChannelScope", 2, 2),
            new(Guid.NewGuid(), root, false, null, "ChannelScope", 3, 2),
            new(Guid.NewGuid(), root, false, null, "ChannelScope", 4, 2)
        };
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), conditions, value: 1m));

        var kq = await HaTang.Engine(data).EvaluateAsync(Req());

        Assert.False(kq.Applied);
        Assert.Equal(1, kq.LeafVisits);
    }

    [Fact]
    public async Task Short_circuit_OR__chi_tham_1_la()
    {
        var data = new DataSourceGia();
        var root = Guid.NewGuid();
        var conditions = new List<PromotionConditionData>
        {
            new(root, null, true, PromotionConditionGroupType.Or, null, 0, 1),
            new(Guid.NewGuid(), root, false, null, "MinOrderAmount", 0, 2),
            new(Guid.NewGuid(), root, false, null, "CustomerGroup", 1, 2),
            new(Guid.NewGuid(), root, false, null, "CustomerGroup", 2, 2)
        };
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), conditions, value: 1m));

        var kq = await HaTang.Engine(data).EvaluateAsync(Req());

        Assert.True(kq.Applied);
        Assert.Equal(1, kq.LeafVisits);
    }

    [Fact]
    public async Task Vuot_ngan_sach__nguon_MA__nem_0005()
    {
        var data = new DataSourceGia();
        var voucher = HaTang.Candidate(Guid.NewGuid(), HaTang.CayNhom(PromotionConditionGroupType.And, "ChannelScope", 20),
            isAutomatic: false, value: 1m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 100);
        data.Voucher = voucher;

        var settings = new Dictionary<string, string?> { [PromotionSettingNames.LeafVisitBudget] = "10" };

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => HaTang.Engine(data, settings: settings).EvaluateAsync(Req(voucher: "ABC")));
        Assert.Equal(PromotionErrorCodes.EvaluationBudgetExceeded, ex.Code);
    }

    [Fact]
    public async Task Vuot_ngan_sach__nguon_TU_DONG__bo_candidate_khong_nem()
    {
        var data = new DataSourceGia();
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), HaTang.CayNhom(PromotionConditionGroupType.And, "ChannelScope", 20),
            value: 1m));

        var settings = new Dictionary<string, string?> { [PromotionSettingNames.LeafVisitBudget] = "10" };

        var kq = await HaTang.Engine(data, settings: settings).EvaluateAsync(Req());

        Assert.False(kq.Applied);
        Assert.Contains(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.BudgetExceeded);
    }

    [Fact]
    public async Task Tran_khong_kep__ma_vuot_Subtotal__nem_0009()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 50_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10);

        // subtotal 40.000 < FixedAmount 50.000 ⇒ NÉM, KHÔNG kẹp.
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC", subtotal: 40_000m)));
        Assert.Equal(PromotionErrorCodes.DiscountExceedsOrder, ex.Code);
    }

    [Fact]
    public async Task Tu_dong_vuot_tran__bo_candidate_chu_khong_nem()
    {
        var data = new DataSourceGia();
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), priority: 10,
            isCumulative: true, value: 30_000m));
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), priority: 5,
            isCumulative: true, value: 20_000m));

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(subtotal: 40_000m));

        Assert.True(kq.Applied);
        Assert.Equal(30_000m, kq.DiscountTotal);
        Assert.Contains(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.DiscountCapExceeded);
    }

    [Fact]
    public async Task Ma_thay_tu_dong_khong_ghep_duoc__SupersededByVoucher()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            isCumulative: false, value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10);
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), priority: 1,
            isCumulative: true, value: 5_000m));

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC"));

        Assert.True(kq.Applied);
        Assert.Equal(10_000m, kq.DiscountTotal);
        Assert.Contains(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.SupersededByVoucher);
    }

    [Fact]
    public async Task Tu_dong_tran_theo_khach_ma_vang_lai__CustomerRequired()
    {
        var data = new DataSourceGia();
        data.Autos.Add(HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), value: 10_000m, maxUsagePerCustomer: 1));

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(customer: null));

        Assert.False(kq.Applied);
        Assert.Contains(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.CustomerRequired);
        Assert.DoesNotContain(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.UsageLimitHit);
    }

    [Fact]
    public async Task Nguon_ma_het_luot_muc_ma__nem_0007()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 1m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 1, voucherUsed: 1);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC")));
        Assert.Equal(PromotionErrorCodes.UsageLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task Khong_co_candidate__NoCandidate()
    {
        var kq = await HaTang.Engine(new DataSourceGia()).EvaluateAsync(Req());
        Assert.False(kq.Applied);
        Assert.Equal(PromotionRejectionReason.NoCandidate, kq.RejectionReason);
        Assert.Equal(0m, kq.DiscountTotal);
    }

    [Fact]
    public async Task Mode_va_VoucherCode_lech__nem_ArgumentException()
    {
        var bad = new PromotionEvaluationRequest(
            Guid.NewGuid(), Array.Empty<PromotionCartLine>(), "web", "VND", 1000m, null, null, null,
            VoucherCode: null, Mode: PromotionApplyMode.CustomerRequested);

        await Assert.ThrowsAsync<ArgumentException>(() => HaTang.Engine(new DataSourceGia()).EvaluateAsync(bad));
    }
}
