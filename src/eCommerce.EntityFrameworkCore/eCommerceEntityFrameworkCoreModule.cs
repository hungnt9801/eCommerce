using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;

namespace eCommerce.EntityFrameworkCore;

/// <summary>
/// Đăng ký DbContext + provider PostgreSQL. Đây là project DUY NHẤT biết DBMS cụ thể;
/// tầng Domain/Application không biết gì về PostgreSQL (đổi sang SQL Server/MySQL =
/// chỉ sửa file này + appsettings).
///
/// 📌 Phase 0 — module ABP do ABP CLI thêm; attribute đã gộp lại cho dễ đọc.
/// ⚠️ BUG CỦA CLI 10.7.0: ghi thiếu tiền tố <c>Abp</c> cho module BackgroundJobs
/// (<c>BackgroundJobsEntityFrameworkCoreModule</c>) ⇒ đã sửa thành
/// <c>AbpBackgroundJobsEntityFrameworkCoreModule</c>.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainModule),
    typeof(ChannelHub.EntityFrameworkCore.ChannelHubEntityFrameworkCoreModule),
    typeof(PaymentHub.EntityFrameworkCore.PaymentHubEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule),
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule)
)]
public class eCommerceEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<eCommerceDbContext>(options =>
        {
            // Tự tạo repository cho entity có DbSet trên eCommerceDbContext
            // ⇒ inject IRepository<T, Guid> ở mọi tầng.
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        // 🚨 BẮT BUỘC — BÀI HỌC KHI CHẠY THẬT:
        // Phải chọn provider cho AbpDbContextOptions, không chỉ cho eCommerceDbContext.
        // Mỗi module ABP (OpenIddict, AuditLogging, Identity…) tự đăng ký DbContext RIÊNG
        // (OpenIddictDbContext, AbpAuditLoggingDbContext…). Thiếu dòng dưới đây thì runtime báo:
        //   "No configuration found for Microsoft.EntityFrameworkCore.DbContext!
        //    Use services.Configure<AbpDbContextOptions>(...) to configure it."
        // ⇒ mọi API chạm DB đều trả 500. Lỗi này KHÔNG lộ khi build/migration, chỉ lộ khi chạy.
        Configure<AbpDbContextOptions>(options =>
        {
            options.UseNpgsql();
        });
    }
}
