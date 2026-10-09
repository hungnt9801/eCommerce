using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;

namespace eCommerce.DbMigrator;

/// <summary>
/// Ứng dụng console chạy migration + seed dữ liệu nền.
/// Tách khỏi Host để CI/CD có thể chạy migration trước khi deploy phiên bản mới.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(EntityFrameworkCore.eCommerceEntityFrameworkCoreModule)
)]
public class eCommerceDbMigratorModule : AbpModule
{
}

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var application = await AbpApplicationFactory.CreateAsync<eCommerceDbMigratorModule>(options =>
        {
            options.UseAutofac();
        });

        await application.InitializeAsync();

        try
        {
            await application.ServiceProvider
                .GetRequiredService<eCommerceDbMigrationService>()
                .MigrateAsync();

            await application.ServiceProvider
                .GetRequiredService<IDataSeeder>()
                .SeedAsync();

            return 0;
        }
        catch (Exception ex)
        {
            application.ServiceProvider
                .GetRequiredService<ILogger<Program>>()
                .LogError(ex, "Migration thất bại.");

            return 1;
        }
        finally
        {
            await application.ShutdownAsync();
        }
    }
}
