using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.ChannelHub.EntityFrameworkCore;

/// <summary>
/// Project này chỉ cung cấp <b>cấu hình mapping EF Core</b> cho entity của ChannelHub.
///
/// Trong modular monolith, app chỉ có MỘT DbContext (eCommerceDbContext) và gọi
/// <c>builder.ConfigureChannelHub()</c>. Cách này giữ được ranh giới module mà không
/// phải gộp nhiều DbContext ở runtime — đúng hướng dẫn của ABP.
///
/// Khi tách ChannelHub thành microservice: đổi project này thành nơi đăng ký
/// <c>AddAbpDbContext&lt;ChannelHubDbContext&gt;()</c> và tự chạy migration riêng.
/// </summary>
[DependsOn(
    typeof(ChannelHubDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ChannelHubEntityFrameworkCoreModule : AbpModule
{
}
