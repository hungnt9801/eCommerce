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

    // ── WP-03 (02/11/2026) — trường của mô hình Nhanh (`03 §3.1`, D-07) ────────────────────────────
    public ProductType Type { get; set; }

    public Guid? ParentId { get; set; }

    public decimal? VatPercent { get; set; }

    public VatMode VatMode { get; set; }

    public int? WeightGram { get; set; }

    public int? WarrantyMonths { get; set; }

    public int? StorageTimeDays { get; set; }

    public bool IsHot { get; set; }

    public bool IsNew { get; set; }

    public string? ExternalId { get; set; }

    public List<ProductUnitDto> Units { get; set; } = new();

    public List<ProductVariantDto> Variants { get; set; } = new();

    public DateTime CreationTime { get; set; }
}

/// <summary>Một **đơn vị tính** của sản phẩm (`03 §3.1`).</summary>
public class ProductUnitDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>Hệ số quy đổi về ĐVT gốc (`1` = gốc, `24` = một thùng 24 lon).</summary>
    public decimal ConversionFactor { get; set; }

    public string? Barcode { get; set; }

    public bool IsBase { get; set; }

    public decimal SalePrice { get; set; }

    public decimal? ImportPrice { get; set; }

    public decimal? WholesalePrice { get; set; }

    public decimal? OldPrice { get; set; }
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

    // ── WP-03: các trường mô hình Nhanh khi tạo sản phẩm ─────────────────────────────────────────

    /// <summary>Loại sản phẩm — mặc định `Standard` nếu bỏ trống.</summary>
    public ProductType Type { get; set; } = ProductType.Standard;

    /// <summary>Sản phẩm cha (cha–con của Nhanh); `null` = độc lập.</summary>
    public Guid? ParentId { get; set; }

    public decimal? VatPercent { get; set; }

    public VatMode VatMode { get; set; } = VatMode.Excluded;

    public int? WeightGram { get; set; }

    public int? WarrantyMonths { get; set; }

    public int? StorageTimeDays { get; set; }

    public bool IsHot { get; set; }

    public bool IsNew { get; set; }

    [StringLength(64)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Các ĐVT tạo kèm sản phẩm (1..n). 🔒 ĐVT **đầu tiên** tự thành đơn vị gốc nếu không đánh dấu `IsBase`.
    /// 📌 Thêm ĐVT sau khi tạo thuộc nửa sau WP-03 — ở đây chỉ có đường tạo kèm (đủ cho màn form sản phẩm).
    /// </summary>
    public List<CreateProductUnitDto> Units { get; set; } = new();
}

/// <summary>
/// Một ĐVT gửi lên khi tạo sản phẩm. ⚠️ `ConversionFactor`/`SalePrice` kiểm ở **domain** (không chỉ
/// DataAnnotations): ràng buộc nghiệp vụ phải nằm một chỗ, và DataAnnotations không chặn được caller nội bộ.
/// </summary>
public class CreateProductUnitDto
{
    [Required]
    [StringLength(eCommerce.eCommerceConsts.CatalogLimits.DonViTinhTenToiDa)]
    public string Name { get; set; } = default!;

    /// <summary>Hệ số quy đổi về ĐVT gốc — phải **> 0**.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    [StringLength(eCommerce.eCommerceConsts.CatalogLimits.BarcodeToiDa)]
    public string? Barcode { get; set; }

    /// <summary>Đánh dấu ĐVT gốc (chỉ **một** ĐVT được đánh dấu).</summary>
    public bool IsBase { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá bán không được âm.")]
    public decimal SalePrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá nhập không được âm.")]
    public decimal? ImportPrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá buôn không được âm.")]
    public decimal? WholesalePrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá cũ không được âm.")]
    public decimal? OldPrice { get; set; }
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

    /// <summary>
    /// 🆕 **WP-03 nửa sau** — sản phẩm cha (cha–con của Nhanh). `null` = **bỏ cha** (sản phẩm thành độc lập).
    ///
    /// 📌 **VÌ SAO PHẢI CÓ Ở ĐÂY:** luật *"chống chu trình cha–con nhiều mức"* (mã `0048`) chỉ có nghĩa khi
    /// **đổi cha của sản phẩm đã tồn tại** — lúc tạo mới, sản phẩm chưa thể có con nên không thể tạo vòng.
    /// Không có trường này thì guard chu trình **không có chỗ gọi** (code chết).
    /// ⚠️ `Guid?` **không phân biệt** *"không gửi"* với *"gửi null"* ⇒ hợp đồng đã chốt: **null = bỏ cha**.
    /// </summary>
    public Guid? ParentId { get; set; }
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
