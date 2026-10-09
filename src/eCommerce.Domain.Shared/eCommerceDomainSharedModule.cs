using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce;

/// <summary>
/// Lớp hằng số, enum và kiểu dùng chung giữa mọi tầng.
/// Không được tham chiếu EF Core / HTTP ở đây.
/// </summary>
[DependsOn(typeof(AbpDddDomainSharedModule))]
public class eCommerceDomainSharedModule : AbpModule
{
}
