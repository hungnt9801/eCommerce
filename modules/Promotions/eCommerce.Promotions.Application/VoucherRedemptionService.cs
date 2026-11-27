using System.Text.Json;
using eCommerce.Kernel.Observability;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace eCommerce.Promotions;

/// <summary>
/// **ĐƯỜNG GHI LƯỢT VOUCHER** (`PRO-001-arch` §6) — hợp đồng **nội bộ** cho `Sales`, gọi **in-process** trong
/// cùng UoW (`ADR-13`). 🚨 **KHÔNG** kế thừa `IApplicationService` ⇒ **không** có route HTTP.
///
/// 🔒 Hàng rào giành lượt là **`UPDATE … WHERE` có điều kiện** (`ADR-03`) — **KHÔNG** dùng distributed lock cho
/// giữ/chốt/nhả. Cấp `UsageSequence` đúng **`D-295`** (SAVEPOINT, 5 lần thử, `ON CONFLICT` **có target**).
/// </summary>
public class VoucherRedemptionService : IVoucherRedemptionService, ITransientDependency
{
    private const string KindVoucher = "voucher";
    private const string SpCapSo = "sp_seq";
    private const string SpChot = "sp_commit";

    private readonly IRepository<VoucherCode, Guid> _vouchers;
    private readonly IRepository<Promotion, Guid> _promotions;
    private readonly IRepository<PromotionRedemption, Guid> _redemptions;
    private readonly IPromotionEngine _engine;

    /// <summary>
    /// 🚨 Dùng <c>eCommerce.Messaging.Idempotency</c> (allowlist §2.2) nhưng **ghi tên đầy đủ** — không thêm
    /// `using eCommerce.Messaging…` để giữ test ranh giới hiện có xanh; `qa`/ORCH cập nhật allowlist **2** namespace.
    /// </summary>
    private readonly eCommerce.Messaging.Idempotency.IIdempotencyGuard _guard;
    private readonly IUsageSequenceAllocator _allocator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guid;
    private readonly IOptions<PromotionOptions> _options;
    private readonly ILogger<VoucherRedemptionService> _logger;
    private readonly eCommerceMetrics _metrics;

