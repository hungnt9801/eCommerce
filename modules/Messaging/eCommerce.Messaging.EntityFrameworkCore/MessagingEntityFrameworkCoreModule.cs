using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Messaging.EntityFrameworkCore;

/// <summary>
/// Module hạ tầng lưu trữ của `Messaging`.
/// 📌 Model được cấu hình trên **DbContext hợp nhất** (`eCommerceDbContext`) qua
/// <see cref="MessagingDbContextModelCreatingExtensions.ConfigureMessaging"/> — đúng quy ước dự án.
/// </summary>
[DependsOn(
    typeof(MessagingDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class MessagingEntityFrameworkCoreModule : AbpModule
{
}
