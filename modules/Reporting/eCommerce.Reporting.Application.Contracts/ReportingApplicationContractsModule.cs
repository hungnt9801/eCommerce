using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Reporting;

/// <summary>**MODULE `Reporting` — APPLICATION CONTRACTS** (`WP-08`): DTO + hợp đồng app service.</summary>
[DependsOn(
    typeof(ReportingDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class ReportingApplicationContractsModule : AbpModule
{
}
