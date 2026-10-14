using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Signing.EntityFrameworkCore;

/// <summary>Module hạ tầng lưu trữ của `Signing`.</summary>
[DependsOn(
    typeof(SigningDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class SigningEntityFrameworkCoreModule : AbpModule
{
}