    public VoucherRedemptionService(
        IRepository<VoucherCode, Guid> vouchers,
        IRepository<Promotion, Guid> promotions,
        IRepository<PromotionRedemption, Guid> redemptions,
        IPromotionEngine engine,
        eCommerce.Messaging.Idempotency.IIdempotencyGuard guard,
        IUsageSequenceAllocator allocator,
        ICurrentTenant currentTenant,
        IClock clock,
        IGuidGenerator guid,
        IOptions<PromotionOptions> options,
        ILogger<VoucherRedemptionService> logger,
        eCommerceMetrics metrics)
    {
        _vouchers = vouchers;
        _promotions = promotions;
        _redemptions = redemptions;
        _engine = engine;
        _guard = guard;
        _allocator = allocator;
        _currentTenant = currentTenant;
        _clock = clock;
        _guid = guid;
        _options = options;
        _logger = logger;
        _metrics = metrics;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    //  ReserveAsync — giữ lượt khi báo giá (PRO-001-arch §6.1)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    public async Task<VoucherQuoteResultDto> ReserveAsync(ReserveVoucherDto input, CancellationToken cancellationToken = default)
    {
        // 1. Guard TƯỜNG MINH (không phải IApplicationService) + tenant.
        VoucherRequestValidator.KiemTraReserve(input);
        var tenantId = _currentTenant.Id;
        VoucherRequestValidator.KiemTraTenant(tenantId);

        var code = ChuanHoaCode(input.VoucherCode);
        var now = _clock.Now.ToUniversalTime();

        // 2. Chuẩn hoá + tìm mã theo tenant.
        var voucher = await _vouchers.FirstOrDefaultAsync(x => x.Code == code, cancellationToken: cancellationToken)
            ?? throw Loi(PromotionErrorCodes.VoucherNotFound).WithData("code", code);

        if (voucher.Status == VoucherCodeStatus.Revoked)
        {
            throw Loi(PromotionErrorCodes.VoucherRevoked).WithData("code", code);
        }

        // `Generated` (chưa phát hành) coi như **không tồn tại** (`PRO-001-arch` §6.1 bước 2).
        if (voucher.Status != VoucherCodeStatus.Issued)
        {
            throw Loi(PromotionErrorCodes.VoucherNotFound).WithData("code", code);
        }

        var promotion = await _promotions.GetAsync(voucher.PromotionId, cancellationToken: cancellationToken);
        if (now < promotion.StartsAtUtc.ToUniversalTime()
            || (promotion.EndsAtUtc is not null && now >= promotion.EndsAtUtc.Value.ToUniversalTime()))
        {
            throw Loi(PromotionErrorCodes.VoucherOutOfWindow).WithData("code", code);
        }

        // 3. Khách vãng lai ⇒ 0118 — TRƯỚC engine, TRƯỚC mọi ghi (D-146, BR-24).
        if (input.CustomerId is null)
        {
            throw Loi(PromotionErrorCodes.CustomerRequiredForVoucher);
        }

        // BR-17: voucher nội bộ chỉ phục vụ kênh `web` ở GĐ2.
        if (!string.Equals(input.ChannelCode, "web", StringComparison.OrdinalIgnoreCase))
        {
            throw Loi(PromotionErrorCodes.VoucherChannelNotAllowed).WithData("channelCode", input.ChannelCode);
        }

        var khoa = PromotionIdempotencyKeys.Checkout(tenantId, input.CheckoutSessionId, code);
        var payload = JsonSerializer.Serialize(input);

        // 4. Idempotency: cùng khoá + cùng payload ⇒ trả kết quả cũ; lượt cũ đã `Released` ⇒ 0117.
        var outcome = await _guard.ThucThiAsync(
            eCommerce.Messaging.Idempotency.IdempotencyKey.Cua("promotion:checkout", khoa),
            payload,
            async token => JsonSerializer.Serialize(
                await ThucHienReserveAsync(input, code, voucher, promotion, khoa, tenantId, now, token)),
            cancellationToken);

        if (outcome.Kind == eCommerce.Messaging.Idempotency.IdempotencyOutcomeKind.XungDotPayload)
        {
            throw new BusinessException(eCommerce.Messaging.MessagingErrorCodes.PayloadConflict).WithData("idempotencyKey", khoa);
        }

        if (outcome.Kind == eCommerce.Messaging.Idempotency.IdempotencyOutcomeKind.DangXuLy)
        {
            throw new BusinessException(eCommerce.Messaging.MessagingErrorCodes.DangXuLy).WithData("idempotencyKey", khoa);
        }

        var quote = JsonSerializer.Deserialize<VoucherQuoteResultDto>(outcome.Result ?? "{}")
                    ?? new VoucherQuoteResultDto { Currency = input.Currency, IdempotencyKey = khoa };

        if (outcome.Kind == eCommerce.Messaging.Idempotency.IdempotencyOutcomeKind.TraVeKetQuaCu)
        {
            // Lượt cũ đã `Released` (hết TTL) ⇒ **cấm** trả kết quả cũ; Sales mở phiên mới (A2, TC-51).
            var cu = await _redemptions.FirstOrDefaultAsync(x => x.IdempotencyKey == khoa, cancellationToken: cancellationToken);
            if (cu is not null && cu.Status == PromotionRedemptionStatus.Released)
            {
                throw Loi(PromotionErrorCodes.ReservationReleasedReplay).WithData("idempotencyKey", khoa);
            }

            GhiMetricGiu("duplicate");
            return quote;
        }

        GhiMetricGiu(quote.RedemptionId is null ? "rejected" : "ok");
        return quote;
    }

    private async Task<VoucherQuoteResultDto> ThucHienReserveAsync(
        ReserveVoucherDto input,
        string code,
        VoucherCode voucher,
        Promotion promotion,
        string khoa,
        Guid? tenantId,
        DateTime now,
        CancellationToken ct)
    {
        // 5. Đánh giá cây + điều kiện bằng engine (CHỈ ĐỌC).
        var kq = await _engine.EvaluateAsync(new PromotionEvaluationRequest(
            Guid.Empty, // engine KHÔNG dùng; khoá idempotency thuộc đường ghi của Promotions.
            input.Lines ?? Array.Empty<PromotionCartLine>(),
            input.ChannelCode,
            input.Currency,
            input.Subtotal,
            input.CustomerId,
            CustomerGroupId: null, // Q26 fail-closed (D-298): 2 lá khách luôn false ở v1.
            IsFirstOrder: null,
            VoucherCode: code,
            Mode: PromotionApplyMode.CustomerRequested), ct);

        var voucherOutcome = kq.VoucherOutcome;
        if (voucherOutcome is null
            || !voucherOutcome.Applied
            || decimal.Compare(voucherOutcome.DiscountTotal, 0m) <= 0)
        {
            // Cây `false` ⇒ báo giá KHÔNG giảm + lý do — **không** ném, **không** ghi (PRO-001-arch §6.1 bước 5).
            return new VoucherQuoteResultDto
            {
                RedemptionId = null,
                DiscountTotal = 0m,
                Currency = input.Currency,
                ReasonCode = voucherOutcome?.RejectionReason ?? kq.RejectionReason,
                IdempotencyKey = khoa,
                ExpiresAtUtc = null
            };
        }

        var discount = voucherOutcome.DiscountTotal;
        var db = await _redemptions.GetDbContextAsync();

        // 6. Giành lượt ở DB — mã TRƯỚC, promotion SAU (D-147 (2)); 0 dòng ⇒ NÉM.
        var soMa = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProVoucherCodes""
   SET ""ReservedCount"" = ""ReservedCount"" + 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {voucher.Id}
   AND ""Status"" = 'Issued'
   AND ""UsedCount"" + ""ReservedCount"" < ""MaxUsage""", ct);
        if (soMa == 0)
        {
            throw Loi(PromotionErrorCodes.UsageLimitExceeded).WithData("voucherCodeId", voucher.Id);
        }

        if (promotion.MaxUsageTotal is not null)
        {
            var soP = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotions""
   SET ""ReservedCount"" = ""ReservedCount"" + 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {promotion.Id}
   AND ""UsedCount"" + ""ReservedCount"" < ""MaxUsageTotal""", ct);
            if (soP == 0)
            {
                throw Loi(PromotionErrorCodes.PromotionUsageLimitExceeded).WithData("promotionId", promotion.Id);
            }
        }

