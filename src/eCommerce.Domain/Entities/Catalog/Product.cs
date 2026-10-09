using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// Sản phẩm (aggregate root). Giá nằm ở <see cref="ProductVariant"/> vì cùng một sản phẩm
/// có nhiều biến thể với giá và tồn kho riêng.
/// </summary>
public class Product : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã nội bộ, duy nhất trong tenant.</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Slug { get; set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }

    public bool IsPublished { get; set; }

    private readonly List<ProductVariant> _variants = new();

    /// <summary>
    /// Biến thể — mỗi biến thể có SKU riêng và là đơn vị mà sàn TMĐT tham chiếu.
    /// </summary>
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    protected Product() { }

    public Product(Guid id, string code, string name) : base(id)
    {
        Code = code;
        Name = name;
    }

    public ProductVariant AddVariant(Guid id, string sku, decimal price, string? barcode = null)
    {
        if (_variants.Any(x => x.Sku == sku))
        {
            throw new ArgumentException($"SKU '{sku}' đã tồn tại trong sản phẩm '{Code}'.", nameof(sku));
        }

        var variant = new ProductVariant(id, sku, price, barcode);
        _variants.Add(variant);
        return variant;
    }
}

/// <summary>Biến thể sản phẩm — đơn vị bán thật, gắn với SKU và giá.</summary>
public class ProductVariant : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    /// <summary>SKU nội bộ; dùng làm khoá đối chiếu với <c>ChannelListing.ExternalSku</c>.</summary>
    public string Sku { get; set; } = default!;

    public string? Barcode { get; set; }

    /// <summary>
    /// Giá niêm yết (nguồn sự thật).
    /// ⚠️ <c>decimal</c>, không phải <c>double</c> — xem quy tắc trong <c>eCommerceDomainModule</c>.
    /// </summary>
    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    /// <summary>Khối lượng (gram) để tính phí vận chuyển.</summary>
    public int? WeightInGrams { get; set; }

    public bool IsActive { get; set; } = true;

    protected ProductVariant() { }

    public ProductVariant(Guid id, string sku, decimal price, string? barcode = null) : base(id)
    {
        Sku = sku;
        Price = price;
        Barcode = barcode;
    }
}
