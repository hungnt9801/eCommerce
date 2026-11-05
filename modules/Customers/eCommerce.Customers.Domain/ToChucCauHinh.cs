using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Customers;

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// `WP-07` — CẤU HÌNH TỔ CHỨC: CỬA HÀNG · PHÒNG BAN · NHÂN VIÊN · PHẠM VI DỮ LIỆU THEO VAI TRÒ.
//
// 🔑 **VÌ SAO NẰM Ở MODULE `Customers`:** `07` §2 ghi rõ nhánh `Customers/ ← mở rộng (WP-07)` — ba bảng
// này phục vụ **cùng một câu hỏi**: *"ai được thấy dữ liệu nào"* (nhân viên CSKH/bán hàng là chủ thể của
// dữ liệu khách). 📌 Tách module mới sẽ phải thêm 4 project + đăng ký DI + cấu hình EF **chỉ** để chứa
// bốn bảng danh mục — và tạo thêm một đường phụ thuộc cho `Pos`/`Sales` phải đi qua.
//
// 🔒 **MỌI BẢNG ĐỀU CÓ `TenantId`** (`D-01`: thiết kế đa doanh nghiệp từ đầu, chưa bật UI).
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 🏬 **CỬA HÀNG** (`SetStores`) — `WP-07`. Là đơn vị tổ chức mà nhân viên thuộc về.
///
/// 🔑 **`WarehouseId` là MẤU CHỐT của phạm vi `Store`:** đơn hàng/hoá đơn chỉ có `WarehouseId` (không có
/// `StoreId`) — nên muốn lọc *"dữ liệu của cửa hàng tôi"* thì **cửa hàng phải biết kho của nó**.
/// 📌 Gắn ở đây (không thêm cột `StoreId` vào `InvWarehouses`) để **không** phải sửa module `Inventory`
/// — bảng kho đã có ổn định riêng và migration của nó không thuộc `WP-07`.
/// ⚠️ `WarehouseId = null` ⇒ cửa hàng **chưa gắn kho** ⇒ phạm vi `Store` sẽ **không thấy dữ liệu nào**
/// (đúng: không biết kho thì không lọc được) và UI phải nói rõ lý do.
/// </summary>
public class Store : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiMaToiDa = 32;
    public const int DoDaiTenToiDa = 128;

    public Guid? TenantId { get; protected set; }

    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    /// <summary>Kho mặc định của cửa hàng — dùng cho phạm vi dữ liệu `Store` (xem chú thích lớp).</summary>
    public Guid? WarehouseId { get; protected set; }

    /// <summary>Địa chỉ dạng chữ (chỉ để hiển thị/in) — 📌 **không** phải `Address` của module `Address`:
    /// đây là *"địa chỉ của một cửa hàng"*, không phải địa chỉ giao hàng của khách (`ADR-01`).</summary>
    public string? AddressText { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; protected set; } = true;

    protected Store() { }

    public Store(Guid id, string code, string name, Guid? warehouseId = null) : base(id)
    {
        DatMa(code);
        DatTen(name);
        WarehouseId = warehouseId;
    }

    public void DatMa(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(code));
        }

        var sach = code.Trim();

        if (sach.Length > DoDaiMaToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code)).WithData("toiDa", DoDaiMaToiDa);
        }

        Code = sach;
    }

    public void DatTen(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(name));
        }

        var sach = name.Trim();

        if (sach.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name)).WithData("toiDa", DoDaiTenToiDa);
        }

        Name = sach;
    }

    public void GanKho(Guid? warehouseId) => WarehouseId = warehouseId;

    public void Bat() => IsActive = true;

    public void Tat() => IsActive = false;
}