        // 7. Cấp `UsageSequence` + INSERT (D-295).
        var daDungRows = await _redemptions.GetListAsync(
            x => x.PromotionId == promotion.Id
                 && x.CustomerId == input.CustomerId
                 && x.Status != PromotionRedemptionStatus.Released,
            cancellationToken: ct);
        var daDung = daDungRows.Select(x => x.UsageSequence).ToHashSet();

        var batDau = UsageSequenceAllocator.TimSoBatDau(daDung, promotion.MaxUsagePerCustomer, daDungRows.Count);
        if (batDau is null)
        {
            // Hết số trong [1, MaxUsagePerCustomer] ⇒ NÉM **ngay** 0104 (retry không nới trần khách).
            throw Loi(PromotionErrorCodes.CustomerUsageLimitExceeded)
                .WithData("promotionId", promotion.Id);
        }

        var redemptionId = _guid.Create();
        var expires = now.AddSeconds(input.TtlSeconds);
        var breakdownJson = JsonSerializer.Serialize(kq.Breakdown);
        var soLanThu = _options.Value.MaxUsageSequenceAttempts;

        var seq = await _allocator.ThuAsync(
            batDau.Value,
            promotion.MaxUsagePerCustomer,
            soLanThu,
            async (thu, token) => await ChenLuotAsync(
                db, redemptionId, tenantId, promotion.Id, promotion.TreeVersion, voucher.Id, input, code, thu,
                discount, breakdownJson, expires, now, khoa, token),
            ct);

        if (seq is null)
        {
            // Hết 5 lần thử do đua ⇒ 0104 + LogWarning (KHÔNG log mã/khách) + metric usage_sequence_exhausted.
            GhiMetricGiu("usage_sequence_exhausted");
            _logger.LogWarning(
                "PROMOTION: cạn UsageSequence sau {SoLan} lần thử — trả 0104 (không log mã khách).", soLanThu);
            throw Loi(PromotionErrorCodes.CustomerUsageLimitExceeded)
                .WithData("promotionId", promotion.Id);
        }

