using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Customers;

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// `WP-07` — DTO của màn **NHÂN VIÊN & MA TRẬN QUYỀN** (`07 §WP-07`: *"FE: … nhân viên & ma trận quyền"*).
//
// 🔑 **VÌ SAO CÓ CẢ CỬA HÀNG / PHÒNG BAN TRONG CÙNG MÀN:** phạm vi dữ liệu `Store` cần biết *"nhân viên
// thuộc cửa hàng nào"*, mà cửa hàng cần biết *"kho nào"*. Ba bảng này **chỉ có nghĩa khi đứng cạnh nhau**
// — tách 3 màn hình riêng là 3 lần nhập liệu cho **một** câu hỏi.
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

public class StoreDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>Kho của cửa hàng — 🔑 dùng cho phạm vi dữ liệu `Store`. `null` = chưa gắn.</summary>
    public Guid? WarehouseId { get; set; }

    public string? AddressText { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public class TaoCuaHangInput
{
    [Required]
    [StringLength(Store.DoDaiMaToiDa)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(Store.DoDaiTenToiDa)]
    public string Name { get; set; } = default!;

    public Guid? WarehouseId { get; set; }

    [StringLength(512)]
    public string? AddressText { get; set; }

    [StringLength(32)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}

public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid? StoreId { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}

public class TaoPhongBanInput
{
    [Required]
    [StringLength(Department.DoDaiMaToiDa)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(Department.DoDaiTenToiDa)]
    public string Name { get; set; } = default!;

    public Guid? StoreId { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StaffDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>Tài khoản đăng nhập — `null` = chưa cấp (`NhanVienChuaCoTaiKhoan` khi thao tác cần).</summary>
    public Guid? IdentityUserId { get; set; }

    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>Vai trò chính — 🔑 khoá tra phạm vi dữ liệu.</summary>
    public string? RoleName { get; set; }

    /// <summary>Phạm vi dữ liệu **hiệu lực** của vai trò (`All`/`Store`/`Own`) — suy từ `SetRoleDataScopes`.
    /// 📌 Trả kèm để màn hình **không phải** tự tra bảng phạm vi (và để hai bên không lệch).</summary>
    public string PhamViHieuLuc { get; set; } = default!;

    public string? Phone { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
}

public class TaoNhanVienInput
{
    [Required]
    [StringLength(Staff.DoDaiMaToiDa)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(Staff.DoDaiTenToiDa)]
    public string Name { get; set; } = default!;

    public Guid? IdentityUserId { get; set; }
    public Guid? StoreId { get; set; }
    public Guid? DepartmentId { get; set; }

    [StringLength(RoleDataScope.DoDaiVaiTroToiDa)]
    public string? RoleName { get; set; }

    [StringLength(32)]
    public string? Phone { get; set; }

    [StringLength(1024)]
    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;
}

public class RoleDataScopeDto
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = default!;

    /// <summary>`All` / `Store` / `Own` — 📌 trả **chuỗi** (client không phải biết số enum, cùng `D-33`).</summary>
    public string Scope { get; set; } = default!;

    public string ScopeLabel { get; set; } = default!;
}

public class DatPhamViDuLieuInput
{
    [Required]
    [StringLength(RoleDataScope.DoDaiVaiTroToiDa)]
    public string RoleName { get; set; } = default!;

    [Required]
    public PhamViDuLieu Scope { get; set; }
}

/// <summary>
/// **PHẠM VI DỮ LIỆU HIỆU LỰC CỦA NGƯỜI GỌI** — DTO của API (không trả thẳng record nội bộ).
/// 🔑 **VÌ SAO PHẢI CÓ DTO RIÊNG:** record `PhamViHienTai` mang **enum số**; UI cần **chuỗi** để tra nhãn
/// (`D-33`: client không phải biết số enum) và cần **lý do** để trả lời *"sao danh sách của tôi ngắn"*.
/// </summary>
public class PhamViCuaToiDto
{
    /// <summary>`All` / `Store` / `Own`.</summary>
    public string PhamVi { get; set; } = default!;

    /// <summary>Vì sao ra phạm vi này (ghi log/audit; hiện trên UI để người dùng hiểu).</summary>
    public string LyDo { get; set; } = default!;

    public Guid? NhanVienId { get; set; }

    /// <summary>Kho của cửa hàng — `null` nghĩa là phạm vi `Store` sẽ lọc ra **rỗng** (cấu hình thiếu).</summary>
    public Guid? KhoCuaHangId { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// MA TRẬN QUYỀN
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// **MA TRẬN QUYỀN** (permission × role) — đọc từ **định nghĩa permission thật** + **grant thật** của ABP.
///
/// 🚨 **VÌ SAO KHÔNG CHÉP DANH SÁCH QUYỀN VÀO FE:** một bảng viết tay trong `page.tsx` sẽ **không** có
/// permission mới thêm sau này ⇒ người vận hành **không cấp được** quyền đó, và **không ai biết** là thiếu
/// (màn hình trông vẫn đầy đủ). 📌 Cùng lý do `D-41` (tab là dữ liệu) và `D-33` (type từ schema).
/// </summary>
public class PermissionMatrixDto
{
    /// <summary>Các nhóm quyền (theo group → permission con, đúng cây của `PermissionDefinitionProvider`).</summary>
    public List<PermissionGroupDto> Groups { get; set; } = new();

    /// <summary>Tên các vai trò hiện có (ABP `AbpRoles`).</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>`role → danh sách permission đang được CẤP` (chỉ cấp trực tiếp cho role).</summary>
    public Dictionary<string, List<string>> GrantedByRole { get; set; } = new();
}

public class PermissionGroupDto
{
    /// <summary>Tên nhóm (`eCommerce`, …).</summary>
    public string Name { get; set; } = default!;

    public List<PermissionDto> Permissions { get; set; } = new();
}

public class PermissionDto
{
    /// <summary>Tên đầy đủ (`eCommerce.Customers.Manage`).</summary>
    public string Name { get; set; } = default!;

    /// <summary>Tên ngắn để hiển thị (bỏ tiền tố group) — 📌 FE **không** tự cắt chuỗi (dễ sai khi tên lồng nhau).</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>Cấp cha (`null` = permission gốc của nhóm) — dùng để thụt lề trong ma trận.</summary>
    public string? ParentName { get; set; }

    /// <summary>📌 `true` = quyền **đọc**; FE tô màu khác để người vận hành thấy mình đang cấp quyền ghi.</summary>
    public bool LaQuyenDoc { get; set; }
}

/// <summary>Đặt **một ô** của ma trận (role × permission).</summary>
public class DatQuyenInput
{
    [Required]
    [StringLength(RoleDataScope.DoDaiVaiTroToiDa)]
    public string RoleName { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string PermissionName { get; set; } = default!;

    public bool IsGranted { get; set; }
}

/// <summary>Kết quả sau khi đổi một ô — 📌 trả **ma trận mới** để UI không phải đoán trạng thái đã lưu.</summary>
public class DatQuyenKetQuaDto
{
    public string RoleName { get; set; } = default!;
    public string PermissionName { get; set; } = default!;
    public bool IsGranted { get; set; }
}
