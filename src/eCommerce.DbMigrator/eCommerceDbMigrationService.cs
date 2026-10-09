using eCommerce.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace eCommerce.DbMigrator;

/// <summary>
/// Áp dụng EF Core migration cho <see cref="eCommerceDbContext"/>.
///
/// ⚠️ Cần có ít nhất một migration trong project eCommerce.EntityFrameworkCore.
/// Vì môi trường dựng khung không truy cập được nuget.org nên KHÔNG thể chạy:
///     dotnet ef migrations add Initial --project src/eCommerce.EntityFrameworkCore --startup-project src/eCommerce.DbMigrator
/// Hãy chạy lệnh trên ở máy có mạng (xem README).
/// </summary>
public class eCommerceDbMigrationService : ITransientDependency
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<eCommerceDbMigrationService> _logger;

    public eCommerceDbMigrationService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<eCommerceDbMigrationService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<eCommerceDbContext>();

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            _logger.LogInformation("Không có migration nào đang chờ. Database đã cập nhật.");
            return;
        }

        _logger.LogInformation("Áp dụng {Count} migration: {Migrations}", pending.Count, string.Join(", ", pending));

        await dbContext.Database.MigrateAsync(cancellationToken);

        _logger.LogInformation("Migration hoàn tất.");
    }
}
