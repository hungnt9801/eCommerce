using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Signing;

/// <summary>Module tầng Domain của `Signing` — sở hữu quy trình ký 2 bước (`SIGN-001`).</summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class SigningDomainModule : AbpModule
{
}
