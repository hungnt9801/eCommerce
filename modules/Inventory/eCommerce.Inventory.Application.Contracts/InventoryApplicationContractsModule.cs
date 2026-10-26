using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Inventory;

/// <summary>**MODULE `Inventory` — APPLICATION.CONTRACTS** (tách khỏi host 24/10/2026, `P0 #1`).</summary>
[DependsOn(
    typeof(InventoryDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class InventoryApplicationContractsModule : AbpModule
{
}
