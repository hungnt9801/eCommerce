using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Sales.Dtos;

/// <summary>🆕 `S14` — một **phiếu trả hàng** (`SAL-006`).</summary>
public class OrderReturnDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string ReturnNumber { get; set; } = default!;
    public string ChannelCode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string GoodsStatus { get; set; } = default!;
    public string ReasonCode { get; set; } = default!;
    public string? ReasonText { get; set; }
    public Guid WarehouseId { get; set; }
    public string RefundMethod { get; set; } = default!;
    public decimal RefundedAmount { get; set; }
    public string RefundCurrency { get; set; } = default!;
    public long Version { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public List<OrderReturnLineDto> Lines { get; set; } = new();
}

public class OrderReturnLineDto
{
    public Guid Id { get; set; }
    public Guid OrderLineId { get; set; }
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public string Disposition { get; set; } = default!;
    public decimal RefundAmount { get; set; }
}

public class CreateOrderReturnInput
{
    public Guid OrderId { get; set; }

    /// <summary>Kho **nhận hàng hoàn** (bắt buộc).</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Mã lý do **ĐÓNG** (`SAL-006` BR-014) — không nhận văn bản tự do.</summary>
    [Required]
    [StringLength(64)]
    public string ReasonCode { get; set; } = default!;

    [StringLength(512)]
    public string? ReasonText { get; set; }

    /// <summary>🔒 Khoá chống trùng của yêu cầu tạo phiếu (`XC-002` BR-003) — **bắt buộc**.</summary>
    [Required]
    [StringLength(256)]
    public string IdempotencyKey { get; set; } = default!;

    [Required]
    [MinLength(1)]
    public List<CreateOrderReturnLineInput> Lines { get; set; } = new();
}

public class CreateOrderReturnLineInput
{
    public Guid OrderLineId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng trả phải ≥ 1.")]
    public int Quantity { get; set; }
}

public class ReceiveOrderReturnInput
{
    [Required]
    [MinLength(1)]
    public List<ReceiveOrderReturnLineInput> Lines { get; set; } = new();
}

public class ReceiveOrderReturnLineInput
{
    public Guid ReturnLineId { get; set; }

    [Range(0, int.MaxValue)]
    public int ReceivedQuantity { get; set; }

    /// <summary>`Restock` · `Damaged` · `ReturnToSupplier` — **bắt buộc** khi đã nhận hàng.</summary>
    public string Disposition { get; set; } = default!;
}

/// <summary>Lý do cho thao tác từ chối/huỷ phiếu — **bắt buộc** (`SAL-006` BR-008).</summary>
public class LyDoTraHangInput
{
    [Required]
    [StringLength(512, MinimumLength = 3)]
    public string LyDo { get; set; } = default!;
}

/// <summary>Ghi nhận **đã hoàn tiền** (thủ công) — `Q4`: chỉ ghi nhận, không gọi cổng.</summary>
public class GhiNhanHoanTienInput
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền hoàn phải lớn hơn 0.")]
    public decimal SoTien { get; set; }
}
