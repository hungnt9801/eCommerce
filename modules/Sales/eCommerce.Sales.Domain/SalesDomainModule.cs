using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Sales;

/// <summary>
/// **MODULE `Sales` — TẦNG DOMAIN** (`P0 #1`, 24/10/2026).
///
/// 🔒 Giữ namespace cũ (`eCommerce.Orders` cho `Order`/`OrderLine`/`Marketplace*`) ⇒ tên bảng
/// (`OrdOrders`, `OrdOrderLines`, `OrdStatusHistory`, `SalMarketplace*`), route và `using` cũ **không đổi**.
/// 📌 Application của module dùng namespace cũ `eCommerce.Application.Sales`/`…Contracts.Sales` — cũng để
/// **không đổi route** và không phải sửa controller/test.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class SalesDomainModule : AbpModule
{
}
