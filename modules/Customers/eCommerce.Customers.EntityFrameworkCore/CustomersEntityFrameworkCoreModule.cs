using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Customers.EntityFrameworkCore;

/// <summary>Module hạ tầng lưu trữ của **Customers** (`CUS-001`).</summary>
[DependsOn(
    typeof(CustomersDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class CustomersEntityFrameworkCoreModule : AbpModule
{
}
