using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Sales;

/// <summary>
/// **MODULE `Sales` — APPLICATION** (`P0 #1`, 24/10/2026).
/// ⚠️ **Nợ có ý thức:** module tham chiếu `Inventory.Domain` + `Pricing.Domain` để gọi dịch vụ miền
/// (giữ chỗ, tính tiền) — kế hoạch L2 muốn hợp đồng (`IStockReservation`/`IPriceResolver`). Ghi ở
/// `audit-gd1.md`; làm hợp đồng là lát cắt sau.
/// </summary>
[DependsOn(
    typeof(SalesApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class SalesApplicationModule : AbpModule
{
}
