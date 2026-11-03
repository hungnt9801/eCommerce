using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **THÀNH PHẦN CỦA GÓI SẢN PHẨM** — sản phẩm loại <see cref="ProductType.Package"/> (`03 §3.1`).
///
/// 📌 Khác <see cref="ComboComponent"/> ở **cách bán**: combo là "mua theo bộ, giá theo bộ"; gói sản phẩm là
/// "đóng gói sẵn nhiều món, bán như một SKU" — nhưng **cùng bất biến**: gói không tự có tồn, xuất tồn theo
/// thành phần.
/// 🔒 `(PackageProductId, ComponentProductId)` duy nhất; `Quantity > 0`.
/// </summary>
public class PackageComponent : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Sản phẩm **gói** (loại <see cref="ProductType.Package"/>).</summary>
    public Guid PackageProductId { get; protected set; }

    /// <summary>Sản phẩm **thành phần** trong gói.</summary>
    public Guid ComponentProductId { get; protected set; }

    /// <summary>Số lượng thành phần trong một gói (`decimal(18,3)`).</summary>
    public decimal Quantity { get; protected set; }

    protected PackageComponent() { }

    public PackageComponent(Guid id, Guid packageProductId, Guid componentProductId, decimal quantity) : base(id)
    {
        if (packageProductId == componentProductId)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThanhPhanKhongHopLe)
                .WithData("lyDo", "Gói không thể chứa chính nó.");
        }

        PackageProductId = packageProductId;
        ComponentProductId = componentProductId;
        Quantity = ComboComponent.KiemTraSoLuong(quantity);
    }
}
