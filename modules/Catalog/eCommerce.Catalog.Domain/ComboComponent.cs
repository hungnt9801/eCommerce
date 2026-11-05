using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **THÀNH PHẦN CỦA COMBO** — sản phẩm loại <see cref="ProductType.Combo"/> (`03 §3.1`).
///
/// 🚨 COMBO **KHÔNG TỰ CÓ TỒN**: bán một combo = xuất tồn của **từng thành phần** theo `Quantity`.
/// Nếu lưu combo như một sản phẩm thường có tồn riêng thì kho sẽ có hai con số cho cùng món hàng
/// (combo và thành phần) và **không bao giờ khớp** — đúng lỗi `03 §3.1` cảnh báo ở mô hình variant cũ.
/// 🔒 `(ComboProductId, ComponentProductId)` duy nhất; `Quantity > 0`.
/// </summary>
public class ComboComponent : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Sản phẩm **combo** (loại <see cref="ProductType.Combo"/>).</summary>
    public Guid ComboProductId { get; protected set; }

    /// <summary>Sản phẩm **thành phần** — phải là sản phẩm bán được (không phải sản phẩm cha).</summary>
    public Guid ComponentProductId { get; protected set; }

    /// <summary>Số lượng thành phần trong một combo (`decimal(18,3)` — combo có thể chứa hàng cân đo).</summary>
    public decimal Quantity { get; protected set; }

    protected ComboComponent() { }

    public ComboComponent(Guid id, Guid comboProductId, Guid componentProductId, decimal quantity) : base(id)
    {
        if (comboProductId == componentProductId)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThanhPhanKhongHopLe)
                .WithData("lyDo", "Combo không thể chứa chính nó.");
        }

        ComboProductId = comboProductId;
        ComponentProductId = componentProductId;
        Quantity = KiemTraSoLuong(quantity);
    }

    /// <summary>Số lượng thành phần phải **lớn hơn 0** — `0` là dòng vô nghĩa, âm là xuất ngược.</summary>
    public static decimal KiemTraSoLuong(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SoLuongKhongHopLe)
                .WithData("quantity", quantity)
                .WithData("lyDo", "Số lượng phải LỚN HƠN 0.");
        }

        return quantity;
    }
}
