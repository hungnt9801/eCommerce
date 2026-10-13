using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Address.EntityFrameworkCore;

/// <summary>
/// Module hạ tầng lưu trữ của `Address`.
/// 📌 Model cấu hình trên **DbContext hợp nhất** qua
/// <see cref="AddressDbContextModelCreatingExtensions.ConfigureAddress"/> — đúng quy ước dự án.
/// </summary>
[DependsOn(
    typeof(AddressDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class AddressEntityFrameworkCoreModule : AbpModule
{
}
