using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.EInvoice;

/// <summary>Module tầng Domain của `EInvoice` — sở hữu hoá đơn điện tử (`EINV-001`).</summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class EInvoiceDomainModule : AbpModule
{
}
