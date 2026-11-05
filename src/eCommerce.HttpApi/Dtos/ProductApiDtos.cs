using eCommerce.Catalog.Dtos;

namespace eCommerce.HttpApi.Dtos;

/// <summary>
/// Trang kết quả **chuẩn của `/api/v1`** — khác `PagedResultDto` của ABP (`Items`/`TotalCount`)
/// để client Next.js có hợp đồng ổn định, không phụ thuộc DTO nội bộ.
/// </summary>
public sealed record ApiPage<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);

/// <summary>Tham số danh sách sản phẩm — **đúng những gì màn hình Nhanh cần** (ô tìm + trạng thái + phân trang).</summary>
public sealed class ProductListQuery
{
    /// <summary>Tìm theo mã / tên / SKU (không phân biệt hoa thường).</summary>
    public string? Q { get; set; }

    /// <summary>`Draft` | `Published` | `Unpublished` | `Archived` (bỏ trống = tất cả).</summary>
    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    /// <summary>Ví dụ `CreationTime desc` · `Name` · `Code`.</summary>
    public string? Sort { get; set; }
}

/// <summary>
/// Một dòng trong bảng sản phẩm (04 §5 màn #4 — 20 cột).
/// ⚠️ Các cột tồn kho (`OnHand`, `Available`…) **CHƯA có** ở WP-02 — sẽ thêm ở WP-04
/// cùng `StockBalance`; **không** trả `0` giả để tránh hiểu nhầm "hết hàng".
/// </summary>
public class ProductListItemDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Slug { get; set; }

    public string Status { get; set; } = default!;

    public bool IsPublished { get; set; }

    /// <summary>SKU của biến thể đầu tiên (tạm thời — WP-03 thay bằng `unit` gốc).</summary>
    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    /// <summary>Giá bán thấp nhất trong các biến thể (VND, `decimal`).</summary>
    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int? WeightInGrams { get; set; }

    public int VariantCount { get; set; }

    // ── WP-03 (02/11/2026) — cột của mô hình Nhanh (`03 §3.1`, D-07) ──────────────────────────
    /// <summary>Tên loại sản phẩm (`Standard`/`Imei`/`Combo`…).</summary>
    public string Type { get; set; } = default!;

    public decimal? VatPercent { get; set; }

    /// <summary>`Included` (giá đã gồm VAT) hoặc `Excluded` (chưa gồm).</summary>
    public string VatMode { get; set; } = default!;

    public int UnitCount { get; set; }

    public DateTime CreationTime { get; set; }
}

public sealed class ProductDetailDto : ProductListItemDto
{
    public string? Description { get; set; }

    public Guid? BrandId { get; set; }

    public Guid? ParentId { get; set; }

    public int? WeightGram { get; set; }

    public int? WarrantyMonths { get; set; }

    public int? StorageTimeDays { get; set; }

    public bool IsHot { get; set; }

    public bool IsNew { get; set; }

    public string? ExternalId { get; set; }

    public List<ProductUnitApiDto> Units { get; set; } = new();

    public List<ProductVariantApiDto> Variants { get; set; } = new();
}

/// <summary>Một đơn vị tính của sản phẩm (WP-03).</summary>
public sealed class ProductUnitApiDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public decimal ConversionFactor { get; set; }

    public string? Barcode { get; set; }

    public bool IsBase { get; set; }

    public decimal SalePrice { get; set; }

    public decimal? ImportPrice { get; set; }

    public decimal? WholesalePrice { get; set; }

    public decimal? OldPrice { get; set; }
}

public sealed class ProductVariantApiDto
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = default!;

    public string? Barcode { get; set; }

    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public int? WeightInGrams { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Kết quả tra cứu cho POS (quét mã vạch / gõ mã) — cố ý **nhỏ** để gọi trong lúc quét.</summary>
public sealed class ProductLookupResultDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public decimal? Price { get; set; }

    public string Status { get; set; } = default!;
}

/// <summary>Map tay — dự án **không dùng AutoMapper** (quy ước #12, đã bỏ vì advisory bảo mật).</summary>
internal static class ProductApiMapper
{
    public static ProductListItemDto ToListItem(ProductDto dto)
    {
        var first = dto.Variants.FirstOrDefault();
        var donViGoc = dto.Units.FirstOrDefault(x => x.IsBase);

        // 💰 Giá hiển thị: ưu tiên ĐVT GỐC (mô hình Nhanh), rơi về biến thể cũ khi sản phẩm chưa có ĐVT —
        // nhờ vậy danh sách vẫn đúng cho cả dữ liệu cũ (chưa backfill) lẫn dữ liệu mới.
        decimal? gia = donViGoc?.SalePrice;

        if (gia is null && dto.Variants.Count > 0)
        {
            gia = dto.Variants.Min(x => x.Price);
        }

        return new ProductListItemDto
        {
            Id = dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            Slug = dto.Slug,
            Status = dto.Status,
            IsPublished = dto.IsPublished,
            Sku = first?.Sku,
            Barcode = first?.Barcode ?? donViGoc?.Barcode,
            Price = gia,
            CompareAtPrice = first?.CompareAtPrice,
            WeightInGrams = first?.WeightInGrams ?? dto.WeightGram,
            VariantCount = dto.Variants.Count,
            Type = dto.Type.ToString(),
            VatPercent = dto.VatPercent,
            VatMode = dto.VatMode.ToString(),
            UnitCount = dto.Units.Count,
            CreationTime = dto.CreationTime
        };
    }

    public static ProductDetailDto ToDetail(ProductDto dto)
    {
        var item = ToListItem(dto);
        return new ProductDetailDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Slug = item.Slug,
            Status = item.Status,
            IsPublished = item.IsPublished,
            Sku = item.Sku,
            Barcode = item.Barcode,
            Price = item.Price,
            CompareAtPrice = item.CompareAtPrice,
            WeightInGrams = item.WeightInGrams,
            VariantCount = item.VariantCount,
            Type = item.Type,
            VatPercent = item.VatPercent,
            VatMode = item.VatMode,
            UnitCount = item.UnitCount,
            CreationTime = item.CreationTime,
            Description = dto.Description,
            BrandId = dto.BrandId,
            ParentId = dto.ParentId,
            WeightGram = dto.WeightGram,
            WarrantyMonths = dto.WarrantyMonths,
            StorageTimeDays = dto.StorageTimeDays,
            IsHot = dto.IsHot,
            IsNew = dto.IsNew,
            ExternalId = dto.ExternalId,
            Units = dto.Units.Select(u => new ProductUnitApiDto
            {
                Id = u.Id,
                Name = u.Name,
                ConversionFactor = u.ConversionFactor,
                Barcode = u.Barcode,
                IsBase = u.IsBase,
                SalePrice = u.SalePrice,
                ImportPrice = u.ImportPrice,
                WholesalePrice = u.WholesalePrice,
                OldPrice = u.OldPrice
            }).ToList(),
            Variants = dto.Variants.Select(v => new ProductVariantApiDto
            {
                Id = v.Id,
                Sku = v.Sku,
                Barcode = v.Barcode,
                Price = v.Price,
                CompareAtPrice = v.CompareAtPrice,
                WeightInGrams = v.WeightInGrams,
                IsActive = v.IsActive
            }).ToList()
        };
    }

    public static ProductLookupResultDto ToLookup(ProductDto dto)
    {
        var first = dto.Variants.FirstOrDefault();
        return new ProductLookupResultDto
        {
            Id = dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            Sku = first?.Sku,
            Barcode = first?.Barcode,
            Price = first?.Price,
            Status = dto.Status
        };
    }
}
