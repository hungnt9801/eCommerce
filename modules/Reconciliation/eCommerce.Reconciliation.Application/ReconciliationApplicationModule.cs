using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Reconciliation;

/// <summary>**MODULE `Reconciliation` — APPLICATION** (`P0 #1`, 24/10/2026).</summary>
[DependsOn(
    typeof(ReconciliationApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ReconciliationApplicationModule : AbpModule
{
}
