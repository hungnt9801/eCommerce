using System.Diagnostics;
using eCommerce.Kernel.Money;
using eCommerce.Kernel.Observability;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Rules;
using eCommerce.Promotions.Validation;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;
using Volo.Abp.Timing;

namespace eCommerce.Promotions.Engine;

/// <summary>
/// Context engine dựng **MỘT LẦN** khi vào đánh giá (`PRO-002` §3.4 / `BR-09`): dữ liệu giỏ + `IClock.Now`
/// **một lần** + tiền tệ. Rule lá chỉ nhận **hình chiếu** `PromotionRuleContext` (không bộ đếm ngân sách).
/// </summary>
public sealed record PromotionEvaluationContext(
    IReadOnlyList<PromotionCartLine> Lines,
    string ChannelCode,
    Currency MaTienTe,
    Money Subtotal,
    Guid? CustomerId,
    Guid? CustomerGroupId,
    bool? IsFirstOrder,
    DateTime NowUtc);

/// <summary>
/// **ENGINE KHUYẾN MÃI — CHỈ ĐỌC** (`PRO-002` §6.1, `CR-7`/`D-151`).
///
/// 🔒 7 bước: dựng context 1 lần → nạp candidate (tự động + mã) → khử trùng + cắt `MaxCandidatesPerCart`
/// → mỗi candidate: guard tĩnh + duyệt ngắn mạch + đếm ngân sách → kiểm lượt **chỉ đọc** → ghép **một luật**
/// → trả `DiscountTotal ≤ Subtotal` **không kẹp**.
///
/// 🚨 **0 ghi DB · 0 idempotency guard · 0 truy vấn trong vòng lá**. **CẤM** `Task.WhenAll`/`Parallel.`/
/// `DateTime.Now`/`Guid.NewGuid` trong đường quyết định.
/// </summary>
public class PromotionEngine : IPromotionEngine, ITransientDependency
{
    private const string SourceNoiBo = "Internal";

    private readonly IPromotionDataSource _data;
    private readonly PromotionRuleRegistry _registry;
    private readonly ISettingProvider _settings;
    private readonly IClock _clock;
    private readonly ILogger<PromotionEngine> _logger;
    private readonly eCommerceMetrics _metrics;

    public PromotionEngine(
        IPromotionDataSource data,
        PromotionRuleRegistry registry,
        ISettingProvider settings,
        IClock clock,
        ILogger<PromotionEngine> logger,
        eCommerceMetrics metrics)
    {
        _data = data;
        _registry = registry;
        _settings = settings;
        _clock = clock;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── Quy tắc 4: `VoucherCode ≠ null ⇔ Mode = CustomerRequested` (HAI CHIỀU) ─────────────────
        var coMa = !string.IsNullOrWhiteSpace(request.VoucherCode);
        var customerRequested = request.Mode == PromotionApplyMode.CustomerRequested;
        if (coMa != customerRequested)
        {
            throw new ArgumentException(
                "VoucherCode và Mode lệch nhau: có mã ⇔ CustomerRequested (PRO-002 §6.1 quy tắc 4).",
                nameof(request));
        }

        var dongHo = Stopwatch.StartNew();
        var maTienTe = Currency.FromCode(request.Currency);
        var now = _clock.Now.ToUniversalTime();
        var limits = await DocLimitsAsync();

        var context = new PromotionEvaluationContext(
            request.Lines ?? [],
            request.ChannelCode,
            maTienTe,
            Money.Cua(request.Subtotal, maTienTe),
            request.CustomerId,
            request.CustomerGroupId,
            request.IsFirstOrder,
            now);

        var breakdown = new List<PromotionBreakdownItem>();
        var outcomes = new List<Outcome>();
        var dem = new Dem();

        // ── Bước 2: nạp candidate (a) tự động + (b) theo mã ──────────────────────────────────────
        PromotionCandidateData? voucher = null;
        if (coMa)
        {
            voucher = await _data.FindVoucherCandidateAsync(request.VoucherCode!.Trim(), cancellationToken);

            // `D-291` ② P2 #4 — mã **không tồn tại** (không có dòng `ProVoucherCodes`/promotion) ⇒ **NÉM**
            // đúng mã đã cấp `0101` (`D-144`), KHÔNG trả `NoCandidate` im lặng. (Voucher tồn tại nhưng
            // promotion không `Active`/voucher `Revoked` thì vẫn tạo candidate rồi bị guard trạng thái từ chối
            // mềm ở `DanhGiaCandidateAsync` ⇒ `Applied=false` theo `D-291` ②.)
            if (voucher is null)
            {
                throw new BusinessException(PromotionErrorCodes.VoucherNotFound)
                    .WithData("code", request.VoucherCode!.Trim());
            }
        }

        var autos = await _data.ListAutoCandidatesAsync(now, request.ChannelCode, cancellationToken);

        // Khử trùng theo PromotionId — mã khách nhập THẮNG (TC-67 (ii)).
        var voucherPromotionId = voucher?.PromotionId;
        var autoDuyet = autos
            .Where(x => x.PromotionId != voucherPromotionId)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.PromotionId)
            .ToList();

