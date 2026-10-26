using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Reconciliation;

/// <summary>
/// **MODULE `Reconciliation` — TẦNG DOMAIN** (`P0 #1`, 24/10/2026).
///
/// 🔒 Giữ namespace cũ (`eCommerce.Inventory` cho entity/service recon, `eCommerce.Reconciliation` cho mã lỗi)
/// ⇒ tên bảng (`ReconRuns`/`ReconRunMismatches`), route, `using` cũ **không đổi**.
/// ⚠️ **Đánh đổi có ý thức:** module tham chiếu `Inventory.Domain` để **đọc** tồn kho/sổ cái. Kế hoạch L2
/// muốn một hợp đồng đọc (`IStockBalanceReader`); lát cắt này chưa làm — ghi ở `audit-gd1.md`.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule),
    typeof(eCommerce.Inventory.InventoryDomainModule)
)]
public class ReconciliationDomainModule : AbpModule
{
}
