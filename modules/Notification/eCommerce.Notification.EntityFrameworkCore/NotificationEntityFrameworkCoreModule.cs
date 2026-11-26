using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Notification.EntityFrameworkCore;

/// <summary>
/// Project chỉ cung cấp cấu hình mapping EF cho entity của module `Notification`.
/// Được gọi từ DbContext hợp nhất qua `ConfigureNotification(builder)`; kéo `Notification.Domain`
/// vào đồ thị module (⇒ seed contributor + DI đăng ký).
/// </summary>
[DependsOn(
    typeof(NotificationDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class NotificationEntityFrameworkCoreModule : AbpModule
{
}