        // Cắt candidate theo Priority DESC, Id ASC (ADR-15).
        var gioiHan = limits.MaxCandidatesPerCart;
        var giuChoMa = voucher is null ? 0 : 1;
        var soTuDongToiDa = Math.Max(0, gioiHan - giuChoMa);
        if (autoDuyet.Count > soTuDongToiDa)
        {
            var biCat = autoDuyet.Skip(soTuDongToiDa).ToList();
            autoDuyet = autoDuyet.Take(soTuDongToiDa).ToList();

            foreach (var cat in biCat)
            {
                breakdown.Add(new PromotionBreakdownItem(
                    cat.PromotionId, null, false, PromotionRejectionReason.CandidatesTruncated, SourceNoiBo));
            }

            _metrics.PromotionCandidatesTruncated.Add(
                biCat.Count,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, "Discount"));

            _logger.LogWarning(
                "PROMOTION: cắt {SoCat} candidate vượt MaxCandidatesPerCart={Max} (tất định theo Priority, Id).",
                biCat.Count, gioiHan);
        }

        // Nạp phân loại + chi phí rule MỘT LẦN cho mọi candidate (ngoài vòng lá).
        var tapCandidate = new List<PromotionCandidateData>();
        if (voucher is not null)
        {
            tapCandidate.Add(voucher);
        }

        tapCandidate.AddRange(autoDuyet);

        var tenRule = tapCandidate
            .SelectMany(c => c.Conditions)
            .Where(c => !c.IsGroup && !string.IsNullOrEmpty(c.RuleSystemName))
            .Select(c => c.RuleSystemName!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var knownRows = await _data.LoadKnownRuleRowsAsync(tenRule, cancellationToken);
        var costs = await _data.LoadDeclaredCostsAsync(tenRule, cancellationToken);

        // ── Bước 4: đánh giá từng candidate ──────────────────────────────────────────────────────
        if (voucher is not null)
        {
            outcomes.Add(await DanhGiaCandidateAsync(voucher, isVoucherSource: true, context, limits, knownRows, costs, dem, cancellationToken));
        }

        foreach (var c in autoDuyet)
        {
            outcomes.Add(await DanhGiaCandidateAsync(c, isVoucherSource: false, context, limits, knownRows, costs, dem, cancellationToken));
        }

        // ── Bước 6: ghép theo MỘT luật (cây → khách → ghép → tiền → trần) ───────────────────────
        var voucherOutcome = outcomes.FirstOrDefault(x => x.IsVoucherSource);
        var autoOutcomes = outcomes.Where(x => !x.IsVoucherSource).ToList();

        var tong = Money.Zero(maTienTe);
        var daAp = false;
        var nhomDaNhan = new List<Outcome>();

        if (voucherOutcome is { Satisfied: true })
        {
            tong = voucherOutcome.Discount!;
            nhomDaNhan.Add(voucherOutcome);
            daAp = true;

            if (tong.CompareTo(context.Subtotal) > 0)
            {
                // Mã khách nhập vượt `Subtotal` ⇒ NÉM `0009` (không kẹp — D-158).
                throw new BusinessException(PromotionErrorCodes.DiscountExceedsOrder)
                    .WithData("DiscountTotal", tong.Amount)
                    .WithData("Subtotal", context.Subtotal.Amount);
            }
        }

        foreach (var o in autoOutcomes)
        {
            if (!o.Satisfied)
            {
                continue;
            }

            var ghepDuoc = CoTheGhep(nhomDaNhan, o, voucherOutcome is { Satisfied: true }, out var lyDoKhongGhep);
            if (!ghepDuoc)
            {
                o.Reason = lyDoKhongGhep;
                continue;
            }

            var tongMoi = tong + o.Discount!;
            if (tongMoi.CompareTo(context.Subtotal) > 0)
            {
                o.Reason = PromotionRejectionReason.DiscountCapExceeded;
                _logger.LogWarning(
                    "PROMOTION: candidate {PromotionId} bị bỏ — luỹ kế vượt Subtotal (DiscountCapExceeded).", o.Data.PromotionId);
                continue;
            }

            tong = tongMoi;
            nhomDaNhan.Add(o);
            daAp = true;
        }

        // ── Dựng breakdown + metric lý do ───────────────────────────────────────────────────────
        foreach (var o in outcomes)
        {
            var dat = o.Satisfied && o.Reason is null;
            breakdown.Add(new PromotionBreakdownItem(o.Data.PromotionId, null, dat, dat ? null : o.Reason, SourceNoiBo));
            GhiMetric(o.Data, o.Reason);
        }

        dongHo.Stop();
        _metrics.PromotionEvalDurationSeconds.Record(
            dongHo.Elapsed.TotalSeconds,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, "eval"));

        PromotionRejectionReason? lyDoTong = null;
        if (outcomes.Count == 0)
        {
            lyDoTong = PromotionRejectionReason.NoCandidate;
        }
        else if (!daAp)
        {
            lyDoTong = outcomes.Select(x => x.Reason).FirstOrDefault(r => r is not null);
        }

        // `D-291` ② P2 #3/#4 — kết quả RIÊNG của voucher, additive; `Applied` chỉ true khi chính voucher được
        // nhận (không phụ thuộc candidate tự động). Lý do của kênh voucher vẫn hiện kể cả khi auto áp.
        PromotionVoucherOutcome? ketQuaVoucher = null;
        if (voucherOutcome is not null)
        {
            var voucherDuocNhan = voucherOutcome.Satisfied;
            ketQuaVoucher = new PromotionVoucherOutcome(
                voucherOutcome.Data.PromotionId,
                voucherDuocNhan,
                voucherDuocNhan ? (voucherOutcome.Discount?.Amount ?? 0m) : 0m,
                voucherDuocNhan ? null : voucherOutcome.Reason);
        }

        return new PromotionEvaluationResult(
            daAp,
            tong.Amount,
            request.Currency,
            breakdown,
            lyDoTong,
            dem.LeafVisits,
            dem.Comparisons,
            ketQuaVoucher);
    }

    private async Task<Outcome> DanhGiaCandidateAsync(
        PromotionCandidateData candidate,
        bool isVoucherSource,
        PromotionEvaluationContext context,
        PromotionLimits limits,
        IReadOnlySet<string> knownRows,
        IReadOnlyDictionary<string, int> costs,
        Dem dem,
        CancellationToken ct)
    {
        var outcome = new Outcome(candidate, isVoucherSource);

        // `D-291` ① P1 #1 — GUARD TRẠNG THÁI **fail-closed** ở đường voucher: chỉ áp khi promotion
        // `Status == Active` **và** voucher **không** `Revoked` (đối xứng `ListAutoCandidatesAsync` đã lọc
        // `Status == Active`). Trạng thái lạ/không đạt ⇒ coi như **không có candidate**: `Applied=false`,
        // 0 đồng, lý do hiện trong kết quả — **không** chạy guard tĩnh/validate cây của promotion đã tạm dừng.
        if (isVoucherSource
            && (candidate.Status != PromotionStatus.Active || candidate.VoucherStatus == VoucherCodeStatus.Revoked))
        {
            outcome.Reason = PromotionRejectionReason.NoCandidate;
            return outcome;
        }

        var nodes = candidate.Conditions
            .Select(x => new PromotionConditionNode(x.Id, x.ParentId, x.IsGroup, x.InteractionType, x.RuleSystemName, x.SortOrder, x.Depth))
            .ToList();

        // Guard TĨNH — NÉM ở MỌI nguồn (0001/0002/0004/0010).
        PromotionTreeValidator.Validate(nodes, limits, _registry.SystemNames, knownRows, costs);

        // Khung hiệu lực + kênh (candidate tự động đã lọc; voucher thì kiểm ở đây).
        var now = context.NowUtc;
        if (candidate.StartsAtUtc.ToUniversalTime() > now
            || (candidate.EndsAtUtc is not null && now >= candidate.EndsAtUtc.Value.ToUniversalTime()))
        {
            outcome.Reason = PromotionRejectionReason.OutOfWindow;
            return outcome;
        }

        if (candidate.ChannelScope is not null
            && !string.Equals(candidate.ChannelScope, context.ChannelCode, StringComparison.Ordinal))
        {
            outcome.Reason = PromotionRejectionReason.ChannelMismatch;
            return outcome;
        }

        // Ngưỡng tiền hàng tối thiểu (cột promotion) — áp cả khi không có lá MinOrderAmount.
        if (candidate.MinOrderAmount is not null && context.Subtotal.Amount < candidate.MinOrderAmount.Value)
        {
            outcome.Reason = PromotionRejectionReason.ConditionFalse;
            return outcome;
        }

        // ── `D-292` — DUYỆT CÂY **TRƯỚC** KHI KIỂM HẾT LƯỢT (đúng `D-161`(1)/`D-162`(3): cây → khách → ghép →
        //    tiền → trần). Cây `false` ⇒ `ConditionFalse` — KHÔNG bao giờ mang `CustomerRequired`/`UsageLimitHit`;
        //    voucher cây `false` + hết lượt ⇒ `ConditionFalse` **không ném** (chỉ ném khi cây `true`).
        // Duyệt cây — ngắn mạch, đếm ngân sách, KHÔNG I/O.
        var ruleContext = new PromotionRuleContext(
            context.Lines, context.ChannelCode, context.MaTienTe.Code, context.Subtotal.Amount,
            context.CustomerId, context.CustomerGroupId, context.IsFirstOrder, context.NowUtc,
            candidate.MinOrderAmount, candidate.ChannelScope);

        var kq = PromotionTreeEvaluator.DanhGia(nodes, _registry, knownRows, ruleContext, limits);
        dem.LeafVisits += kq.LeafVisits;
        dem.Comparisons += kq.Comparisons;

        if (kq.BudgetExceeded)
        {
            _metrics.PromotionEvalBudgetExceeded.Add(1, new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, candidate.Kind.ToString()));

            if (isVoucherSource)
            {
                throw new BusinessException(PromotionErrorCodes.EvaluationBudgetExceeded);
            }

            _logger.LogWarning(
                "PROMOTION: candidate {PromotionId} vượt ngân sách chạy — bỏ (nguồn tự động, BudgetExceeded).",
                candidate.PromotionId);
            outcome.Reason = PromotionRejectionReason.BudgetExceeded;
            return outcome;
        }

        if (!kq.Satisfied)
        {
            outcome.Reason = PromotionRejectionReason.ConditionFalse;
            return outcome;
        }

        // Kiểm lượt — CHỈ ĐỌC (CR-7) — nay chỉ chạy **sau** khi cây `true` (`D-292`).
        if (isVoucherSource && candidate.VoucherMaxUsage is not null
            && candidate.VoucherUsedCount + candidate.VoucherReservedCount >= candidate.VoucherMaxUsage.Value)
        {
            throw new BusinessException(PromotionErrorCodes.UsageLimitExceeded)
                .WithData("voucherCodeId", candidate.VoucherCodeId ?? Guid.Empty);
        }

        if (candidate.MaxUsageTotal is not null
            && candidate.UsedCount + candidate.ReservedCount >= candidate.MaxUsageTotal.Value)
        {
            // `D-291` ② P2 #4 — hết lượt **mức promotion** ở nguồn voucher ⇒ **NÉM** `0119` (`D-147`);
            // nguồn tự động ⇒ bỏ candidate + `UsageLimitHit` (không ném — BR-07/A4).
            if (isVoucherSource)
            {
                throw new BusinessException(PromotionErrorCodes.PromotionUsageLimitExceeded)
                    .WithData("promotionId", candidate.PromotionId);
            }

            outcome.Reason = PromotionRejectionReason.UsageLimitHit;
            _metrics.PromotionUsageLimitHit.Add(1, new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, candidate.Kind.ToString()));
            return outcome;
        }

        if (candidate.MaxUsagePerCustomer is not null)
        {
            if (context.CustomerId is null)
            {
                if (!isVoucherSource)
                {
                    outcome.Reason = PromotionRejectionReason.CustomerRequired;
                    return outcome;
                }
            }
            else
            {
                var daDung = await _data.CountCustomerUsageAsync(candidate.PromotionId, context.CustomerId.Value, ct);
                if (daDung >= candidate.MaxUsagePerCustomer.Value)
                {
                    // `D-291` ② P2 #4 — vượt trần **mức khách** ở nguồn voucher ⇒ **NÉM** `0104` (`D-144`);
                    // nguồn tự động ⇒ bỏ candidate + `UsageLimitHit`.
                    if (isVoucherSource)
                    {
                        throw new BusinessException(PromotionErrorCodes.CustomerUsageLimitExceeded)
                            .WithData("promotionId", candidate.PromotionId)
                            .WithData("customerId", context.CustomerId!.Value);
                    }

                    outcome.Reason = PromotionRejectionReason.UsageLimitHit;
                    _metrics.PromotionUsageLimitHit.Add(1, new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, candidate.Kind.ToString()));
                    return outcome;
                }
            }
        }

        // Duyệt cây đã chạy **trước** khối kiểm lượt (`D-292`); tới đây cây chắc chắn `true`.
        outcome.Satisfied = true;
        outcome.Discount = PromotionDiscountCalculator.Tinh(
            candidate.DiscountKind, candidate.DiscountValue, candidate.MaxDiscountAmount,
            context.Subtotal, new RoundHalfAwayFromZeroPolicy());
        return outcome;
    }

    /// <summary>
    /// **MỘT luật ghép** (`D-160`): giữ candidate đứng trước; hai candidate **không cộng dồn** không ghép được;
    /// mã khách nhập thay candidate tự động không ghép được ⇒ `SupersededByVoucher`.
    /// </summary>
    private static bool CoTheGhep(List<Outcome> daNhan, Outcome ungVien, bool coMaKhongGhep, out PromotionRejectionReason? lyDo)
    {
        lyDo = null;

        if (daNhan.Count == 0)
        {
            return true;
        }

        if (coMaKhongGhep)
        {
            // Đã nhận voucher; ungVien chỉ ghép được nếu CẢ HAI cộng dồn.
            var voucher = daNhan[0];
            if (!voucher.Data.IsCumulative || !ungVien.Data.IsCumulative)
            {
                lyDo = PromotionRejectionReason.SupersededByVoucher;
                return false;
            }

            return true;
        }

        // Không có mã: giữ candidate đứng trước; nếu bất kỳ cái nào không cộng dồn ⇒ không ghép.
        if (!ungVien.Data.IsCumulative || daNhan.Any(x => !x.Data.IsCumulative))
        {
            lyDo = PromotionRejectionReason.ExcludedByNonCumulative;
            return false;
        }

        return true;
    }

    private void GhiMetric(PromotionCandidateData candidate, PromotionRejectionReason? reason)
    {
        if (reason is null)
        {
            return;
        }

        _metrics.PromotionConditionRejected.Add(
            1,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, candidate.Kind.ToString()),
            new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, reason.Value.ToString()));

        if (reason == PromotionRejectionReason.SupersededByVoucher)
        {
            _logger.LogInformation(
                "PROMOTION: candidate tự động {PromotionId} bị mã thay (SupersededByVoucher).", candidate.PromotionId);
        }
        else if (reason == PromotionRejectionReason.ExcludedByNonCumulative)
        {
            _logger.LogInformation(
                "PROMOTION: candidate {PromotionId} bị loại vì không cộng dồn (ExcludedByNonCumulative).", candidate.PromotionId);
        }
    }

    private async Task<PromotionLimits> DocLimitsAsync()
    {
        var n = await _settings.GetOrNullAsync(PromotionSettingNames.MaxNodes);
        var d = await _settings.GetOrNullAsync(PromotionSettingNames.MaxDepth);
        var l = await _settings.GetOrNullAsync(PromotionSettingNames.MaxLeaves);
        var c = await _settings.GetOrNullAsync(PromotionSettingNames.MaxDeclaredCombinations);
        var cand = await _settings.GetOrNullAsync(PromotionSettingNames.MaxCandidatesPerCart);
        var lb = await _settings.GetOrNullAsync(PromotionSettingNames.LeafVisitBudget);
        var cb = await _settings.GetOrNullAsync(PromotionSettingNames.ComparisonBudget);

        var limits = PromotionSettings.Doc(n, d, l, c, cand, lb, cb, out var hopLe);

        if (!hopLe)
        {
            _logger.LogWarning("PROMOTION: có setting cây/ngân sách không hợp lệ — dùng mặc định an toàn.");
        }

        return limits;
    }

    private sealed class Outcome
    {
        public Outcome(PromotionCandidateData data, bool isVoucherSource)
        {
            Data = data;
            IsVoucherSource = isVoucherSource;
        }

        public PromotionCandidateData Data { get; }
        public bool IsVoucherSource { get; }
        public bool Satisfied { get; set; }
        public PromotionRejectionReason? Reason { get; set; }
        public Money? Discount { get; set; }
    }

    private sealed class Dem
    {
        public int LeafVisits;
        public int Comparisons;
    }
}
