using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Address;

/// <summary>Module tầng Domain của `Address` — sở hữu danh mục &amp; xác minh ĐVHC (`ADDR-001`).</summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class AddressDomainModule : AbpModule
{
}
