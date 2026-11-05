using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Users;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// Endpoint hệ thống — dùng để **kiểm chứng đường dọc** DB → API → UI trong 10 ngày đầu
/// (`07 §7`). `ping` công khai (kiểm kết nối); `me`/`permissions` cần token.
/// </summary>
[ApiController]
[Route("api/v1/system")]
public class SystemController : AbpControllerBase
{
    private const int SoQuyenToiDa = 300;

    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;

    public SystemController(ICurrentUser currentUser, IPermissionChecker permissionChecker)
    {
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
    }

    [HttpGet("ping")]
    [AllowAnonymous]
    public ActionResult<PingResult> Ping()
    {
        return new PingResult(
            Service: "eCommerce.HttpApi",
            ApiVersion: "v1",
            ServerTimeUtc: DateTime.UtcNow,
            Authenticated: _currentUser.IsAuthenticated);
    }

    /// <summary>Trả claim thực tế của token — FE dùng để dựng menu theo quyền (không đoán).</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<MeResult> Me()
    {
        var permissions = HttpContext.User.FindAll("permission").Select(c => c.Value).Distinct().OrderBy(x => x).ToList();

        return new MeResult(
            UserId: _currentUser.Id,
            UserName: _currentUser.UserName,
            TenantId: _currentUser.TenantId,
            Permissions: permissions,
            PermissionCount: permissions.Count);
    }

    /// <summary>
    /// Hỏi **server** xem trong danh sách quyền được hỏi, quyền nào đang được cấp.
    ///
    /// 🔑 **VÌ SAO KHÔNG ĐỌC CLAIM `permission` TỪ TOKEN:** token phát lúc đăng nhập, còn quyền có thể
    /// đổi sau đó (admin sửa role) ⇒ đọc token là **dữ liệu cũ**. Hàm này đi qua `IPermissionChecker`
    /// (có cache phía server) nên luôn khớp DB, và trả về **đúng danh sách FE hỏi** (menu 180 mục).
    /// </summary>
    [HttpGet("permissions")]
    [Authorize]
    public async Task<PermissionsResult> PermissionsAsync([FromQuery] string? names)
    {
        var hoi = (names ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Take(SoQuyenToiDa)
            .ToArray();

        if (hoi.Length == 0)
        {
            return new PermissionsResult(Array.Empty<string>(), 0);
        }

        var ketQua = await _permissionChecker.IsGrantedAsync(hoi);
        var duocCap = hoi
            .Where(x => ketQua.Result.TryGetValue(x, out var r) && r == PermissionGrantResult.Granted)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new PermissionsResult(duocCap, duocCap.Length);
    }

    public sealed record PingResult(string Service, string ApiVersion, DateTime ServerTimeUtc, bool Authenticated);

    public sealed record MeResult(Guid? UserId, string? UserName, Guid? TenantId, IReadOnlyList<string> Permissions, int PermissionCount);

    public sealed record PermissionsResult(IReadOnlyList<string> Granted, int Count);
}
