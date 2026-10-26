using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Reconciliation;

/// <summary>
/// **MODULE `Reconciliation` — TẦNG DOMAIN** (`P0 #1`, 24/10/2026).
///
/// 🔒 Giữ namespace cũ (`eCommerce.Inventory` cho entity/service recon, `eCommerce.Reconciliation` cho mã lỗi)
/// ⇒ tên bảng (`ReconRuns`/`ReconRunMismatches`), route, `using` cũ **không đổi**.
/// ✅ **Đã gỡ nợ (26/10/2026):** module **không** tham chiếu `Inventory.Domain`; chỉ dùng hợp đồng
/// `IStockBalanceReader`, hiện thực ở host. Kế hoạch L2 được thoả.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ReconciliationDomainModule : AbpModule
{
}
