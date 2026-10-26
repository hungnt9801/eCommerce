using System.ComponentModel.DataAnnotations;
using eCommerce.Inventory;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Inventory;

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

public class CreateWarehouseDto
{
    [Required]
    [StringLength(32)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    [StringLength(512)]
    public string? Address { get; set; }
}

/// <summary>Một cặp `(kho, SKU)` **LỆCH** giữa ảnh chụp `OnHand` và sổ cái (`INV-001` ADR-10).</summary>
public class LechTonKhoDto
{
    public Guid WarehouseId { get; set; }
    public string Sku { get; set; } = default!;
    public int OnHand { get; set; }
    public int TongSoCai { get; set; }

    /// <summary>`OnHand − TổngSổCái`. Dương ⇒ ảnh chụp **thừa**; âm ⇒ ảnh chụp **thiếu**.</summary>
    public int ChenhLech { get; set; }
}

public class KetQuaDoiSoatDto
{
    /// <summary>Số cặp `(kho, SKU)` đã kiểm.</summary>
    public int SoCapDaKiem { get; set; }

    /// <summary>Số cặp **LỆCH** — `0` nghĩa là **khớp hoàn toàn**.</summary>
    public int SoCapLech { get; set; }

    public List<LechTonKhoDto> CacLech { get; set; } = new();
}

/// <summary>**API KHO** (`INV-001` ADR-01) — tạo/liệt kê kho. 🔐 Quyền gắn ở lớp hiện thực.</summary>
public interface IWarehouseAppService : IApplicationService
{
    Task<List<WarehouseDto>> GetListAsync(bool? chiHoatDong = null);

    /// <summary>Tạo kho. **NÉM** nếu mã kho đã tồn tại trong tenant (hàng rào DB là unique index `(TenantId, Code)`).</summary>
    Task<WarehouseDto> CreateAsync(CreateWarehouseDto input);

    Task<WarehouseDto> NgungSuDungAsync(Guid id);
}

/// <summary>
/// **API ĐỐI SOÁT KHO** (`INV-001` **ADR-10**).
/// 📌 Mục đích: biến việc *"phát hiện lệch tồn kho bởi khách/kế toán"* thành *"phát hiện bởi HỆ THỐNG"*.
/// </summary>
public interface IInventoryReconciliationAppService : IApplicationService
{
    Task<KetQuaDoiSoatDto> GetDoiSoatAsync();
}
