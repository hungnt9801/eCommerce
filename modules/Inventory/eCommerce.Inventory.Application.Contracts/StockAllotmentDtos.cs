using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Inventory.Dtos;

/// <summary>🆕 `S13` — một dòng **ngân sách tồn theo kênh** (`INV-003`).</summary>
public class StockAllotmentDto
{
    public Guid Id { get; set; }

    public Guid WarehouseId { get; set; }

    public string Sku { get; set; } = default!;

    public string ChannelCode { get; set; } = default!;

    public int AllottedQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    /// <summary>`Allotted − Reserved` khi `Active`; **0** khi `Frozen`/`Released` (`BR-18`).</summary>
    public int AvailableToPromise { get; set; }

    public int Priority { get; set; }

    /// <summary>Tên trạng thái (`Active`/`Frozen`/`Released`) — chuỗi để client không phụ thuộc số enum.</summary>
    public string Status { get; set; } = default!;

    public DateTime? FrozenAtUtc { get; set; }

    public string? ReleaseReason { get; set; }
}

public class GetStockAllotmentsInput
{
    public Guid? WarehouseId { get; set; }

    [StringLength(64)]
    public string? Sku { get; set; }

    [StringLength(32)]
    public string? ChannelCode { get; set; }
}

public class DatNganSachInput
{
    public Guid WarehouseId { get; set; }

    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    /// <summary>Mã kênh — **bắt buộc** (ngân sách luôn gắn một kênh).</summary>
    [Required]
    [StringLength(32)]
    public string ChannelCode { get; set; } = default!;

    /// <summary>Số lượng ngân sách **sau khi đặt** (0 = chưa cấp). 🚨 Tổng các kênh không được vượt `OnHand`.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "Ngân sách không được âm.")]
    public int SoLuong { get; set; }

    /// <summary>🔒 Khoá idempotency của biên vào (`XC-002` BR-005) — gọi lại **cùng khoá** không ghi thêm dòng sổ.</summary>
    [StringLength(256)]
    public string? IdempotencyKey { get; set; }

    [StringLength(64)]
    public string? ReferenceType { get; set; }

    [StringLength(128)]
    public string? ReferenceId { get; set; }
}

/// <summary>Lý do cho thao tác **đóng băng / mở / nhả** — **bắt buộc** (`INV-003` BR-12).</summary>
public class LyDoNganSachInput
{
    [Required]
    [StringLength(512, MinimumLength = 3, ErrorMessage = "Lý do phải từ 3 ký tự.")]
    public string LyDo { get; set; } = default!;
}
