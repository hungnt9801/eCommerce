using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **Định nghĩa thuộc tính** (`CatAttributes`) — `CAT-002` D4/BR-008.
/// </summary>
public class CatalogAttribute : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã thuộc tính, **duy nhất trong tenant** (unique index `(TenantId, Code)`).</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Quyết định thuộc tính có tham gia **sinh tổ hợp** hay không (BR-008).</summary>
    public CatalogAttributeUsage Usage { get; protected set; }

    /// <summary>
    /// Ngừng dùng. ⚠️ **KHÔNG xoá** khi đã có biến thể dùng (`CAT-002` BR-012 ⇒ `Catalog:0023`) —
    /// xoá sẽ làm `ProductVariantAttribute` trỏ vào hư không và **bộ chọn biến thể của khách hỏng im lặng**.
    /// </summary>
    public bool IsActive { get; protected set; } = true;

    protected CatalogAttribute() { }

    public CatalogAttribute(Guid id, string code, string name, CatalogAttributeUsage usage) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name));
        }

        var ma = code.Trim();

        if (ma.Length > 64)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.MaDanhMucKhongHopLe)
                .WithData("truong", "CatalogAttribute.Code")
                .WithData("doDai", ma.Length);
        }

        Code = ma;
        Name = name.Trim();
        Usage = usage;
    }

    public void DoiTen(string ten)
    {
        if (string.IsNullOrWhiteSpace(ten))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(ten));
        }

        Name = ten.Trim();
    }

    /// <summary>
    /// Đổi `Usage`. 🚨 **NGUY HIỂM**: đổi từ `Variant` sang `Specification` khi đã sinh biến thể sẽ làm
    /// các biến thể hiện có **không còn trục tương ứng** ⇒ lần sinh sau **không** tạo lại chúng và chúng
    /// trở thành "biến thể mồ côi" (vẫn bán được nhưng không xuất hiện trong bộ chọn nào). Vì vậy **CẤM**
    /// đổi khi đã có biến thể dùng thuộc tính này — app service kiểm tra và ném `Catalog:0023`.
    /// </summary>
    public void DoiUsage(CatalogAttributeUsage usage) => Usage = usage;

    public void NgungDung() => IsActive = false;

    public void KichHoat() => IsActive = true;
}

/// <summary>**Giá trị của thuộc tính** (`CatAttributeValues`) — ví dụ "Đỏ", "M".</summary>
public class CatalogAttributeValue : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid AttributeId { get; protected set; }

    /// <summary>Giá trị hiển thị — **duy nhất trong một thuộc tính** (unique `(TenantId, AttributeId, Value)`).</summary>
    public string Value { get; protected set; } = default!;

    /// <summary>Thứ tự hiển thị trong bộ chọn. ⚠️ **KHÔNG** ảnh hưởng `AttributeHash` (BR-016) —
    /// hash chỉ phụ thuộc **tập** giá trị, đã sắp xếp theo `AttributeId`.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; protected set; } = true;

    protected CatalogAttributeValue() { }

    public CatalogAttributeValue(Guid id, Guid attributeId, string value) : base(id)
    {
        if (attributeId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(attributeId));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(value));
        }

        AttributeId = attributeId;
        Value = value.Trim();
    }

    public void DoiGiaTri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(value));
        }

        Value = value.Trim();
    }

    public void NgungDung() => IsActive = false;

    public void KichHoat() => IsActive = true;
}

/// <summary>
/// **Giá trị thuộc tính `Variant` mà người dùng đã CHỌN trên một sản phẩm**
/// (`CatProductAttributeSelections`) — đầu vào của bước sinh tổ hợp.
/// </summary>
public class ProductAttributeSelection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    public Guid AttributeId { get; protected set; }

    public Guid AttributeValueId { get; protected set; }

    public int SortOrder { get; set; }

    protected ProductAttributeSelection() { }

    public ProductAttributeSelection(Guid id, Guid productId, Guid attributeId, Guid attributeValueId) : base(id)
    {
        if (productId == Guid.Empty || attributeId == Guid.Empty || attributeValueId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "ProductId/AttributeId/AttributeValueId");
        }

        ProductId = productId;
        AttributeId = attributeId;
        AttributeValueId = attributeValueId;
    }
}

/// <summary>
/// **Tổ hợp đã chốt của một biến thể** (`CatProductVariantAttributes`) — biến thể này mang giá trị nào
/// (ví dụ biến thể X = Đỏ + M). Dùng để dựng **bộ chọn biến thể** ở storefront.
/// </summary>
public class ProductVariantAttribute : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ProductVariantId { get; protected set; }

    public Guid AttributeId { get; protected set; }

    public Guid AttributeValueId { get; protected set; }

    protected ProductVariantAttribute() { }

    public ProductVariantAttribute(Guid id, Guid productVariantId, Guid attributeId, Guid attributeValueId) : base(id)
    {
        if (productVariantId == Guid.Empty || attributeId == Guid.Empty || attributeValueId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", "ProductVariantId/AttributeId/AttributeValueId");
        }

        ProductVariantId = productVariantId;
        AttributeId = attributeId;
        AttributeValueId = attributeValueId;
    }
}
