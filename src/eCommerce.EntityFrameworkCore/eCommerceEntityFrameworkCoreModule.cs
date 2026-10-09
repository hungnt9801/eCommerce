using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;

namespace eCommerce.EntityFrameworkCore;

/// <summary>
/// Đăng ký DbContext + provider PostgreSQL. Đây là project DUY NHẤT biết DBMS cụ thể;
/// tầng Domain/Application không biết gì về PostgreSQL (đổi sang SQL Server/MySQL =
/// chỉ sửa file này + ConnectionStrings).
/// </summary>
[DependsOn(
    typeof(eCommerceDomainModule),
    typeof(ChannelHub.EntityFrameworkCore.ChannelHubEntityFrameworkCoreModule),
    typeof(PaymentHub.EntityFrameworkCore.PaymentHubEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
)]
public class eCommerceEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<eCommerceDbContext>(options =>
        {
            // Tự tạo repository cho mọi aggregate root ⇒ inject IRepository<T, Guid> ở mọi tầng.
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}
