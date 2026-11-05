using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Sales;

/// <summary>**MODULE `Sales` — APPLICATION.CONTRACTS** (`P0 #1`, 24/10/2026).</summary>
[DependsOn(
    typeof(SalesDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class SalesApplicationContractsModule : AbpModule
{
}
