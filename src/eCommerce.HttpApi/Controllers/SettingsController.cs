using eCommerce.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// 🆕 **`WP-07`** — **CẤU HÌNH TỔ CHỨC & MA TRẬN QUYỀN**, bề mặt `/api/v1/settings/**`
/// (`07 §WP-07`: *"FE: … nhân viên & ma trận quyền"*).
///
/// 🔐 **HAI MỨC QUYỀN, KHÔNG GỘP:**
/// <list type="bullet">
///   <item>`GET` ⇒ `Settings.Default` — xem cấu hình (nhân viên/cửa hàng/phòng ban/phạm vi/quyền).</item>
///   <item>`POST`/`PUT` ⇒ `Settings.Manage` — **đổi phạm vi dữ liệu** và **cấp/thu quyền**. 🚨 Đây là quyền
///     **phân quyền**, tách khỏi `Customers.Manage`: người trực CSKH cần sửa hồ sơ khách **không** đương nhiên
///     được nới phạm vi dữ liệu của chính mình (leo thang quyền im lặng).</item>
/// </list>
/// 📌 Không có đường **xoá** nhân viên/cửa hàng/phòng ban: dữ liệu chứng từ đã trỏ vào chúng ⇒ xoá cứng là
/// **tham chiếu treo**. Muốn ngừng dùng thì đặt `isActive = false`.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Authorize(eCommerceConsts.Permissions.Settings.Default)]
public class SettingsController : AbpControllerBase
{
    private readonly IStaffAppService _toChuc;
    private readonly IPermissionMatrixAppService _quyen;

    public SettingsController(IStaffAppService toChuc, IPermissionMatrixAppService quyen)
    {
        _toChuc = toChuc;
        _quyen = quyen;
    }

    // ── NHÂN VIÊN ─────────────────────────────────────────────────────────────────────────────────

    [HttpGet("staff")]
    public Task<List<StaffDto>> GetStaffAsync() => _toChuc.GetStaffAsync();

    [HttpPost("staff")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<StaffDto> CreateStaffAsync([FromBody] TaoNhanVienInput input) => _toChuc.CreateStaffAsync(input);

    [HttpPut("staff/{id:guid}")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<StaffDto> UpdateStaffAsync(Guid id, [FromBody] TaoNhanVienInput input)
        => _toChuc.UpdateStaffAsync(id, input);

    // ── CỬA HÀNG ──────────────────────────────────────────────────────────────────────────────────

    [HttpGet("stores")]
    public Task<List<StoreDto>> GetStoresAsync() => _toChuc.GetStoresAsync();

    [HttpPost("stores")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<StoreDto> CreateStoreAsync([FromBody] TaoCuaHangInput input) => _toChuc.CreateStoreAsync(input);

    [HttpPut("stores/{id:guid}")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<StoreDto> UpdateStoreAsync(Guid id, [FromBody] TaoCuaHangInput input)
        => _toChuc.UpdateStoreAsync(id, input);

    // ── PHÒNG BAN ─────────────────────────────────────────────────────────────────────────────────

    [HttpGet("departments")]
    public Task<List<DepartmentDto>> GetDepartmentsAsync() => _toChuc.GetDepartmentsAsync();

    [HttpPost("departments")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<DepartmentDto> CreateDepartmentAsync([FromBody] TaoPhongBanInput input)
        => _toChuc.CreateDepartmentAsync(input);

    [HttpPut("departments/{id:guid}")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<DepartmentDto> UpdateDepartmentAsync(Guid id, [FromBody] TaoPhongBanInput input)
        => _toChuc.UpdateDepartmentAsync(id, input);

    // ── PHẠM VI DỮ LIỆU ───────────────────────────────────────────────────────────────────────────

    /// <summary>DS phạm vi **đã cấu hình** — vai trò chưa có dòng ⇒ hiệu lực `All`.</summary>
    [HttpGet("data-scopes")]
    public Task<List<RoleDataScopeDto>> GetDataScopesAsync() => _toChuc.GetRoleDataScopesAsync();

    /// <summary>Phạm vi **hiệu lực của chính tôi** — 📌 để người dùng hiểu vì sao danh sách ngắn hơn đồng nghiệp.</summary>
    [HttpGet("data-scopes/me")]
    public Task<PhamViCuaToiDto> GetMyScopeAsync() => _toChuc.GetPhamViCuaToiAsync();

    [HttpPut("data-scopes")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<RoleDataScopeDto> SetDataScopeAsync([FromBody] DatPhamViDuLieuInput input)
        => _toChuc.SetRoleDataScopeAsync(input);

    // ── MA TRẬN QUYỀN ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Toàn bộ ma trận (nhóm quyền × vai trò × grant trực tiếp).</summary>
    [HttpGet("permissions")]
    public Task<PermissionMatrixDto> GetPermissionsAsync() => _quyen.GetMatrixAsync();

    /// <summary>Đặt **một ô** của ma trận — trả lại trạng thái **đã lưu**.</summary>
    [HttpPut("permissions")]
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public Task<DatQuyenKetQuaDto> SetPermissionAsync([FromBody] DatQuyenInput input) => _quyen.SetAsync(input);
}
