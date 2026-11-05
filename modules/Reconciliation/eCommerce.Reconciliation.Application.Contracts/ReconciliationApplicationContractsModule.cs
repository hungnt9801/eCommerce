using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Reconciliation;

/// <summary>**MODULE `Reconciliation` — APPLICATION.CONTRACTS** (`P0 #1`, 24/10/2026).</summary>
[DependsOn(
    typeof(ReconciliationDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class ReconciliationApplicationContractsModule : AbpModule
{
}