/// <summary>
/// 🗂️ **PHÒNG BAN** (`SetDepartments`) — `WP-07`. Cây **một cấp cha** (`ParentId`) để nhóm nhân viên.
/// 📌 Không làm cây nhiều tầng có kiểm chu trình như `Product.ParentId`: phòng ban **không** tham gia
/// tính toán nghiệp vụ (chỉ để hiển thị/lọc), nên một chu trình ở đây **không** làm sai số nào —
/// và thêm luật chống chu trình là **phạm vi không có trong WP-07**.
/// </summary>
public class Department : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiMaToiDa = 32;
    public const int DoDaiTenToiDa = 128;

    public Guid? TenantId { get; protected set; }

    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    public Guid? StoreId { get; protected set; }

    public Guid? ParentId { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    protected Department() { }

    public Department(Guid id, string code, string name, Guid? storeId = null, Guid? parentId = null) : base(id)
    {
        DatMa(code);
        DatTen(name);
        StoreId = storeId;
        ParentId = parentId;
    }

    public void DatMa(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(code));
        }

        var sach = code.Trim();

        if (sach.Length > DoDaiMaToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code)).WithData("toiDa", DoDaiMaToiDa);
        }

        Code = sach;
    }

    public void DatTen(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(name));
        }

        var sach = name.Trim();

        if (sach.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name)).WithData("toiDa", DoDaiTenToiDa);
        }

        Name = sach;
    }

    public void DatCha(Guid? parentId)
    {
        if (parentId is { } cha && cha == Id)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(parentId))
                .WithData("lyDo", "Phòng ban KHÔNG thể là cha của chính nó.");
        }

        ParentId = parentId;
    }

    public void Bat() => IsActive = true;

    public void Tat() => IsActive = false;

    /// <summary>Gắn/gỡ **cửa hàng** của phòng ban (thuộc tổ chức nào).</summary>
    public void DatCuaHang(Guid? storeId) => StoreId = storeId;
}

/// <summary>
/// 👤 **NHÂN VIÊN** (`SetStaffs`) — `WP-07` (`07 §WP-07`: *"`Staff` (gắn `IdentityUser`)"*).
///
/// 🚨 **HAI ĐỊNH DANH, HAI MỤC ĐÍCH — KHÔNG gộp:**
/// <list type="bullet">
///   <item>`IdentityUserId` — **tài khoản đăng nhập** (ABP Identity). 🔑 Dùng để ánh xạ *"người đang gọi API"*
///     sang *"nhân viên nào"* ⇒ tính được phạm vi dữ liệu `Own`/`Store`. `null` = **chưa cấp tài khoản**
///     (nhân viên mới, hoặc người chỉ xuất hiện trên chứng từ) — 📌 vẫn phải lưu được (`NhanVienChuaCoTaiKhoan`).</item>
///   <item>`Code` — **mã nhân viên trên chứng từ** (in trên hoá đơn, đối chiếu lương). Ổn định, không đổi
///     khi đổi tên/đổi tài khoản.</item>
/// </list>
/// 🔒 Unique `(TenantId, IdentityUserId)` **có bộ lọc `IS NOT NULL`**: một tài khoản chỉ ứng với **một**
/// nhân viên — nếu không, *"đơn của tôi"* có **hai** nghĩa và phạm vi dữ liệu lọc sai **im lặng**.
/// </summary>
public class Staff : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiMaToiDa = 32;
    public const int DoDaiTenToiDa = 128;

    public Guid? TenantId { get; protected set; }

    public string Code { get; protected set; } = default!;

    public string Name { get; protected set; } = default!;

    /// <summary>Tài khoản đăng nhập (ABP `AbpUsers.Id`) — `null` = chưa cấp.</summary>
    public Guid? IdentityUserId { get; protected set; }

    public Guid? StoreId { get; protected set; }

    public Guid? DepartmentId { get; protected set; }

    /// <summary>
    /// **Vai trò chính** (tên role ABP, ví dụ `admin`, `sales`) — 🔑 đây là **khoá** để tra
    /// `SetRoleDataScopes` (phạm vi dữ liệu). `null` = chưa gán ⇒ phạm vi mặc định `All` (xem `D-46`).
    /// 📌 Một tài khoản có thể có nhiều role ở ABP; `WP-07` **chỉ** dùng role chính cho phạm vi dữ liệu —
    /// ghi rõ để phiên sau không tưởng là "đã hỗ trợ đa role".
    /// </summary>
    public string? RoleName { get; protected set; }

    public string? Phone { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; protected set; } = true;

    protected Staff() { }

    public Staff(Guid id, string code, string name, Guid? identityUserId = null, Guid? storeId = null,
        Guid? departmentId = null, string? roleName = null) : base(id)
    {
        DatMa(code);
        DatTen(name);
        GanTaiKhoan(identityUserId);
        StoreId = storeId;
        DepartmentId = departmentId;
        DatVaiTro(roleName);
    }

    public void DatMa(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(code));
        }

        var sach = code.Trim();

        if (sach.Length > DoDaiMaToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code)).WithData("toiDa", DoDaiMaToiDa);
        }

        Code = sach;
    }

    public void DatTen(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(name));
        }

        var sach = name.Trim();

        if (sach.Length > DoDaiTenToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(name)).WithData("toiDa", DoDaiTenToiDa);
        }

        Name = sach;
    }

    /// <summary>Gắn/gỡ tài khoản đăng nhập. 🔒 Gỡ (`null`) là hợp lệ — nhân viên nghỉ việc.</summary>
    public void GanTaiKhoan(Guid? identityUserId)
    {
        if (identityUserId is { } id && id == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(identityUserId))
                .WithData("lyDo", "`IdentityUserId` KHÔNG được là GUID rỗng (null = chưa cấp tài khoản).");
        }

        IdentityUserId = identityUserId;
    }

    public void DatVaiTro(string? roleName)
        => RoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName.Trim();

    public void DatToChuc(Guid? storeId, Guid? departmentId)
    {
        StoreId = storeId;
        DepartmentId = departmentId;
    }

    public void Bat() => IsActive = true;

    public void Tat() => IsActive = false;
}