        return new VoucherQuoteResultDto
        {
            RedemptionId = redemptionId,
            DiscountTotal = discount,
            Currency = input.Currency,
            ReasonCode = null,
            IdempotencyKey = khoa,
            ExpiresAtUtc = expires
        };
    }

    /// <summary>
    /// `INSERT … ON CONFLICT (target) WHERE … DO NOTHING` trong **SAVEPOINT** (`D-295`).
    /// Trả `false` = **0 dòng** = trùng SỐ (thử tiếp); ném khi `23505` từ index **KHÁC** (không thử tiếp).
    /// </summary>
    private static async Task<bool> ChenLuotAsync(
        DbContext db,
        Guid redemptionId,
        Guid? tenantId,
        Guid promotionId,
        int treeVersion,
        Guid voucherId,
        ReserveVoucherDto input,
        string code,
        int thu,
        decimal discount,
        string breakdownJson,
        DateTime expires,
        DateTime now,
        string khoa,
        CancellationToken ct)
    {
        var customerId = input.CustomerId;
        var tx = db.Database.CurrentTransaction;
        if (tx is not null)
        {
            await tx.CreateSavepointAsync(SpCapSo, ct);
        }

        try
        {
            var soDong = await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""ProPromotionRedemptions""
    (""Id"",""TenantId"",""PromotionId"",""VoucherCodeId"",""OrderId"",""Status"",""ExpiresAtUtc"",""UsageSequence"",
     ""ReasonCode"",""CustomerId"",""AppliedAtUtc"",""ReleasedAtUtc"",""ReversedAtUtc"",""IdempotencyKey"",
     ""DiscountAmount"",""Currency"",""BreakdownJson"",""TreeVersion"",""EvaluatedAtUtc"")
VALUES
    ({redemptionId},{tenantId},{promotionId},{voucherId},NULL,0,{expires},{thu},
     NULL,{customerId},NULL,NULL,NULL,{khoa},
     {discount},{input.Currency},{breakdownJson}::jsonb,{treeVersion},{now})
ON CONFLICT (""TenantId"",""PromotionId"",""CustomerId"",""UsageSequence"")
       WHERE ""CustomerId"" IS NOT NULL AND ""Status"" <> 2
DO NOTHING", ct);

            if (tx is not null)
            {
                await tx.ReleaseSavepointAsync(SpCapSo, ct);
            }

            return soDong == 1;
        }
        catch (Exception ex)
        {
            if (tx is not null)
            {
                try
                {
                    await tx.RollbackToSavepointAsync(SpCapSo, ct);
                }
                catch
                {
                    // Không che lỗi gốc.
                }
            }

            // `23505` từ index KHÁC (v.d. `IdempotencyKey`) ⇒ NÉM trong SAVEPOINT, KHÔNG thử tiếp (`D-295`).
            if (LaLoiTrungKhoa(ex))
            {
                throw new BusinessException(eCommerce.Messaging.MessagingErrorCodes.PayloadConflict)
                    .WithData("idempotencyKey", khoa);
            }

            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    //  CommitAsync — chốt lượt cùng UoW tạo đơn (PRO-001-arch §6.3)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    public async Task<VoucherAppliedDto> CommitAsync(CommitVoucherDto input, CancellationToken cancellationToken = default)
    {
        VoucherRequestValidator.KiemTraCommit(input);
        var tenantId = _currentTenant.Id;
        VoucherRequestValidator.KiemTraTenant(tenantId);

        var code = ChuanHoaCode(input.VoucherCode);
        var now = _clock.Now.ToUniversalTime();

        // TRƯỚC guard: kiểm (tenant, redemptionId) + khoá phiên ⇒ lệch 0105 (D-298 ①(4)).
        var redemption = await _redemptions.FirstOrDefaultAsync(x => x.Id == input.RedemptionId, cancellationToken: cancellationToken)
            ?? throw Loi(PromotionErrorCodes.RedemptionNotReserved).WithData("redemptionId", input.RedemptionId);

        var khoaPhienMongDoi = PromotionIdempotencyKeys.Checkout(tenantId, input.CheckoutSessionId, code);
        if (!string.Equals(redemption.IdempotencyKey, khoaPhienMongDoi, StringComparison.Ordinal))
        {
            throw Loi(PromotionErrorCodes.RedemptionNotReserved)
                .WithData("redemptionId", input.RedemptionId)
                .WithData("lyDo", "Khoá phiên không khớp lượt.");
        }

        var discount = redemption.DiscountAmount;
        var currency = redemption.Currency;
        var khoaChot = PromotionIdempotencyKeys.Commit(tenantId, input.OrderId, code);
        var payload = JsonSerializer.Serialize(new { input.RedemptionId, input.OrderId, Code = code });

        var outcome = await _guard.ThucThiAsync(
            eCommerce.Messaging.Idempotency.IdempotencyKey.Cua("promotion:commit", khoaChot),
            payload,
            async token =>
            {
                await ThucHienCommitAsync(input, redemption, tenantId, now, token);
                return JsonSerializer.Serialize(new VoucherAppliedDto
                {
                    RedemptionId = input.RedemptionId,
                    OrderId = input.OrderId,
                    Status = PromotionRedemptionStatus.Applied,
                    DiscountAmount = discount,
                    Currency = currency,
                    AlreadyApplied = false
                });
            },
            cancellationToken);

        if (outcome.Kind == eCommerce.Messaging.Idempotency.IdempotencyOutcomeKind.XungDotPayload)
        {
            throw new BusinessException(eCommerce.Messaging.MessagingErrorCodes.PayloadConflict).WithData("idempotencyKey", khoaChot);
        }

        if (outcome.Kind == eCommerce.Messaging.Idempotency.IdempotencyOutcomeKind.DangXuLy)
        {
            throw new BusinessException(eCommerce.Messaging.MessagingErrorCodes.DangXuLy).WithData("idempotencyKey", khoaChot);
        }

        var applied = JsonSerializer.Deserialize<VoucherAppliedDto>(outcome.Result ?? "{}")
                      ?? new VoucherAppliedDto { RedemptionId = input.RedemptionId, OrderId = input.OrderId, DiscountAmount = discount, Currency = currency };

        if (outcome.Kind == eCommerce.Messaging.Idempotency.IdempotencyOutcomeKind.TraVeKetQuaCu)
        {
            applied.AlreadyApplied = true;
        }

        return applied;
    }

    private async Task ThucHienCommitAsync(CommitVoucherDto input, PromotionRedemption redemption, Guid? tenantId, DateTime now, CancellationToken ct)
    {
        var db = await _redemptions.GetDbContextAsync();
        var tx = db.Database.CurrentTransaction;
        if (tx is not null)
        {
            await tx.CreateSavepointAsync(SpChot, ct);
        }

        int soDong;

        try
        {
            soDong = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotionRedemptions""
   SET ""Status"" = 1, ""OrderId"" = {input.OrderId}, ""AppliedAtUtc"" = {now}
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {input.RedemptionId}
   AND ""Status"" = 0 AND ""ExpiresAtUtc"" > {now}", ct);

            if (tx is not null)
            {
                await tx.ReleaseSavepointAsync(SpChot, ct);
            }
        }
        catch (Exception ex)
        {
            if (tx is not null)
            {
                try
                {
                    await tx.RollbackToSavepointAsync(SpChot, ct);
                }
                catch
                {
                }
            }

            // Vi phạm unique `(TenantId, OrderId)` (mã thứ hai cho cùng đơn) ⇒ 0115.
            if (LaLoiTrungKhoa(ex))
            {
                throw Loi(PromotionErrorCodes.SecondVoucherOnOrder).WithData("orderId", input.OrderId);
            }

            throw;
        }

        if (soDong == 0)
        {
            // Lượt không còn `Reserved` hoặc đã hết TTL ⇒ Sales DỪNG, báo giá lại (cấm đổi giá im lặng).
            throw Loi(PromotionErrorCodes.RedemptionNotReserved)
                .WithData("redemptionId", input.RedemptionId)
                .WithData("orderId", input.OrderId);
        }

        // Bộ đếm: `ReservedCount − 1`, `UsedCount + 1` — mã TRƯỚC, promotion SAU.
        if (redemption.VoucherCodeId is Guid voucherId)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProVoucherCodes""
   SET ""ReservedCount"" = ""ReservedCount"" - 1, ""UsedCount"" = ""UsedCount"" + 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {voucherId}
   AND ""ReservedCount"" > 0", ct);
        }

        var promotion = await _promotions.GetAsync(redemption.PromotionId, cancellationToken: ct);
        if (promotion.MaxUsageTotal is not null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotions""
   SET ""ReservedCount"" = ""ReservedCount"" - 1, ""UsedCount"" = ""UsedCount"" + 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {promotion.Id}
   AND ""ReservedCount"" > 0", ct);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    //  ReleaseAsync (Reserved → Released) · ReverseAsync (Applied → Reversed)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    public async Task ReleaseAsync(ReleaseVoucherDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = _currentTenant.Id;
        VoucherRequestValidator.KiemTraTenant(tenantId);
        var now = _clock.Now.ToUniversalTime();

        var redemption = await _redemptions.FirstOrDefaultAsync(x => x.Id == input.RedemptionId, cancellationToken: cancellationToken);
        if (redemption is null)
        {
            return; // 0 dòng = bình thường (idempotent).
        }

        var reason = string.IsNullOrWhiteSpace(input.ReasonCode) ? "MANUAL" : input.ReasonCode;
        var db = await _redemptions.GetDbContextAsync();

        var soDong = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotionRedemptions""
   SET ""Status"" = 2, ""ReleasedAtUtc"" = {now}, ""ReasonCode"" = {reason}
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {input.RedemptionId}
   AND ""Status"" = 0", cancellationToken);
        if (soDong == 0)
        {
            return; // Đã Released/không còn Reserved ⇒ bỏ qua (idempotent).
        }

        if (redemption.VoucherCodeId is Guid voucherId)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProVoucherCodes"" SET ""ReservedCount"" = ""ReservedCount"" - 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {voucherId} AND ""ReservedCount"" > 0", cancellationToken);
        }

        var promotion = await _promotions.GetAsync(redemption.PromotionId, cancellationToken: cancellationToken);
        if (promotion.MaxUsageTotal is not null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotions"" SET ""ReservedCount"" = ""ReservedCount"" - 1
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {promotion.Id} AND ""ReservedCount"" > 0", cancellationToken);
        }

        _metrics.PromotionVoucherReleased.Add(
            1,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, HopLeNhanMetric(reason, "MANUAL")));
    }

    public async Task ReverseAsync(ReverseVoucherDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = _currentTenant.Id;
        VoucherRequestValidator.KiemTraTenant(tenantId);
        var now = _clock.Now.ToUniversalTime();

        var redemption = await _redemptions.FirstOrDefaultAsync(
            x => x.OrderId == input.OrderId && x.Status == PromotionRedemptionStatus.Applied,
            cancellationToken: cancellationToken);
        if (redemption is null)
        {
            return; // Không có lượt của đơn ⇒ bỏ qua (không DLQ).
        }

        var reason = string.IsNullOrWhiteSpace(input.ReasonCode) ? "ORDER_RETURNED" : input.ReasonCode;
        var db = await _redemptions.GetDbContextAsync();

        // `Applied → Reversed` — **KHÔNG** hoàn `UsedCount` (BR-25 / §6.4).
        await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""ProPromotionRedemptions""
   SET ""Status"" = 3, ""ReversedAtUtc"" = {now}, ""ReasonCode"" = {reason}
 WHERE ""TenantId"" IS NOT DISTINCT FROM {tenantId} AND ""Id"" = {redemption.Id}
   AND ""Status"" = 1", cancellationToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────

    private static string ChuanHoaCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static BusinessException Loi(string code) => new(code);

    private static string HopLeNhanMetric(string reason, string macDinh)
        => reason is "TTL_EXPIRED" or "ORDER_CANCELLED" or "ORDER_RETURNED" or "MANUAL" ? reason : macDinh;

    private void GhiMetricGiu(string result)
        => _metrics.PromotionVoucherReserved.Add(
            1,
            new KeyValuePair<string, object?>(eCommerceMetricTags.Kind, KindVoucher),
            new KeyValuePair<string, object?>(eCommerceMetricTags.Result, result));

    /// <summary>Phát hiện `23505` PostgreSQL mà **không** phụ thuộc trực tiếp `Npgsql` (khuôn Notification).</summary>
    private static bool LaLoiTrungKhoa(Exception ex)
    {
        for (var e = (Exception?)ex; e is not null; e = e.InnerException)
        {
            var type = e.GetType();
            if (type.Name == "PostgresException"
                && type.GetProperty("SqlState")?.GetValue(e) as string == "23505")
            {
                return true;
            }
        }

        return false;
    }
}
