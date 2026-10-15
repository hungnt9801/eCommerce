using System.ComponentModel.DataAnnotations;

namespace eCommerce.Pricing.Dtos;

/// <summary>Đầu vào **tính tiền cho cả giỏ** (`PRC-002` §4.2).</summary>
public class TinhGioInput
{
    [Required]
    [MinLength(1)]
    public List<DongGioInput> Items { get; set; } = new();

    /// <summary>Mã kênh bán — **bắt buộc** (BR-003: cấm truy vấn giá không kèm ngữ cảnh).</summary>
    [Required]
    [StringLength(64)]
    public string ChannelCode { get; set; } = default!;

    public Guid? ChannelShopId { get; set; }

    /// <summary>Giảm giá **cấp đơn** (phân bổ xuống từng dòng — R2).</summary>
    [Range(0, double.MaxValue)]
    public decimal OrderDiscount { get; set; }

    /// <summary>Phí **cấp đơn** (phân bổ xuống từng dòng — R3).</summary>
    [Range(0, double.MaxValue)]
    public decimal OrderFee { get; set; }

    /// <summary>Thời điểm tính — để trống ⇒ lấy giờ hệ thống **ở tầng app** (hàm thuần cấm đọc đồng hồ).</summary>
    public DateTime? At { get; set; }
}

public class DongGioInput
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    [Range(1, int.MaxValue, ErrorMessage = "`quantity` phải ≥ 1.")]
    public int Quantity { get; set; } = 1;

    /// <summary>Thuế suất (0,10 = 10%) — **do bên gọi truyền vào** (Pricing không đọc module `Tax`).</summary>
    [Range(0, 1)]
    public decimal TaxRate { get; set; }

    /// <summary>Giá niêm yết (cấp 4) — do bên gọi truyền (Pricing không đọc Catalog).</summary>
    [Range(0, double.MaxValue)]
    public decimal? ListPrice { get; set; }

    [StringLength(8)]
    public string? ListPriceCurrency { get; set; }
}

/// <summary>Chi tiết **từng dòng** — đủ để **giải trình** *"vì sao ra số này"* (`PRC-002` ADR-03/ADR-08).</summary>
public class ChiTietDongDto
{
    public string Sku { get; set; } = default!;

    public int Quantity { get; set; }

    /// <summary>Cấp thắng của **giá cơ sở** (B2) — `GiaTheoShop`/`GiaTheoKenh`/`BangGia`/`GiaNiemYet`.</summary>
    public string BaseLevel { get; set; } = default!;

    public string BaseSource { get; set; } = default!;

    /// <summary>Đơn giá **trước** làm tròn (dùng để kiểm tra R1).</summary>
    public decimal UnitPriceBeforeRounding { get; set; }

    /// <summary>Cấp thắng **cuối cùng** — `BacThang` nếu bậc thang được áp (B5).</summary>
    public string WinningLevel { get; set; } = default!;

    public string WinningSource { get; set; } = default!;

    /// <summary>Bậc thang có được áp cho dòng này không.</summary>
    public bool TierApplied { get; set; }

    /// <summary>Đơn giá **sau R1**.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineSubtotal { get; set; }

    public decimal AllocatedDiscount { get; set; }

    public decimal AllocatedFee { get; set; }

    public decimal Tax { get; set; }

    /// <summary>Thành tiền cuối của dòng = `LineSubtotal − AllocatedDiscount + AllocatedFee + Tax`.</summary>
    public decimal LineTotal { get; set; }

    public List<ResolvedPriceCandidateDto> AllCandidates { get; set; } = new();
}

/// <summary>Kết quả tính tiền cho **cả giỏ**.</summary>
public class GioTinhTienDto
{
    public string CurrencyCode { get; set; } = default!;

    /// <summary>Mã **chính sách làm tròn** đã dùng — để giải trình (ADR-08).</summary>
    public string RoundingPolicyCode { get; set; } = default!;

    public decimal Subtotal { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TotalFee { get; set; }

    public decimal TotalTax { get; set; }

    /// <summary>🚨 **Tổng = Σ dòng ĐÃ làm tròn** — KHÔNG làm tròn tổng (ADR-06).</summary>
    public decimal Total { get; set; }

    public List<ChiTietDongDto> Lines { get; set; } = new();
}