/// <summary>
/// 🔐 **PHẠM VI DỮ LIỆU THEO VAI TRÒ** (`SetRoleDataScopes`) — `WP-07` (`07 §WP-07`:
/// *"`RoleDataScope` (own/store/all)"*; DoD: *"nhân viên bật 'chỉ xem dữ liệu của mình' chỉ thấy đơn mình tạo"*).
///
/// 🚨 **VÌ SAO KHÔNG ĐỂ CẤU HÌNH NÀY TRONG CLAIM/TOKEN:** token do client giữ; một phạm vi nằm trong token
/// thì **đổi quyền phải chờ token hết hạn**, và token cũ vẫn mang phạm vi rộng ⇒ *"đã siết quyền mà vẫn
/// thấy dữ liệu"* — lỗi bảo mật im lặng. 📌 Đọc từ **DB** (có cache của ABP ở tầng đọc nếu cần) là nguồn
/// sự thật duy nhất.
///
/// 🔒 **Unique `(TenantId, RoleName)`**: một vai trò chỉ có **một** phạm vi. Nhiều dòng cho cùng role ⇒
/// phép gộp phải "đoán" và người vận hành thấy hai cấu hình mâu thuẫn mà không biết cái nào đang áp.
/// </summary>
public class RoleDataScope : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiVaiTroToiDa = 256;

    public Guid? TenantId { get; protected set; }

    /// <summary>Tên vai trò ABP (`AbpRoles.Name`).</summary>
    public string RoleName { get; protected set; } = default!;

    public PhamViDuLieu Scope { get; protected set; } = PhamViDuLieu.All;

    protected RoleDataScope() { }

    public RoleDataScope(Guid id, string roleName, PhamViDuLieu scope) : base(id)
    {
        DatVaiTro(roleName);
        Scope = scope;
    }

    public void DatVaiTro(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(roleName));
        }

        var sach = roleName.Trim();

        if (sach.Length > DoDaiVaiTroToiDa)
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(roleName)).WithData("toiDa", DoDaiVaiTroToiDa);
        }

        RoleName = sach;
    }

    public void DatPhamVi(PhamViDuLieu scope) => Scope = scope;
}
