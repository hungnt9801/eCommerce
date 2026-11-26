using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Promotions;

/// <summary>
/// Slot flash sale (`ProFlashSaleSlots`). **0 dòng ở v1** vì `Kind <> 'FlashSale'` (`D-156`, nợ `CR-P2`).
/// `SalePrice` là **giá bán** (đi vào đơn giá dòng), **không** phải khoản giảm theo dòng.
/// </summary>
public class FlashSaleSlot : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PromotionId { get; private set; }
    public string Sku { get; private set; } = default!;
    public decimal SalePrice { get; private set; }
    public string Currency { get; private set; } = default!;

    /// <summary>Số lượng — `decimal(18,3)` (`D-31`).</summary>
    public decimal QuantityLimit { get; private set; }

    /// <summary>Số lượng đã bán — `decimal(18,3)` (`D-31`).</summary>
    public decimal SoldCount { get; private set; }

    protected FlashSaleSlot()
    {
    }

    public static FlashSaleSlot TaoMoi(Guid id, Guid? tenantId, Guid promotionId, string sku, decimal salePrice, string currency, decimal quantityLimit)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            PromotionId = promotionId,
            Sku = sku,
            SalePrice = salePrice,
            Currency = currency,
            QuantityLimit = quantityLimit,
            SoldCount = 0m
        };
}
