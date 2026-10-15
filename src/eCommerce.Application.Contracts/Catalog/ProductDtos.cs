using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Catalog.Dtos;

/// <summary>DTO sản phẩm (`CAT-001` §6.1). **Map tay** — dự án không dùng AutoMapper (quy ước #12).</summary>
public class ProductDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Slug { get; set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }

    /// <summary>Tên trạng thái (`Draft`/`Published`/`Unpublished`/`Archived`) — trả **chuỗi** để client không phụ thuộc số enum.</summary>
    public string Status { get; set; } = default!;

    public bool IsPublished { get; set; }

    public List<ProductVariantDto> Variants { get; set; } = new();

    public DateTime CreationTime { get; set; }
}

public class ProductVariantDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Sku { get; set; } = default!;

    public string? Barcode { get; set; }

    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int? WeightInGrams { get; set; }

    public bool IsActive { get; set; }
}

public class GetProductsInput : PagedAndSortedResultRequestDto
{
    /// <summary>Tìm theo **mã hoặc tên** sản phẩm (không phân biệt hoa/thường).</summary>
    public string? Keyword { get; set; }

    /// <summary>Lọc theo trạng thái (`null` = tất cả).</summary>
    public ProductStatus? Status { get; set; }
}

public class CreateProductDto
{
    [Required]
    [StringLength(eCommerce.eCommerceConsts.CatalogLimits.MaToiDa)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(512)]
    public string Name { get; set; } = default!;

    [StringLength(512)]
    public string? Slug { get; set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }
}

/// <summary>
/// Cập nhật sản phẩm. 🚨 **KHÔNG có `Code`** — mã sản phẩm bất biến sau khi publish
/// (`CAT-001` **D2**); đổi mã phải qua đường riêng và chỉ khi còn `Draft`.
/// </summary>
public class UpdateProductDto
{
    [Required]
    [StringLength(512)]
    public string Name { get; set; } = default!;

    [StringLength(512)]
    public string? Slug { get; set; }

    public string? Description { get; set; }

    public Guid? BrandId { get; set; }
}

public class CreateVariantDto
{
    [Required]
    [StringLength(eCommerce.eCommerceConsts.CatalogLimits.SkuToiDa)]
    public string Sku { get; set; } = default!;

    /// <summary>Giá niêm yết — `decimal` (`numeric(18,4)`), **cấm** `double`.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    [StringLength(64)]
    public string? Barcode { get; set; }

    public int? WeightInGrams { get; set; }
}

public class UpdateVariantPriceDto
{
    [Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
    public decimal Price { get; set; }
}
