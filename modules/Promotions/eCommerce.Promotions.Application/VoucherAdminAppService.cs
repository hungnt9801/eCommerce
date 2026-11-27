using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using eCommerce.Promotions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Volo.Abp.Tracing;
using Volo.Abp.Validation;

namespace eCommerce.Promotions;

/// <summary>
/// **Quản trị voucher** (`PRO-001` §6.1) — **ĐÚNG 7 method** (`TC-40`). 🔐 `[Authorize]` cấp **CLASS** +
/// permission hẹp từng method (`BR-15`, `A1`).
///
/// 🔒 Audit hành động rủi ro tiền (`D-299`): `RevokeAsync`/`OverrideLimitAsync` giữ `reason` là **tham số method**
/// ⇒ ABP auditing ghi `AbpAuditLogActions.Parameters`; thêm **một** `LogInformation` có `CorrelationId`.
/// **Cấm** log mã voucher/PII (`BR-22`).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Promotions.Default)]
public class VoucherAdminAppService : ApplicationService, IVoucherAdminAppService
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int PrefixToiDa = 8;
    private const int DoDaiMaToiThieu = 3;
    private const int DoDaiMaToiDa = 32;

    private readonly IRepository<VoucherCode, Guid> _vouchers;
    private readonly IRepository<Promotion, Guid> _promotions;
    private readonly IRepository<PromotionRedemption, Guid> _redemptions;
    private readonly IGuidGenerator _guid;
    private readonly IClock _clock;
    private readonly ICorrelationIdProvider _correlationId;

    public VoucherAdminAppService(
        IRepository<VoucherCode, Guid> vouchers,
        IRepository<Promotion, Guid> promotions,
        IRepository<PromotionRedemption, Guid> redemptions,
        IGuidGenerator guid,
        IClock clock,
        ICorrelationIdProvider correlationId)
    {
        _vouchers = vouchers;
        _promotions = promotions;
        _redemptions = redemptions;
        _guid = guid;
        _clock = clock;
        _correlationId = correlationId;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<PagedResultDto<VoucherDto>> GetListAsync(VoucherFilterDto input)
    {
        var ds = await _vouchers.GetListAsync(x =>
            (input.PromotionId == null || x.PromotionId == input.PromotionId) &&
            (input.Status == null || x.Status == input.Status) &&
            (input.Code == null || x.Code.Contains(input.Code)));

        var tong = ds.Count;
        var trang = ds.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        var khuyenMai = await NapKhuyenMaiAsync(trang);
        var now = _clock.Now.ToUniversalTime();

        return new PagedResultDto<VoucherDto>(tong, trang.Select(x => Map(x, khuyenMai, now)).ToList());
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<VoucherDetailDto> GetAsync(Guid id)
    {
        var voucher = await _vouchers.GetAsync(id);
        var promotion = await _promotions.GetAsync(voucher.PromotionId);
        var now = _clock.Now.ToUniversalTime();

        var dto = new VoucherDetailDto
        {
            MaxUsagePerCustomer = promotion.MaxUsagePerCustomer,
            PromotionCode = promotion.Code
        };
        Map(voucher, promotion, now, dto);
        return dto;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherGenerate)]
    public async Task<GeneratedVouchersDto> GenerateAsync(GenerateVouchersDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var promotion = await _promotions.GetAsync(input.PromotionId);
        if (promotion.Status is not (PromotionStatus.Approved or PromotionStatus.Active))
        {
            throw new BusinessException(PromotionErrorCodes.PromotionNotApproved)
                .WithData("promotionId", promotion.Id)
                .WithData("status", promotion.Status.ToString());
        }

        var maxBatch = PromotionSettings.DocMaxBatchSize(
            await SettingProvider.GetOrNullAsync(PromotionSettingNames.MaxBatchSize));

        if (input.Quantity < 1 || input.MaxUsage < 1)
        {
            throw new AbpValidationException(new List<ValidationResult>
            {
                new("Quantity và MaxUsage phải ≥ 1.")
            });
        }

        // 🚨 Vượt trần ⇒ 0108 TRƯỚC khi ghi dòng nào (D-302).
        if (input.Quantity > maxBatch)
        {
            throw new BusinessException(PromotionErrorCodes.BatchSizeExceeded)
                .WithData("quantity", input.Quantity)
                .WithData("maxBatchSize", maxBatch);
        }

        var prefix = ChuanHoaPrefix(input.Prefix);
        var doDai = input.Length;
        if (doDai < DoDaiMaToiThieu || doDai > DoDaiMaToiDa || prefix.Length >= doDai)
        {
            throw new BusinessException(PromotionErrorCodes.VoucherCodeInvalid)
                .WithData("length", doDai)
                .WithData("prefix", prefix);
        }

        // 🔒 D-309 (P2 #1): PRE-CHECK dung lượng không gian mã TRƯỚC vòng sinh — chống treo vô hạn.
        // Input HỢP LỆ theo guard trên vẫn có thể bất khả thi: {Length:3, Prefix:"AB", Quantity:100}
        // chỉ có 36^(3−2)=36 mã phân biệt < 100 ⇒ vòng `while` dưới KHÔNG BAO GIỜ đủ ⇒ treo.
        // Thiếu chỗ ⇒ NÉM `0107` VoucherCodeInvalid (giữ số đã cấp, KHÔNG thêm mã) thay vì treo.
        // Nhân có trần `>= Quantity` để tránh tràn `long` khi `doDai` tới `DoDaiMaToiDa` (32).
        var soKyTuNga = doDai - prefix.Length; // ≥ 1 nhờ guard `prefix.Length >= doDai` ở trên.
        long codespace = 1;
        for (var i = 0; i < soKyTuNga && codespace < input.Quantity; i++)
        {
            codespace *= Alphabet.Length;
        }

        if (codespace < input.Quantity)
        {
            throw new BusinessException(PromotionErrorCodes.VoucherCodeInvalid)
                .WithData("length", doDai)
                .WithData("prefix", prefix)
                .WithData("codespace", codespace)
                .WithData("quantity", input.Quantity);
        }

        var ma = new HashSet<string>(StringComparer.Ordinal);
        while (ma.Count < input.Quantity)
        {
            ma.Add(TaoMa(prefix, doDai));
        }

        var now = _clock.Now.ToUniversalTime();
        foreach (var code in ma)
        {
            var voucher = VoucherCode.TaoMoi(
                _guid.Create(), CurrentTenant.Id, promotion.Id, code, input.MaxUsage, VoucherCodeStatus.Generated);
            await _vouchers.InsertAsync(voucher, autoSave: false);
        }

        return new GeneratedVouchersDto
        {
            PromotionId = promotion.Id,
            Codes = ma.ToList(),
            Count = ma.Count
        };
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherPublish)]
    public async Task<VoucherDto> IssueAsync(Guid id)
    {
        var voucher = await _vouchers.GetAsync(id);
        var promotion = await _promotions.GetAsync(voucher.PromotionId);

        if (promotion.Status is not (PromotionStatus.Approved or PromotionStatus.Active))
        {
            throw new BusinessException(PromotionErrorCodes.PromotionNotApproved)
                .WithData("promotionId", promotion.Id)
                .WithData("status", promotion.Status.ToString());
        }

        var now = _clock.Now.ToUniversalTime();
        voucher.Issue(now);
        await _vouchers.UpdateAsync(voucher, autoSave: false);

        return Map(voucher, promotion, now);
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherPublish)]
    public async Task<VoucherDto> RevokeAsync(Guid id, string reason)
    {
        KiemTraLyDo(reason, nameof(reason));

        var voucher = await _vouchers.GetAsync(id);
        var promotion = await _promotions.GetAsync(voucher.PromotionId);

        var now = _clock.Now.ToUniversalTime();
        voucher.Revoke(now);
        await _vouchers.UpdateAsync(voucher, autoSave: false);

        // Audit: 1 dòng log có CorrelationId + id (KHÔNG log mã voucher/PII — BR-22).
        Logger.LogInformation(
            "PROMOTION VOUCHER REVOKE: voucherId={VoucherId} correlationId={CorrelationId}",
            voucher.Id, _correlationId.Get());

        return Map(voucher, promotion, now);
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherOverrideLimit)]
    public async Task<VoucherDto> OverrideLimitAsync(Guid id, int newMaxUsage, string reason)
    {
        KiemTraLyDo(reason, nameof(reason));

        var voucher = await _vouchers.GetAsync(id);
        var promotion = await _promotions.GetAsync(voucher.PromotionId);

        voucher.DoiGioiHan(newMaxUsage);
        await _vouchers.UpdateAsync(voucher, autoSave: false);

        Logger.LogInformation(
            "PROMOTION VOUCHER OVERRIDE-LIMIT: voucherId={VoucherId} maxUsage={MaxUsage} correlationId={CorrelationId}",
            voucher.Id, voucher.MaxUsage, _correlationId.Get());

        return Map(voucher, promotion, _clock.Now.ToUniversalTime());
    }

    /// <summary>
    /// Sổ lượt — `Status = Reserved` phục vụ CSKH (`D-302`). Trả `ExpiresAtUtc` additive (**không** trả
    /// `IdempotencyKey`).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<PagedResultDto<RedemptionDto>> GetRedemptionsAsync(RedemptionFilterDto input)
    {
        var ds = await _redemptions.GetListAsync(x =>
            (input.OrderId == null || x.OrderId == input.OrderId) &&
            (input.Status == null || x.Status == input.Status) &&
            (input.CustomerId == null || x.CustomerId == input.CustomerId) &&
            (input.PromotionId == null || x.PromotionId == input.PromotionId));

        var tong = ds.Count;
        var items = ds.OrderByDescending(x => x.EvaluatedAtUtc).ThenBy(x => x.Id)
            .Skip(input.SkipCount).Take(input.MaxResultCount)
            .Select(x => new RedemptionDto
            {
                Id = x.Id,
                PromotionId = x.PromotionId,
                VoucherCodeId = x.VoucherCodeId,
                OrderId = x.OrderId,
                Status = x.Status,
                UsageSequence = x.UsageSequence,
                ReasonCode = x.ReasonCode,
                CustomerId = x.CustomerId,
                DiscountAmount = x.DiscountAmount,
                Currency = x.Currency,
                TreeVersion = x.TreeVersion,
                EvaluatedAtUtc = x.EvaluatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc
            })
            .ToList();

        return new PagedResultDto<RedemptionDto>(tong, items);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────

    private static void KiemTraLyDo(string? reason, string thamSo)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
        {
            throw new AbpValidationException(new List<ValidationResult>
            {
                new("Lý do bắt buộc ≥ 10 ký tự.", new[] { thamSo })
            });
        }
    }

    private static string ChuanHoaPrefix(string? prefix)
    {
        var p = (prefix ?? string.Empty).Trim().ToUpperInvariant();
        if (p.Length > PrefixToiDa)
        {
            throw new BusinessException(PromotionErrorCodes.VoucherCodeInvalid)
                .WithData("prefix", p)
                .WithData("lyDo", "Prefix quá dài.");
        }

        foreach (var c in p)
        {
            if (!(c is >= 'A' and <= 'Z' || c is >= '0' and <= '9' || c == '-'))
            {
                throw new BusinessException(PromotionErrorCodes.VoucherCodeInvalid)
                    .WithData("prefix", p)
                    .WithData("lyDo", "Prefix chỉ gồm A-Z, 0-9, '-'.");
            }
        }

        return p;
    }

    private static string TaoMa(string prefix, int length)
    {
        var sb = new StringBuilder(length);
        sb.Append(prefix);
        for (var i = prefix.Length; i < length; i++)
        {
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }

        return sb.ToString();
    }

    private async Task<Dictionary<Guid, Promotion>> NapKhuyenMaiAsync(IReadOnlyCollection<VoucherCode> vouchers)
    {
        var ids = vouchers.Select(x => x.PromotionId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Promotion>();
        }

        var ds = await _promotions.GetListAsync(x => ids.Contains(x.Id));
        return ds.ToDictionary(x => x.Id);
    }

    private static VoucherDto Map(VoucherCode v, Promotion? promotion, DateTime now)
    {
        var dto = new VoucherDto();
        Map(v, promotion, now, dto);
        return dto;
    }

    private static VoucherDto Map(VoucherCode v, IReadOnlyDictionary<Guid, Promotion> khuyenMai, DateTime now)
    {
        Promotion? promotion = khuyenMai.TryGetValue(v.PromotionId, out var p) ? p : null;
        return Map(v, promotion, now);
    }

    private static void Map(VoucherCode v, Promotion? promotion, DateTime now, VoucherDto dto)
    {
        dto.Id = v.Id;
        dto.PromotionId = v.PromotionId;
        dto.Code = v.Code;
        dto.MaxUsage = v.MaxUsage;
        dto.UsedCount = v.UsedCount;
        dto.ReservedCount = v.ReservedCount;
        dto.Status = v.Status;
        dto.RemainingUses = v.MaxUsage - v.UsedCount - v.ReservedCount;
        dto.IsExhausted = dto.RemainingUses <= 0;
        dto.Currency = promotion?.Currency ?? string.Empty;
        dto.StartsAtUtc = promotion?.StartsAtUtc;
        dto.EndsAtUtc = promotion?.EndsAtUtc;
        dto.IsExpired = promotion?.EndsAtUtc is not null && now >= promotion.EndsAtUtc.Value.ToUniversalTime();
    }
}
