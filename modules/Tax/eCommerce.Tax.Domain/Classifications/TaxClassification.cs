using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tax.Classifications;

/// <summary>
/// Phân loại thuế cho một SKU hoặc một danh mục — **đây là chỗ KẾ TOÁN quyết định**
/// hàng hoá nào thuộc/không thuộc Phụ lục I/II của NĐ 174/2025.
///
/// 🎯 VÌ SAO KHÔNG HARD-CODE DANH MỤC LUẬT:
/// danh mục Phụ lục I/II phân loại theo mã ngành/HS, thay đổi theo văn bản pháp luật.
/// Nếu nhúng vào code thì MỖI LẦN LUẬT ĐỔI phải release lại phần mềm. Vì vậy danh mục là DỮ LIỆU:
/// kế toán import Excel (7 cột, xem §6.4 tài liệu thiết kế) và duyệt.
///
/// 🚨 BR-XC-006-005: bản ghi là **append-only** — bản ghi đã <see cref="TaxClassificationStatus.Active"/>
///    KHÔNG được sửa/xoá; muốn đổi phải tạo bản ghi mới (để giải trình với cơ quan thuế).
/// </summary>
public class TaxClassification : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public TaxClassificationTargetType TargetType { get; protected set; }

    /// <summary><c>ProductVariantId</c> khi <see cref="TargetType"/> = Sku; <c>CategoryId</c> khi = Category.</summary>
    public Guid TargetId { get; protected set; }

    /// <summary>Mã mức cơ sở, tham chiếu <c>TaxRateSchedule.Code</c> — ví dụ <c>VAT10</c>.</summary>
    public string BaseRateCode { get; protected set; } = default!;

    /// <summary>
    /// <c>true</c> = **KHÔNG** thuộc Phụ lục I/II ⇒ **được giảm** (10% → 8% khi còn khung hiệu lực).
    /// <c>false</c> = thuộc Phụ lục I/II ⇒ **không được giảm**.
    ///
    /// ⚠️ Giá trị này là **quyết định của kế toán**, không phải hệ thống tự suy ra.
    /// </summary>
    public bool ReductionApplicable { get; protected set; }

    public DateOnly EffectiveFrom { get; protected set; }

    public DateOnly? EffectiveTo { get; protected set; }

    /// <summary>Căn cứ: số công văn, mã ngành/HS… — phục vụ **giải trình thuế** (Q5 đã chốt: chưa cần tệp đính kèm).</summary>
    public string? LegalBasisNote { get; protected set; }

    public TaxClassificationStatus Status { get; protected set; } = TaxClassificationStatus.Draft;

    public Guid? ApprovedBy { get; protected set; }

    public DateTime? ApprovedAtUtc { get; protected set; }

    /// <summary>Phiên bản bản ghi — tăng khi tạo bản ghi thay thế.</summary>
    public int Version { get; protected set; } = 1;

    protected TaxClassification()
    {
    }

    public TaxClassification(
        Guid id,
        TaxClassificationTargetType targetType,
        Guid targetId,
        string baseRateCode,
        bool reductionApplicable,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        string? legalBasisNote = null) : base(id)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("TargetId không được rỗng.", nameof(targetId));
        }

        BaseRateCode = Check.NotNullOrWhiteSpace(baseRateCode, nameof(baseRateCode), maxLength: 32);
        TargetType = targetType;
        TargetId = targetId;
        ReductionApplicable = reductionApplicable;
        EffectiveFrom = effectiveFrom;

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo phải >= EffectiveFrom.", nameof(effectiveTo));
        }

        EffectiveTo = effectiveTo;
        LegalBasisNote = legalBasisNote;
    }

    /// <summary>
    /// Bản ghi này có đang ảnh hưởng tới việc tính thuế tại ngày <paramref name="date"/> hay không.
    /// 🚨 CHỈ <c>Approved</c> và <c>Active</c> mới ảnh hưởng (BR-007) — <c>Draft</c> thì KHÔNG.
    /// </summary>
    public bool IsUsableAt(DateOnly date)
        => Status is TaxClassificationStatus.Approved or TaxClassificationStatus.Active
           && date >= EffectiveFrom
           && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>Sửa khi còn nháp. Bản ghi đã Active thì KHÔNG sửa được (append-only).</summary>
    public void UpdateDraft(
        string baseRateCode,
        bool reductionApplicable,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string? legalBasisNote)
    {
        EnsureDraft(TaxErrorCodes.CannotModifyActive);

        BaseRateCode = Check.NotNullOrWhiteSpace(baseRateCode, nameof(baseRateCode), maxLength: 32);
        ReductionApplicable = reductionApplicable;
        EffectiveFrom = effectiveFrom;

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo phải >= EffectiveFrom.", nameof(effectiveTo));
        }

        EffectiveTo = effectiveTo;
        LegalBasisNote = legalBasisNote;
    }

    /// <summary>Draft → Approved. Chỉ kế toán trưởng có quyền gọi (kiểm ở tầng app service).</summary>
    public void Approve(Guid approverId, DateTime approvedAtUtc)
    {
        if (Status != TaxClassificationStatus.Draft)
        {
            throw new BusinessException(TaxErrorCodes.InvalidStatusTransition)
                .WithData("currentStatus", Status.ToString())
                .WithData("expectedStatus", nameof(TaxClassificationStatus.Draft));
        }

        Status = TaxClassificationStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAtUtc = approvedAtUtc;
    }

    /// <summary>Approved → Active khi đã tới ngày hiệu lực. Chưa tới thì giữ nguyên (KHÔNG ném lỗi).</summary>
    public bool ActivateIfDue(DateOnly today)
    {
        if (Status != TaxClassificationStatus.Approved || today < EffectiveFrom)
        {
            return false;
        }

        Status = TaxClassificationStatus.Active;
        return true;
    }

    /// <summary>Active → Superseded. Bản ghi mới phải đã được duyệt (BR-005).</summary>
    public void Supersede(DateOnly endDate, bool replacementIsApproved)
    {
        if (Status != TaxClassificationStatus.Active)
        {
            throw new BusinessException(TaxErrorCodes.InvalidStatusTransition)
                .WithData("currentStatus", Status.ToString());
        }

        if (!replacementIsApproved)
        {
            throw new BusinessException(TaxErrorCodes.ReplacementNotApproved);
        }

        if (endDate < EffectiveFrom)
        {
            throw new ArgumentException("Ngày kết thúc phải >= EffectiveFrom.", nameof(endDate));
        }

        EffectiveTo = endDate;
        Status = TaxClassificationStatus.Superseded;
    }

    /// <summary>Active → Expired khi đã quá ngày kết thúc.</summary>
    public bool ExpireIfDue(DateOnly today)
    {
        if (Status != TaxClassificationStatus.Active || EffectiveTo is null || today <= EffectiveTo)
        {
            return false;
        }

        Status = TaxClassificationStatus.Expired;
        return true;
    }

    /// <summary>Huỷ — CHỈ khi còn Draft (bản ghi đã duyệt/đã hiệu lực thì không huỷ được).</summary>
    public void Cancel()
    {
        EnsureDraft(TaxErrorCodes.CannotModifyActive);
        Status = TaxClassificationStatus.Cancelled;
    }

    public void IncreaseVersion() => Version++;

    private void EnsureDraft(string errorCode)
    {
        if (Status != TaxClassificationStatus.Draft)
        {
            throw new BusinessException(errorCode)
                .WithData("currentStatus", Status.ToString());
        }
    }
}
