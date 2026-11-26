using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Promotions.Contracts;

/// <summary>Module tầng Contracts của `Promotions` (`PRO-002` §2).</summary>
[DependsOn(
    typeof(AbpDddApplicationContractsModule),
    typeof(PromotionsDomainModule)
)]
public class PromotionsApplicationContractsModule : AbpModule
{
}
