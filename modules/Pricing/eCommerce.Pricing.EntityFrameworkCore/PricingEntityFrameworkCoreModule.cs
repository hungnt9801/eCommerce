using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Pricing.EntityFrameworkCore;

/// <summary>Module hạ tầng lưu trữ của **Pricing** (`PRC-001`).</summary>
[DependsOn(
    typeof(PricingDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class PricingEntityFrameworkCoreModule : AbpModule
{
}
