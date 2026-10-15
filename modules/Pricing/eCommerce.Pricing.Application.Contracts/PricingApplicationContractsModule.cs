using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Pricing;

/// <summary>Module hợp đồng (DTO + interface) của **Pricing**.</summary>
[DependsOn(
    typeof(PricingDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class PricingApplicationContractsModule : AbpModule
{
}
