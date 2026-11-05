using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Catalog;

/// <summary>**MODULE `Catalog` — TẦNG APPLICATION.CONTRACTS** (tách khỏi host 24/10/2026, `P0 #1`).</summary>
[DependsOn(
    typeof(CatalogDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class CatalogApplicationContractsModule : AbpModule
{
}
