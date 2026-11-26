using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Promotions;

/// <summary>
/// Ánh xạ phạm vi áp dụng (`ProPromotionMappings`). v1 **chỉ** `Channel`/`Shipping`
/// (`CHECK ("TargetType" NOT IN ('Sku','Category','Brand'))` — `D-152`, nợ `CR-P1`).
/// </summary>
public class PromotionMapping : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PromotionId { get; private set; }
    public string TargetType { get; private set; } = default!;
    public Guid TargetId { get; private set; }
    public string Mode { get; private set; } = default!;

    protected PromotionMapping()
    {
    }

    public static PromotionMapping TaoMoi(Guid id, Guid? tenantId, Guid promotionId, string targetType, Guid targetId, string mode)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            PromotionId = promotionId,
            TargetType = targetType,
            TargetId = targetId,
            Mode = mode
        };
}
