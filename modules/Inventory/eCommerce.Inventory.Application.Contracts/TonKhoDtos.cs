using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Inventory;

/// <summary>Một dòng tồn kho để hiển thị (read-only, `INV-001`).</summary>
public class TonKhoDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = default!;
    public string WarehouseName { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
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

    /// <summary>
    /// 🆕 **`WP-04`** — tồn kho **9 chiều** có phân trang (màn #6/#8: cột tồn 3 số + DS phiếu kho).
    /// 📌 Tách khỏi <see cref="GetAsync"/> **có chủ ý**: màn `/Kho` cũ dùng `TonKhoDto` (3 số); đổi nó thành 12 số
    /// sẽ làm hỏng view Razor đang chạy và **không** cần thiết cho màn cũ.
    /// </summary>
    Task<PagedResultDto<StockBalanceDto>> GetBalancesAsync(
        Guid? warehouseId = null, string? sku = null, int skipCount = 0, int maxResultCount = 50);
}
