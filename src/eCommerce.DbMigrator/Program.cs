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
    /// <summary>
    /// Tham số CLI:
    /// <list type="bullet">
    ///   <item>*(không có)* — migration + seed (hành vi cũ, dùng cho CI/deploy);</item>
    ///   <item><c>--backfill-catalog</c> — sau migration + seed, chạy backfill WP-03 (mục 5+6);</item>
    ///   <item><c>--dry-run</c> — đi kèm <c>--backfill-catalog</c>: **chỉ đếm và in**, không ghi.</item>
    /// </list>
    /// 📌 **VÌ SAO DÙNG THAM SỐ CỦA CHÍNH MIGRATOR, không tách app riêng:** backfill cần **cùng** DbContext,
    /// cùng cấu hình connection, cùng đồ thị module với bước migrate; tách app thứ hai là hai nguồn cấu hình
    /// ⇒ có ngày backfill chạy vào DB khác với DB vừa migrate (im lặng).
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var backfillCatalog = args.Any(a => string.Equals(a, "--backfill-catalog", StringComparison.OrdinalIgnoreCase));
        var dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));

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

            if (backfillCatalog)
            {
                var ketQua = await application.ServiceProvider
                    .GetRequiredService<BackfillCatalogService>()
                    .ChayAsync(dryRun);

                // 🔒 Exit code phản ánh **kết quả đối soát**, không phải "đã chạy xong": script/CI phải phân biệt
                // được *"chạy mà lệch"* với *"chạy và khớp"* — nếu chỉ dựa vào exception thì một lần dừng do
                // xung đột vẫn trả 0 và không ai biết. 📌 Dùng **1** (lỗi sản phẩm/dữ liệu), **không** dùng 2 —
                // quy ước cổng `XC-007` BR-006 dành số 2 cho lỗi **môi trường** (script hỏng/không chạy được).
                if (!ketQua.ThanhCong || ketQua.XungDot.Count > 0)
                {
                    return 1;
                }
            }

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
