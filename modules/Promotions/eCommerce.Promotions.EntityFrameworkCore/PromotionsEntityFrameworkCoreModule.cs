using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Promotions.EntityFrameworkCore;

/// <summary>
/// Project cung cấp mapping EF cho 7 bảng `Pro*` của module `Promotions`.
/// Được gọi **MỘT LẦN** từ `eCommerceDbContext.OnModelCreating` qua `ConfigurePromotions(builder)`.
/// </summary>
[DependsOn(
    typeof(PromotionsDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class PromotionsEntityFrameworkCoreModule : AbpModule
{
}
