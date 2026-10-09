using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce;

/// <summary>
/// DTO + interface application service. Đây là hợp đồng công khai (HTTP API, client khác).
/// Không được tham chiếu entity của tầng Domain ra ngoài qua tầng này.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class eCommerceApplicationContractsModule : AbpModule
{
}
