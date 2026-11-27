using System.Diagnostics.Metrics;
using eCommerce.Kernel.Observability;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Engine;
using Volo.Abp;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`TC-72`** (`D-291` ① P1 #1) — guard trạng thái ở **đường voucher**: promotion không `Active`
/// (`Paused`/`Cancelled`/`Draft`/`Expired`) **hoặc** voucher `Revoked` ⇒ **KHÔNG áp**, 0 đồng,
/// `VoucherOutcome.RejectionReason = NoCandidate`; guard chạy **trước** validate cây (cây có node lạ cũng
/// **không** ném `0004`). Đối xứng lọc `Status == Active` của `ListAutoCandidatesAsync`.
///
/// **`D-292`** — `DanhGiaCandidateAsync` duyệt **cây TRƯỚC**, kiểm hết lượt **sau** (`D-161`/`D-162`):
/// (i) auto + vãng lai + trần khách + cây `false` ⇒ `ConditionFalse`, **không** `CustomerRequired`;
/// (ii) voucher cây `false` + hết lượt ⇒ `ConditionFalse`, **không ném**;
/// (iii) voucher cây `true` + hết lượt ⇒ **NÉM** `0007`/`0119`/`0104`;
/// (v) `promotion_usage_limit_hit_total` **không** tăng cho ca cây `false`.
/// </summary>
public class PromotionVoucherGuardTests
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

    // ── TC-72 — guard trạng thái voucher (fail-closed) ────────────────────────────────────────

    [Theory]
    [InlineData(PromotionStatus.Paused)]
    [InlineData(PromotionStatus.Cancelled)]
    [InlineData(PromotionStatus.Draft)]
    [InlineData(PromotionStatus.Expired)]
    public async Task TC72_voucher_cua_promotion_khong_Active__khong_ap_va_0_dong(PromotionStatus status)
    {
        // Cây `MinOrderAmount` (ngưỡng null ⇒ `true`): nếu guard trạng thái bị gỡ, voucher sẽ áp 10.000.
        var data = new DataSourceGia();
        var voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10, status: status);
        data.Voucher = voucher;

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC"));

        Assert.False(kq.Applied);
        Assert.Equal(0m, kq.DiscountTotal);
        Assert.NotNull(kq.VoucherOutcome);
        Assert.False(kq.VoucherOutcome!.Applied);
        Assert.Equal(0m, kq.VoucherOutcome.DiscountTotal);
        Assert.Equal(PromotionRejectionReason.NoCandidate, kq.VoucherOutcome.RejectionReason);
        Assert.Contains(kq.Breakdown, b =>
            b.PromotionId == voucher.PromotionId
            && !b.Satisfied
            && b.RejectionReason == PromotionRejectionReason.NoCandidate);
    }

    [Fact]
    public async Task TC72_voucher_Revoked_cua_promotion_Active__khong_ap_va_0_dong()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10,
            status: PromotionStatus.Active, voucherStatus: VoucherCodeStatus.Revoked);

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC"));

        Assert.False(kq.Applied);
        Assert.Equal(0m, kq.DiscountTotal);
        Assert.Equal(PromotionRejectionReason.NoCandidate, kq.VoucherOutcome!.RejectionReason);
    }

    [Fact]
    public async Task TC72_guard_trang_thai_chay_TRUOC_validate_cay__cay_hong_cung_khong_nem_0004()
    {
        // `PRO-002` §14 `TC-72`: promotion đã dừng ⇒ KHÔNG chạy guard tĩnh/cây ⇒ node lạ không ném `0004`.
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa("KhongTonTaiTrongRegistry"), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10,
            status: PromotionStatus.Paused);

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC"));

        Assert.False(kq.Applied);
        Assert.Equal(PromotionRejectionReason.NoCandidate, kq.VoucherOutcome!.RejectionReason);
    }

    // ── D-292 (i)+(v) — auto + vãng lai + trần khách + cây `false` ⇒ ConditionFalse ──────────

    [Fact]
    public async Task D292i_tu_dong_cay_FALSE_va_vang_lai_tran_khach__ConditionFalse_khong_CustomerRequired()
    {
        var metrics = new eCommerceMetrics(new Meter("promo-test-d292i-" + Guid.NewGuid()));
        var (tongUsageLimitHit, listener) = TheoDoi(metrics, eCommerceMetricNames.PromotionUsageLimitHitTotal);
        using var _ = listener;

        var data = new DataSourceGia();
        // Lá `CustomerGroup` với `CustomerId = NULL` ⇒ cây `false` (`BR-08`). Trần khách = 1 (vãng lai).
        data.Autos.Add(HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa("CustomerGroup"), value: 10_000m, maxUsagePerCustomer: 1));

        var kq = await HaTang.Engine(data, metrics: metrics).EvaluateAsync(Req(customer: null));

        Assert.False(kq.Applied);
        Assert.Contains(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.ConditionFalse);
        Assert.DoesNotContain(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.CustomerRequired);
        Assert.DoesNotContain(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.UsageLimitHit);
        Assert.Equal(0L, tongUsageLimitHit()); // (v) cây `false` ⇒ không chạm trần nào.
    }

    // ── D-292 (ii) — voucher cây `false` + hết lượt ⇒ ConditionFalse, KHÔNG ném ──────────────

    [Fact]
    public async Task D292ii_voucher_cay_FALSE_cong_het_luot__ConditionFalse_khong_nem()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa("CustomerGroup"), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 1, voucherUsed: 1,
            maxUsageTotal: 1, used: 1);

        var kq = await HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC", customer: null));

        Assert.False(kq.Applied);
        Assert.Equal(PromotionRejectionReason.ConditionFalse, kq.VoucherOutcome!.RejectionReason);
    }

    // ── D-292 (iii) — voucher cây `true` + hết lượt ⇒ vẫn NÉM đúng mã ────────────────────────

    [Fact]
    public async Task D292iii_voucher_cay_TRUE_het_luot_MUC_MA__nem_0007()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 1, voucherUsed: 1);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC")));
        Assert.Equal(PromotionErrorCodes.UsageLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task D292iii_voucher_cay_TRUE_het_luot_MUC_PROMOTION__nem_0119()
    {
        var data = new DataSourceGia();
        data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10,
            maxUsageTotal: 1, used: 1);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC")));
        Assert.Equal(PromotionErrorCodes.PromotionUsageLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task D292iii_voucher_cay_TRUE_het_luot_MUC_KHACH__nem_0104()
    {
        var data = new DataSourceGia();
        data.CustomerUsage = 1;
        data.Voucher = HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), isAutomatic: false,
            value: 10_000m, voucherCodeId: Guid.NewGuid(), voucherMaxUsage: 10,
            maxUsagePerCustomer: 1);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => HaTang.Engine(data).EvaluateAsync(Req(voucher: "ABC", customer: Guid.NewGuid())));
        Assert.Equal(PromotionErrorCodes.CustomerUsageLimitExceeded, ex.Code);
    }

    // ── D-292 (v) — counter `promotion_usage_limit_hit_total` chỉ tăng khi cây `true` ────────

    [Fact]
    public async Task D292v_auto_cay_TRUE_het_luot__counter_tang()
    {
        var metrics = new eCommerceMetrics(new Meter("promo-test-d292v-" + Guid.NewGuid()));
        var (tongUsageLimitHit, listener) = TheoDoi(metrics, eCommerceMetricNames.PromotionUsageLimitHitTotal);
        using var _ = listener;

        var data = new DataSourceGia();
        data.Autos.Add(HaTang.Candidate(
            Guid.NewGuid(), HaTang.CayMotLa(), value: 10_000m, maxUsageTotal: 1, used: 1));

        var kq = await HaTang.Engine(data, metrics: metrics).EvaluateAsync(Req());

        Assert.False(kq.Applied);
        Assert.Contains(kq.Breakdown, b => b.RejectionReason == PromotionRejectionReason.UsageLimitHit);
        Assert.True(tongUsageLimitHit() >= 1L, "counter `promotion_usage_limit_hit_total` phải tăng khi cây `true` + hết lượt.");
    }

    // ── Helper: đếm counter theo tên instrument (mọi meter) ─────────────────────────────────

    private static (Func<long> Tong, MeterListener Listener) TheoDoi(
        eCommerceMetrics metrics, string tenInstrument)
    {
        _ = metrics; // giữ tham chiếu để meter sống trong suốt ca test
        long tong = 0;
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == tenInstrument)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref tong, value));
        listener.Start();
        return (() => Interlocked.Read(ref tong), listener);
    }
}
