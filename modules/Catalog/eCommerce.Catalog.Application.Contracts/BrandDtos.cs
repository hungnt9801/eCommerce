using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Catalog.Dtos;

/// <summary>Thương hiệu trả về cho client (`CatBrands`, 27/10/2026 — `CAT-002` Q3).</summary>
public class BrandDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Lọc danh sách thương hiệu — `Filter` khớp Code/Name/Slug (không phân biệt hoa thường).</summary>
public class GetBrandsInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }

    public bool? IsActive { get; set; }
}

public class CreateBrandDto
{
    [Required]
    [StringLength(Brand.DoDaiCodeToiDa)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(Brand.DoDaiTenToiDa)]
    public string Name { get; set; } = default!;

    /// <summary>Bỏ trống ⇒ tự sinh từ `Name`; 🔒 không sinh được thì **báo lỗi**, không bịa `brand-{guid}`.</summary>
    [StringLength(Brand.DoDaiSlugToiDa)]
    public string? Slug { get; set; }

    [StringLength(Brand.DoDaiMoTaToiDa)]
    public string? Description { get; set; }

    [StringLength(Brand.DoDaiLogoUrlToiDa)]
    public string? LogoUrl { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>⚠️ **Không** có `Code`: mã thương hiệu **bất biến** sau khi tạo (dùng cho import/đối soát).</summary>
public class UpdateBrandDto
{
    [Required]
    [StringLength(Brand.DoDaiTenToiDa)]
    public string Name { get; set; } = default!;

    [StringLength(Brand.DoDaiSlugToiDa)]
    public string? Slug { get; set; }

    [StringLength(Brand.DoDaiMoTaToiDa)]
    public string? Description { get; set; }

    [StringLength(Brand.DoDaiLogoUrlToiDa)]
    public string? LogoUrl { get; set; }

    public int SortOrder { get; set; }
}
