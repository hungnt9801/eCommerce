using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Inventory;

/// <summary>Một dòng tồn kho để hiển thị (read-only, `INV-001`).</summary>
public class TonKhoDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = default!;
    public string WarehouseName { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
}

/// <summary>
/// 🆕 **ĐỌC TỒN KHO CHO UI** (28/10/2026) — màn hình `/Kho`, read-only.
/// 🔐 `Inventory.Default` (cùng quyền với danh mục kho).
/// 📌 Không tái dùng `IWarehouseAppService` vì nó chỉ trả **danh mục kho**; màn hình cần **số tồn theo SKU**.
/// </summary>
public interface ITonKhoAppService : IApplicationService
{
    /// <summary>Danh sách tồn theo kho/SKU; lọc rỗng = tất cả. Sắp xếp ổn định theo (mã kho, SKU).</summary>
    Task<List<TonKhoDto>> GetAsync(Guid? warehouseId = null, string? sku = null);
}
