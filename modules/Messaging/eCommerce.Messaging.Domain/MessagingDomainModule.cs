using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Messaging;

/// <summary>
/// Module tầng Domain của `Messaging` — sở hữu hợp đồng chống trùng ở biên vào (`XC-002`).
/// </summary>
[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(eCommerce.Kernel.eCommerceKernelModule)
)]
public class MessagingDomainModule : AbpModule
{
}
