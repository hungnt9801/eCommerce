using Microsoft.Extensions.Logging;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace eCommerce.Settings;

/// <summary>
/// 🔐 **MA TRẬN QUYỀN (permission × vai trò)** — `WP-07` (`07 §WP-07`: *"FE: … nhân viên & ma trận quyền"*).
///
/// 🚨 **VÌ SAO ĐỌC TỪ `IPermissionDefinitionManager` (không chép danh sách vào FE/DB):**
/// danh sách quyền **đang sống** trong `PermissionDefinitionProvider`. Một bảng viết tay ở FE sẽ **không**
/// có quyền thêm sau này ⇒ người vận hành **không cấp được**, và **không ai biết** là thiếu (màn hình trông
/// vẫn đầy đủ). 📌 Cùng nguyên tắc `D-41` (tab là dữ liệu) và `D-33` (type từ schema).
///
/// 🔒 **Quyền gọi = `Settings.Default` (đọc) / `Settings.Manage` (ghi)** — đặt ở controller và ở đây
/// (hai lớp) để không ai gọi thẳng app service mà bỏ qua kiểm quyền.
///
/// ⚠️ **Đây KHÔNG phải màn quản trị người dùng của ABP:** nó chỉ trả lời *"vai trò X có quyền Y không"*.
/// Việc gán **vai trò cho người dùng** vẫn là việc của ABP Identity (chưa làm ở `WP-07`).
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize(eCommerceConsts.Permissions.Settings.Default)]
public class PermissionMatrixAppService : Volo.Abp.Application.Services.ApplicationService,
    eCommerce.Customers.IPermissionMatrixAppService
{
    /// <summary>Provider name của ABP cho grant theo **role** — 🔒 hằng số của framework, không tự chế.</summary>
    private const string RoleProvider = "R";

    private readonly IPermissionDefinitionManager _dinhNghia;
    private readonly IPermissionManager _quyen;
    private readonly IIdentityRoleRepository _vaiTro;

    public PermissionMatrixAppService(
        IPermissionDefinitionManager dinhNghia,
        IPermissionManager quyen,
        IIdentityRoleRepository vaiTro)
    {
        _dinhNghia = dinhNghia;
        _quyen = quyen;
        _vaiTro = vaiTro;
    }

    /// <summary>
    /// **ĐỌC TOÀN BỘ MA TRẬN** — nhóm quyền (cây) + các vai trò + grant **trực tiếp** cho từng vai trò.
    /// 📌 Grant **trực tiếp**: quyền thừa hưởng từ vai trò cha của ABP không nằm trong đây — ghi rõ để
    /// người vận hành không tưởng *"ô trống = không có quyền"* khi thực tế được thừa hưởng.
    /// </summary>
    public async Task<Customers.PermissionMatrixDto> GetMatrixAsync()
    {
        // 🔑 Đọc **nhóm** (cây) chứ không dùng `PermissionDefinition.Group` — ABP 10 đã bỏ thuộc tính đó
        // trên `PermissionDefinition`; nguồn sự thật của cây là `PermissionGroupDefinition`.
        var nhom = await _dinhNghia.GetGroupsAsync();

        var dsVaiTro = await _vaiTro.GetListAsync(sorting: nameof(IdentityRole.Name), skipCount: 0, maxResultCount: 1000);

        var tenVaiTro = dsVaiTro
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tatCa = new List<PermissionDefinition>();
        var groups = new List<Customers.PermissionGroupDto>();

        foreach (var g in nhom.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            var ds = g.Permissions.SelectMany(LayCaCay).Where(x => x.IsEnabled).ToList();
            tatCa.AddRange(ds);

            groups.Add(new Customers.PermissionGroupDto
            {
                Name = g.Name,
                Permissions = ds
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => new Customers.PermissionDto
                    {
                        Name = x.Name,
                        DisplayName = TenNgan(g.Name, x.Name),
                        ParentName = x.Parent?.Name,
                        // 📌 Quyền **đọc** là hậu tố `.View` (quy ước `Products.View`, `TaxClassification.View`…)
                        // — FE tô màu khác để người vận hành thấy mình đang cấp **quyền ghi**.
                        LaQuyenDoc = x.Name.EndsWith(".View", StringComparison.Ordinal)
                    })
                    .ToList()
            });
        }

        var granted = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var vaiTro in tenVaiTro)
        {
            var ds = new List<string>();

            foreach (var p in tatCa)
            {
                var ketQua = await _quyen.GetAsync(p.Name, RoleProvider, vaiTro);

                if (ketQua.IsGranted)
                {
                    ds.Add(p.Name);
                }
            }

            granted[vaiTro] = ds;
        }

        return new Customers.PermissionMatrixDto
        {
            Groups = groups,
            Roles = tenVaiTro,
            GrantedByRole = granted
        };
    }

    /// <summary>
    /// **ĐẶT MỘT Ô** của ma trận. 📌 Trả lại **trạng thái đã lưu** để UI không phải đoán; nếu quyền
    /// **không tồn tại** ⇒ `BusinessException` (không im lặng bỏ qua — bỏ qua thì người dùng tưởng đã cấp).
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<Customers.DatQuyenKetQuaDto> SetAsync(Customers.DatQuyenInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ten = input.PermissionName.Trim();
        var tonTai = (await _dinhNghia.GetPermissionsAsync()).FirstOrDefault(x => x.Name == ten);

        if (tonTai is null)
        {
            throw new Volo.Abp.BusinessException(SettingsErrorCodes.QuyenKhongTonTai)
                .WithData("permission", ten)
                .WithData("lyDo", "Permission KHÔNG tồn tại trong định nghĩa của hệ thống.");
        }

        if ((await _vaiTro.FindByNormalizedNameAsync(input.RoleName.Trim().ToUpperInvariant())) is null)
        {
            throw new Volo.Abp.BusinessException(SettingsErrorCodes.VaiTroKhongTonTai)
                .WithData("role", input.RoleName)
                .WithData("lyDo", "Vai trò KHÔNG tồn tại (tên vai trò phân biệt hoa/thường theo ABP).");
        }

        await _quyen.SetAsync(ten, RoleProvider, input.RoleName.Trim(), input.IsGranted);

        Logger.LogWarning("ĐÃ ĐỔI QUYỀN: vai trò {VaiTro} · {Permission} = {GiaTri} (bởi {NguoiDoi}). [WP07-QUYEN]",
            input.RoleName, ten, input.IsGranted, CurrentUser.UserName);

        // 🔑 Đọc lại từ nguồn sự thật (không trả `input.IsGranted`) — nếu store chuẩn hoá khác, UI phải thấy
        // **sự thật**, không phải điều nó vừa gửi lên.
        var daLuu = await _quyen.GetAsync(ten, RoleProvider, input.RoleName.Trim());

        return new Customers.DatQuyenKetQuaDto
        {
            RoleName = input.RoleName.Trim(),
            PermissionName = ten,
            IsGranted = daLuu.IsGranted
        };
    }

    /// <summary>Duyệt permission và **toàn bộ** permission con (cây) — bỏ bước đệ quy là **mất** quyền con.</summary>
    private static IEnumerable<PermissionDefinition> LayCaCay(PermissionDefinition goc)
    {
        yield return goc;

        foreach (var con in goc.Children)
        {
            foreach (var chau in LayCaCay(con))
            {
                yield return chau;
            }
        }
    }

    /// <summary>Tên ngắn để hiển thị: bỏ tiền tố group (`eCommerce.Customers.Manage` → `Customers.Manage`).</summary>
    private static string TenNgan(string groupName, string permissionName)
        => permissionName.StartsWith(groupName + ".", StringComparison.Ordinal)
            ? permissionName[(groupName.Length + 1)..]
            : permissionName;
}
