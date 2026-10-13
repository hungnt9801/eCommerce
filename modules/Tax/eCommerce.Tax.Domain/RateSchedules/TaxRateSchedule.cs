using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tax.RateSchedules;

/// <summary>
/// Một mức thuế suất GTGT có **khung hiệu lực theo thời gian**.
///
/// 🚨 BR-XC-006-001: thuế suất là HÀM CỦA THỜI GIAN tại thời điểm lập hoá đơn.
///    Lý do: mức 8% chỉ có hiệu lực đến hết **31/12/2026** (NQ 204/2025/QH15).
///    Nếu hard-code thuế suất ⇒ hệ thống tự động tính SAI từ 00:00 ngày 01/01/2027.
///
/// 🚨 BR-011 (đã chốt ở Q6): đổi khung hiệu lực phải qua duyệt + audit, và
///    **CẤM đặt hiệu lực lùi về quá khứ** vì sẽ làm thay đổi cách tính của chứng từ đã phát hành.
/// </summary>
public class TaxRateSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã mức, ví dụ <c>VAT10</c>, <c>VAT8</c>. Unique theo tenant.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    /// <summary>Mức **phần trăm** (10.0000 = 10%). decimal — không bao giờ dùng double.</summary>
    public decimal Rate { get; protected set; }

    public TaxRateKind Kind { get; protected set; }

    /// <summary>Với mức <see cref="TaxRateKind.Reduced"/>: mã mức bị giảm, ví dụ <c>VAT10</c>.</summary>
    public string? ReducedFromCode { get; protected set; }

    public DateOnly EffectiveFrom { get; protected set; }

    /// <summary><c>null</c> = vô hạn. Với <c>VAT8</c> giá trị này là <c>2026-12-31</c>.</summary>
    public DateOnly? EffectiveTo { get; protected set; }

    /// <summary>Căn cứ pháp lý, ví dụ "NQ 204/2025/QH15 + NĐ 174/2025/NĐ-CP".</summary>
    public string? LegalBasis { get; protected set; }

    protected TaxRateSchedule()
    {
    }

    public TaxRateSchedule(
        Guid id,
        string code,
        string name,
        decimal rate,
        TaxRateKind kind,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        string? reducedFromCode = null,
        string? legalBasis = null) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), maxLength: 32);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);

        if (rate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "Thuế suất phải trong khoảng 0..100 (phần trăm).");
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo phải >= EffectiveFrom.", nameof(effectiveTo));
        }

        if (kind == TaxRateKind.Reduced && string.IsNullOrWhiteSpace(reducedFromCode))
        {
            throw new ArgumentException("Mức giảm bắt buộc phải có ReducedFromCode (mức bị giảm).", nameof(reducedFromCode));
        }

        Rate = rate;
        Kind = kind;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        ReducedFromCode = reducedFromCode;
        LegalBasis = legalBasis;
    }

    /// <summary>Mức này có hiệu lực tại ngày <paramref name="date"/> hay không.</summary>
    public bool IsEffectiveAt(DateOnly date)
        => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>
    /// Đổi ngày kết thúc hiệu lực — ví dụ luật **gia hạn** mức 8% sau 31/12/2026 (kịch bản Q6).
    ///
    /// 🚨 BR-011: KHÔNG cho đặt hiệu lực lùi về quá khứ. Nếu cho phép, hoá đơn **đã phát hành**
    /// sẽ có cách tính khác với cách hệ thống tính lại ⇒ không giải trình được với cơ quan thuế.
    /// (<c>EffectiveFrom</c> là BẤT BIẾN — muốn đổi phải tạo bản ghi mới.)
    /// </summary>
    public void ChangeEffectiveTo(DateOnly? newEffectiveTo, DateOnly today)
    {
        if (newEffectiveTo is not null)
        {
            if (newEffectiveTo < EffectiveFrom)
            {
                throw new ArgumentException("EffectiveTo phải >= EffectiveFrom.", nameof(newEffectiveTo));
            }

            if (newEffectiveTo < today)
            {
                throw new BusinessException(TaxErrorCodes.BackdatedEffectiveDate)
                    .WithData("effectiveTo", newEffectiveTo.Value.ToString("yyyy-MM-dd"))
                    .WithData("today", today.ToString("yyyy-MM-dd"));
            }
        }

        EffectiveTo = newEffectiveTo;
    }
}
