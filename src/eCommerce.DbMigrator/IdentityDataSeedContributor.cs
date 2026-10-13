using Microsoft.Extensions.Logging;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace eCommerce.DbMigrator;

/// <summary>
/// Seed user/role mặc định của Identity + cấp toàn bộ permission cho role <c>admin</c>.
///
/// ⚠️ VÌ SAO PHẢI CẤP PERMISSION TƯỜNG MINH:
/// Các app service của ta dùng <c>[Authorize("eCommerce.Channels.Manage")]</c>…
/// Role <c>admin</c> vẫn cần bản ghi cấp quyền trong Permission Management để chắc chắn
/// có quyền (không phụ thuộc vào quy ước ngầm của framework).
///
/// 🔐 ĐỔI MẬT KHẨU NGAY sau lần deploy đầu tiên — mật khẩu dưới đây chỉ dùng cho dev.
/// </summary>
public class IdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    // 🚨 BUG ĐÃ SỬA (C5 — chạy thật với PostgreSQL mới lộ ra, 13/10/2026):
    //
    // Trước đây hằng số này là "admin@ecommerce.local", nhưng ABP có **seeder MẶC ĐỊNH của chính nó**
    // (`Volo.Abp.Identity.Domain` → `IdentityDataSeedContributor` với `admin@abp.io`) và nó chạy TRƯỚC.
    // Hệ quả đã KIỂM CHỨNG trên DB thật:
    //   • `AbpUsers` chỉ có **admin@abp.io** — KHÔNG hề có `admin@ecommerce.local`
    //   • log Host: "No user found matching username: admin@ecommerce.local" ⇒ không lấy được token
    //   • `AbpPermissionGrants` = **0** ⇒ mọi API có `[Authorize(permission)]` trả **403 cho TẤT CẢ mọi người**
    //   • tài liệu (README, tài liệu này) ghi **sai** tài khoản đăng nhập
    //
    // Nay dùng đúng email thật của ABP để: (1) tìm được user đã tồn tại, (2) khớp với tài liệu,
    // (3) bảo đảm bước cấp permission chạy.
    public const string AdminEmail = "admin@abp.io";
    public const string AdminPassword = "1q2w3E*";

    private readonly IIdentityDataSeeder _identityDataSeeder;
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IPermissionManager _permissionManager;
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;
    private readonly ILogger<IdentityDataSeedContributor> _logger;

    public IdentityDataSeedContributor(
        IIdentityDataSeeder identityDataSeeder,
        IIdentityRoleRepository identityRoleRepository,
        IPermissionManager permissionManager,
        IPermissionDefinitionManager permissionDefinitionManager,
        ILogger<IdentityDataSeedContributor> logger)
    {
        _identityDataSeeder = identityDataSeeder;
        _identityRoleRepository = identityRoleRepository;
        _permissionManager = permissionManager;
        _permissionDefinitionManager = permissionDefinitionManager;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // context.TenantId == null ⇒ seed cho host (đúng cho DbMigrator).
        await _identityDataSeeder.SeedAsync(AdminEmail, AdminPassword, context.TenantId);

        var adminRole = await _identityRoleRepository.FindByNormalizedNameAsync("ADMIN");

        if (adminRole is null)
        {
            _logger.LogWarning("Không tìm thấy role 'admin' — bỏ qua bước cấp permission.");
            return;
        }

        var permissions = await _permissionDefinitionManager.GetPermissionsAsync();

        foreach (var permission in permissions)
        {
            // ⚠️ SetForRoleAsync nhận TÊN role (string), không phải roleId.
            await _permissionManager.SetForRoleAsync(adminRole.Name, permission.Name, true);
        }

        _logger.LogInformation(
            "Đã cấp {Count} permission cho role 'admin' (user: {AdminEmail}).",
            permissions.Count, AdminEmail);
    }
}
