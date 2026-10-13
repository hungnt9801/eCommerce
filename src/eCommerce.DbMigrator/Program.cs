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
    typeof(EntityFrameworkCore.eCommerceEntityFrameworkCoreModule),
    // 🚨 BUG ĐÃ SỬA (C7) — DÒNG NÀY LÀ MẤU CHỐT, không phải ProjectReference:
    // Chỉ thêm ProjectReference là KHÔNG ĐỦ. Assembly chỉ được nạp khi có module dependency kéo nó vào,
    // và ABP chỉ quét `PermissionDefinitionProvider` trong các assembly thuộc **đồ thị module**.
    // Thiếu dòng này ⇒ `IPermissionDefinitionManager.GetPermissionsAsync()` trả RỖNG
    // ⇒ seeder cấp 0 quyền ⇒ mọi API trả 403 cho tất cả mọi người (đã kiểm chứng bằng DB thật).
    typeof(eCommerceApplicationContractsModule)
)]
public class eCommerceDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 🚨 BUG ĐÃ SỬA (C7 — chạy thật mới lộ ra, 13/10/2026):
        //
        // `eCommerceIdentityDataSeedContributor` là bản sao khái niệm của `IdentityDataSeedContributor`
        // mà ABP có sẵn (Volo.Abp.Identity.Domain). Trước đây class của ta **TRÙNG TÊN** với nó
        // ⇒ đăng ký theo quy ước bị đè/bỏ qua ⇒ seeder **không chạy** ⇒ `AbpPermissionGrants` = 0
        // ⇒ mọi API có `[Authorize(permission)]` trả **403 cho TẤT CẢ mọi người**.
        // (Bằng chứng: role `admin`/`ADMIN` có, user có role đó, seeder thuế cùng assembly ĐÃ chạy,
        //  nhưng 0 grant.)
        //
        // Đã làm 2 việc: (1) ĐỔI TÊN class để hết trùng; (2) ĐĂNG KÝ TƯỜNG MINH ở đây để không phụ
        // thuộc vào quy ước quét assembly — seeder cấp quyền là thứ **bắt buộc phải chạy**.
        context.Services.AddTransient<Volo.Abp.Data.IDataSeedContributor, eCommerceIdentityDataSeedContributor>();
    }
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
            // 🚨 SỬA LỖI ĐÃ GẶP THẬT (khi mới bật PostgreSQL):
            // Trước đây chỉ ghi qua ILogger<Program>, NHƯNG app console không đăng ký logging
            // provider nào ⇒ log đi vào hư không ⇒ migration thất bại mà KHÔNG có một dòng
            // thông báo, chỉ thấy exit code 1. Ghi thẳng ra stderr để luôn nhìn thấy.
            Console.Error.WriteLine("=== MIGRATION THẤT BẠI ===");
            Console.Error.WriteLine(ex.ToString());

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
