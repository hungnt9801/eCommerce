using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Pricing.Dtos;

public class PriceListDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Tên phạm vi (`All`/`Channel`/`ChannelShop`) — **chuỗi** để client không phụ thuộc số enum.</summary>
    public string Scope { get; set; } = default!;

    public string? ChannelCode { get; set; }

    public Guid? ChannelShopId { get; set; }

    public string CurrencyCode { get; set; } = default!;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public int Priority { get; set; }

    /// <summary>Tên trạng thái (`Draft`/`Approved`/`Active`/…).</summary>
    public string Status { get; set; } = default!;

    /// <summary>Số dòng giá **đếm từ DB** (không phải thuộc tính trên entity — xem ghi chú ở `PriceListAppService`).</summary>
    public int ItemCount { get; set; }
}

public class PriceListItemDto
{
    public Guid Id { get; set; }

    public Guid PriceListId { get; set; }

    public string Sku { get; set; } = default!;

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = default!;
}

public class ChannelPriceDto
{
    public Guid Id { get; set; }

    public string ChannelCode { get; set; } = default!;

    public Guid? ChannelShopId { get; set; }

    public string Sku { get; set; } = default!;

    public int MinQuantity { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = default!;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public int Priority { get; set; }
}

public class CreatePriceListDto
{
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    public PriceListScope Scope { get; set; } = PriceListScope.All;

    /// <summary>Mã kênh — **bắt buộc** khi `Scope != All`.</summary>
    [StringLength(64)]
    public string? ChannelCode { get; set; }

    public Guid? ChannelShopId { get; set; }

    /// <summary>Mã ISO (`VND`, `USD`). Mặc định `VND`.</summary>
    [StringLength(8)]
    public string CurrencyCode { get; set; } = "VND";

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public int Priority { get; set; }
}

public class AddPriceListItemDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    /// <summary>⚠️ `decimal` **đi kèm** `CurrencyCode` của bảng giá cha (BR-001: **cấm** tiền trần không có tiền tệ).</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
    public decimal Amount { get; set; }
}

/// <summary>Đầu vào **tra giá** (`PRC-001` §6.1) — dùng bởi `SAL-002` khi dựng giỏ/checkout.</summary>
public class ResolvePriceInput
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    /// <summary>Mã kênh bán — **bắt buộc** (BR-003: **cấm** truy vấn giá không kèm ngữ cảnh).</summary>
    [Required]
    [StringLength(64)]
    public string ChannelCode { get; set; } = default!;

    /// <summary>Shop cụ thể (chỉ có khi giá áp cho một shop — `PRC-001` D7 cấp 1).</summary>
    public Guid? ChannelShopId { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>Thời điểm tra giá — ⚠️ **bắt buộc** trên thực tế; để trống ⇒ lấy giờ hệ thống **ở tầng app service**
    /// (hàm thuần **cấm** đọc đồng hồ — `PRC-002` ADR-04).</summary>
    public DateTime? At { get; set; }
}

/// <summary>Kết quả tra giá — **kèm `Cap` + `Nguon`** để luôn trả lời được *"vì sao giá này"* (BR-009/`PRC-002` ADR-03).</summary>
public class ResolvedPriceDto
{
    public string Sku { get; set; } = default!;

    public string ChannelCode { get; set; } = default!;

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = default!;

    /// <summary>Cấp thắng (`GiaTheoShop`/`GiaTheoKenh`/`BangGia`/`GiaNiemYet`).</summary>
    public string WinningLevel { get; set; } = default!;

    /// <summary>Nguồn cụ thể (ví dụ `PriceList:BG-1#SKU-1`).</summary>
    public string Source { get; set; } = default!;

    /// <summary>Tất cả ứng viên đã xét — để kiểm chứng **không áp mù** (`PRC-002` ADR-03).</summary>
    public List<ResolvedPriceCandidateDto> AllCandidates { get; set; } = new();
}

public class ResolvedPriceCandidateDto
{
    public string Level { get; set; } = default!;

    public decimal Amount { get; set; }

    public int Priority { get; set; }

    public string Source { get; set; } = default!;
}

/// <summary>Tra giá **hàng loạt** (giỏ hàng nhiều dòng) — tránh N+1 ở tầng gọi.</summary>
public class ResolveManyInput
{
    [Required]
    [MinLength(1)]
    public List<ResolvePriceInput> Items { get; set; } = new();
}
