using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Pos;

/// <summary>**MODULE `Pos` — APPLICATION.CONTRACTS** (`WP-06`).</summary>
[DependsOn(
    typeof(PosDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class PosApplicationContractsModule : AbpModule
{
}
