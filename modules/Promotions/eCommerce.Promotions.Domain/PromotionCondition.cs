using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Promotions;

/// <summary>
/// **Node cây AND/OR** (`PRO-002` §3.3, bảng `ProPromotionConditions`).
/// 📌 **KHÔNG** soft-delete, **KHÔNG** audit (SRS §5.2) — cần cho partial unique một gốc
/// (`IX_ProPromotionConditions_MotGoc`), và `SaveTreeAsync` **xoá cứng** cũ trước thêm mới.
/// </summary>
public class PromotionCondition : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PromotionId { get; private set; }
    public Guid? ParentId { get; private set; }
    public bool IsGroup { get; private set; }
    public PromotionConditionGroupType? InteractionType { get; private set; }
    public string? RuleSystemName { get; private set; }
    public string? ConfigJson { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>Độ sâu **lưu sẵn** (1 = gốc) để `CHECK ("Depth" BETWEEN 1 AND 8)`.</summary>
    public int Depth { get; private set; }

    protected PromotionCondition()
    {
    }

    public static PromotionCondition TaoMoi(
        Guid id,
        Guid? tenantId,
        Guid promotionId,
        Guid? parentId,
        bool isGroup,
        PromotionConditionGroupType? interactionType,
        string? ruleSystemName,
        string? configJson,
        int sortOrder,
        int depth)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            PromotionId = promotionId,
            ParentId = parentId,
            IsGroup = isGroup,
            InteractionType = interactionType,
            RuleSystemName = ruleSystemName,
            ConfigJson = configJson,
            SortOrder = sortOrder,
            Depth = depth
        };
}
