using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Promotions;

/// <summary>
/// **Mã giảm giá** (`CR-2`, bảng `ProVoucherCodes`). Bộ đếm `UsedCount + ReservedCount ≤ MaxUsage` được cưỡng chế
/// bằng **một** `CHECK` ở DB (thay bản cũ — `ADR-09`, `CR-3`); **sổ cái** `ProPromotionRedemptions` mới là nguồn sự thật.
/// </summary>
public class VoucherCode : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PromotionId { get; private set; }
    public string Code { get; private set; } = default!;
    public int MaxUsage { get; private set; }
    public int UsedCount { get; private set; }
    public int ReservedCount { get; private set; }
    public VoucherCodeStatus Status { get; private set; }
    public byte[]? RowVersion { get; private set; }

    protected VoucherCode()
    {
    }

    public static VoucherCode TaoMoi(Guid id, Guid? tenantId, Guid promotionId, string code, int maxUsage, VoucherCodeStatus status = VoucherCodeStatus.Generated)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            PromotionId = promotionId,
            Code = code.Trim(),
            MaxUsage = maxUsage,
            UsedCount = 0,
            ReservedCount = 0,
            Status = status
        };
}
