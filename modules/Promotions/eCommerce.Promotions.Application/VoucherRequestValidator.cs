using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using eCommerce.Promotions.Contracts;
using Volo.Abp;
using Volo.Abp.Validation;

namespace eCommerce.Promotions;

/// <summary>
/// **GUARD TƯỜNG MINH** cho <see cref="ReserveVoucherDto"/>/<see cref="CommitVoucherDto"/> (`PRO-001-arch` §6.1 bước 1).
///
/// 🚨 Vì sao **phải** guard tay: `VoucherRedemptionService` **không** kế thừa `IApplicationService` ⇒ interceptor
/// DataAnnotations của ABP **không tự chạy** trên DTO. Guard ném <see cref="AbpValidationException"/> (HTTP **400**,
/// **0** mã lỗi mới, **0** dòng ghi). **Cấm** `ArgumentException` (ABP **không** map 400 ⇒ rơi 500).
/// </summary>
public static class VoucherRequestValidator
{
    public const decimal SubtotalToiThieu = 0.01m;
    public const decimal SubtotalToiDa = 99999999999999.9999m;
    public const int TtlToiThieu = 60;
    public const int TtlToiDa = 86400;
    public const int SessionToiDa = 128;

    private static readonly Regex MaTienTe = new("^[A-Z]{3}$", RegexOptions.CultureInvariant);

    public static void KiemTraReserve(ReserveVoucherDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var loi = new List<ValidationResult>();

        if (input.Subtotal < SubtotalToiThieu || input.Subtotal > SubtotalToiDa)
        {
            loi.Add(new ValidationResult(
                $"Subtotal phải trong [{SubtotalToiThieu}, {SubtotalToiDa}] (nhận {input.Subtotal}).",
                new[] { nameof(ReserveVoucherDto.Subtotal) }));
        }

        if (string.IsNullOrWhiteSpace(input.Currency) || !MaTienTe.IsMatch(input.Currency))
        {
            loi.Add(new ValidationResult(
                "Currency phải là 3 chữ HOA (ISO-4217), KHÔNG tự .ToUpper().",
                new[] { nameof(ReserveVoucherDto.Currency) }));
        }

        if (input.TtlSeconds < TtlToiThieu || input.TtlSeconds > TtlToiDa)
        {
            loi.Add(new ValidationResult(
                $"TtlSeconds phải trong [{TtlToiThieu}, {TtlToiDa}].",
                new[] { nameof(ReserveVoucherDto.TtlSeconds) }));
        }

        KiemTraSession(input.CheckoutSessionId, loi);

        if (loi.Count > 0)
        {
            throw new AbpValidationException(loi);
        }
    }

    public static void KiemTraCommit(CommitVoucherDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var loi = new List<ValidationResult>();
        KiemTraSession(input.CheckoutSessionId, loi);

        if (loi.Count > 0)
        {
            throw new AbpValidationException(loi);
        }
    }

    /// <summary>
    /// 🚨 `ICurrentTenant.Id == Guid.Empty` (`00000000-…`) ⇒ **NÉM** `0112`; `null` là **ca 1-tenant hợp lệ**
    /// (**không** ném — `PRO-001-arch` §7.3, `D-297` docrev vòng 1 P2 #4).
    /// </summary>
    public static void KiemTraTenant(Guid? tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new BusinessException(PromotionErrorCodes.MissingTenantContext);
        }
    }

    private static void KiemTraSession(string? session, ICollection<ValidationResult> loi)
    {
        if (string.IsNullOrEmpty(session) || session.Length > SessionToiDa)
        {
            loi.Add(new ValidationResult(
                $"CheckoutSessionId bắt buộc, 1…{SessionToiDa} ký tự.",
                new[] { nameof(ReserveVoucherDto.CheckoutSessionId) }));
        }
    }
}
