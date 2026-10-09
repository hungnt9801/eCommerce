using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace eCommerce.EntityFrameworkCore;

/// <summary>
/// Factory cho công cụ dòng lệnh EF Core (<c>dotnet ef</c>).
///
/// Vì sao cần file này: nếu không có, <c>dotnet ef</c> phải khởi động cả ứng dụng ABP
/// (module + Autofac) chỉ để dựng model — chậm và dễ hỏng trong CI.
/// Với factory này, migration được sinh ra mà KHÔNG cần bật host và KHÔNG cần database.
/// </summary>
public class eCommerceDbContextFactory : IDesignTimeDbContextFactory<eCommerceDbContext>
{
    public eCommerceDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var connectionString = configuration.GetConnectionString(eCommerceConsts.DefaultConnectionStringName)
            ?? "Host=localhost;Port=5432;Database=ecommerce;User ID=ecommerce;Password=ecommerce_dev_only;";

        var builder = new DbContextOptionsBuilder<eCommerceDbContext>()
            .UseNpgsql(connectionString, options =>
            {
                // Giữ migration trong project này (xem thư mục Migrations).
                options.MigrationsAssembly(typeof(eCommerceDbContext).Assembly.FullName);
            });

        return new eCommerceDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
