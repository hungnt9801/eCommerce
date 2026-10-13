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
public class eCommerceIdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    // 🚨 BUG ĐÃ SỬA (C5 — chạy thật với PostgreSQL mới lộ ra, 13/10/2026):
    //
    // Trước đây hằng số này là "admin@ecommerce.local", nhưng ABP có **seeder MẶC ĐỊNH của chính nó**
    // (`Volo.Abp.Identity.Domain` → `eCommerceIdentityDataSeedContributor` với `admin@abp.io`) và nó chạy TRƯỚC.
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
    private readonly ILogger<eCommerceIdentityDataSeedContributor> _logger;

    public eCommerceIdentityDataSeedContributor(
        IIdentityDataSeeder identityDataSeeder,
        IIdentityRoleRepository identityRoleRepository,
        IPermissionManager permissionManager,
        IPermissionDefinitionManager permissionDefinitionManager,
        ILogger<eCommerceIdentityDataSeedContributor> logger)
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

        // 🚨 GUARD BẮT BUỘC NÉM LỖI (C7): nếu danh sách permission RỖNG thì đó KHÔNG phải "không có gì để cấp"
        // mà là **assembly định nghĩa permission chưa được nạp** (thiếu ProjectReference tới
        // `eCommerce.Application.Contracts`). Trước đây trường hợp này **im lặng thành công** với 0 grant
        // ⇒ mọi API trả 403 cho tất cả mọi người mà không ai biết.
        if (permissions.Count == 0)
        {
            throw new InvalidOperationException(
                "Không đọc được permission nào để cấp cho role 'admin'. " +
                "Nguyên nhân thường gặp: DbMigrator THIẾU ProjectReference tới " +
                "'eCommerce.Application.Contracts' (nơi có eCommercePermissionDefinitionProvider). " +
                "Đây KHÔNG phải trạng thái hợp lệ — cấp 0 quyền sẽ làm mọi API trả 403.");
        }

        foreach (var permission in permissions)
        {
            // 🚨 BUG ĐÃ SỬA (C7b): KHÔNG phải permission nào cũng cấp được cho VAI TRÒ.
            // ABP có những permission chỉ dành cho provider khác (ví dụ `AbpIdentity.UserLookup`),
            // cấp cho provider 'R' (role) sẽ NÉM:
            //   "The permission named 'AbpIdentity.UserLookup' is not compatible with the provider named 'R'"
            // ⇒ phải lọc theo danh sách provider mà permission đó cho phép.
            if (!permission.Providers.Contains(RolePermissionValueProvider.ProviderName))
            {
                continue;
            }

            // ⚠️ SetForRoleAsync nhận TÊN role (string), không phải roleId.
            await _permissionManager.SetForRoleAsync(adminRole.Name, permission.Name, true);
        }

        _logger.LogInformation(
            "Đã cấp {Count} permission cho role 'admin' (user: {AdminEmail}).",
            permissions.Count, AdminEmail);
    }
}
