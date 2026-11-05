using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Customers;

/// <summary>
/// 👥 **API CẤU HÌNH TỔ CHỨC**: cửa hàng · phòng ban · nhân viên · **phạm vi dữ liệu theo vai trò** —
/// `WP-07` (`07 §WP-07`: *"`Store`, `Department`, `Staff` (gắn `IdentityUser`), `RoleDataScope` (own/store/all)"*).
///
/// 🔐 **Quyền:** đọc = `Settings.Default`; **ghi** = `Settings.Manage`.
/// 🚨 **VÌ SAO PHẢI TÁCH QUYỀN GHI (không dùng chung `Customers.Manage`):** đổi **phạm vi dữ liệu** là đổi
/// *ai thấy dữ liệu của ai* — một thay đổi **phân quyền**, không phải sửa hồ sơ. 📌 Người trực CSKH cần
/// `Customers.Manage` (chặn khách chây nợ) **không** đương nhiên được nới phạm vi dữ liệu của chính mình
/// (đó là **leo thang quyền**). Cùng nguyên tắc `Orders.Cancel` tách khỏi `Orders.StatusChange`.
///
/// 📌 **Guard chống tự nới quyền:** không cho đổi phạm vi của **vai trò mà chính mình đang giữ** nếu người
/// gọi **không** phải `admin`? — ⏳ Chưa làm ở `WP-07` (cần đọc role của người gọi + so sánh); ghi ở
/// `handover-n8` §5 để phiên sau không tưởng là đã có.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Settings.Default)]
public class StaffAppService : ApplicationService, IStaffAppService
{
    private readonly IRepository<Staff, Guid> _nhanVien;
    private readonly IRepository<Store, Guid> _cuaHang;
    private readonly IRepository<Department, Guid> _phongBan;
    private readonly IRepository<RoleDataScope, Guid> _phamVi;
    private readonly IDataScopeResolver _phamViCuaToi;

    public StaffAppService(
        IRepository<Staff, Guid> nhanVien,
        IRepository<Store, Guid> cuaHang,
        IRepository<Department, Guid> phongBan,
        IRepository<RoleDataScope, Guid> phamVi,
        IDataScopeResolver phamViCuaToi)
    {
        _nhanVien = nhanVien;
        _cuaHang = cuaHang;
        _phongBan = phongBan;
        _phamVi = phamVi;
        _phamViCuaToi = phamViCuaToi;
    }

    // ── NHÂN VIÊN ───────────────────────────────────────────────────────────────────────────────

    public async Task<List<StaffDto>> GetStaffAsync()
    {
        var ds = await _nhanVien.GetListAsync();
        var cuaHang = (await _cuaHang.GetListAsync()).ToDictionary(x => x.Id);
        var phongBan = (await _phongBan.GetListAsync()).ToDictionary(x => x.Id);
        var phamVi = (await _phamVi.GetListAsync()).ToDictionary(x => x.RoleName, StringComparer.OrdinalIgnoreCase);

        return ds
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => ToDto(x, cuaHang, phongBan, phamVi))
            .ToList();
    }

    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<StaffDto> CreateStaffAsync(TaoNhanVienInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        await KiemTrungMaNhanVienAsync(input.Code.Trim(), null);
        await KiemTrungTaiKhoanAsync(input.IdentityUserId, null);

        var nv = new Staff(
            GuidGenerator.Create(), input.Code, input.Name, input.IdentityUserId,
            input.StoreId, input.DepartmentId, input.RoleName);

        nv.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        nv.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        if (!input.IsActive)
        {
            nv.Tat();
        }

        await _nhanVien.InsertAsync(nv, autoSave: true);

        Logger.LogInformation("ĐÃ TẠO NHÂN VIÊN: {Code} · vai trò {VaiTro} · cửa hàng {CuaHang}. [WP07-NHAN-VIEN]",
            nv.Code, nv.RoleName, nv.StoreId);

        return await DungDtoAsync(nv);
    }

    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<StaffDto> UpdateStaffAsync(Guid id, TaoNhanVienInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var nv = await LayNhanVienAsync(id);

        await KiemTrungMaNhanVienAsync(input.Code.Trim(), id);
        await KiemTrungTaiKhoanAsync(input.IdentityUserId, id);

        nv.DatMa(input.Code);
        nv.DatTen(input.Name);
        nv.GanTaiKhoan(input.IdentityUserId);
        nv.DatToChuc(input.StoreId, input.DepartmentId);
        nv.DatVaiTro(input.RoleName);
        nv.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        nv.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        if (input.IsActive)
        {
            nv.Bat();
        }
        else
        {
            nv.Tat();
        }

        await _nhanVien.UpdateAsync(nv, autoSave: true);

        return await DungDtoAsync(nv);
    }

    // ── CỬA HÀNG ────────────────────────────────────────────────────────────────────────────────

    public async Task<List<StoreDto>> GetStoresAsync()
    {
        var ds = await _cuaHang.GetListAsync();

        return ds.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => new StoreDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            WarehouseId = x.WarehouseId,
            AddressText = x.AddressText,
            Phone = x.Phone,
            IsActive = x.IsActive
        }).ToList();
    }

    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<StoreDto> CreateStoreAsync(TaoCuaHangInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var daCo = await _cuaHang.FirstOrDefaultAsync(x => x.Code == input.Code.Trim());

        if (daCo is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCauHinhDaTonTai)
                .WithData("code", input.Code).WithData("loai", "cua-hang").WithData("id", daCo.Id);
        }

        var store = new Store(GuidGenerator.Create(), input.Code, input.Name, input.WarehouseId)
        {
            AddressText = string.IsNullOrWhiteSpace(input.AddressText) ? null : input.AddressText.Trim(),
            Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim()
        };

        if (!input.IsActive)
        {
            store.Tat();
        }

        await _cuaHang.InsertAsync(store, autoSave: true);

        return (await GetStoresAsync()).First(x => x.Id == store.Id);
    }

    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<StoreDto> UpdateStoreAsync(Guid id, TaoCuaHangInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var store = await _cuaHang.FindAsync(id)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayCauHinh)
                .WithData("id", id).WithData("loai", "cua-hang");

        var trung = await _cuaHang.FirstOrDefaultAsync(x => x.Code == input.Code.Trim() && x.Id != id);

        if (trung is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCauHinhDaTonTai)
                .WithData("code", input.Code).WithData("loai", "cua-hang").WithData("id", trung.Id);
        }

        store.DatMa(input.Code);
        store.DatTen(input.Name);
        store.GanKho(input.WarehouseId);
        store.AddressText = string.IsNullOrWhiteSpace(input.AddressText) ? null : input.AddressText.Trim();
        store.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();

        if (input.IsActive)
        {
            store.Bat();
        }
        else
        {
            store.Tat();
        }

        await _cuaHang.UpdateAsync(store, autoSave: true);

        return (await GetStoresAsync()).First(x => x.Id == store.Id);
    }

    // ── PHÒNG BAN ───────────────────────────────────────────────────────────────────────────────

    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        var ds = await _phongBan.GetListAsync();

        return ds.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => new DepartmentDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            StoreId = x.StoreId,
            ParentId = x.ParentId,
            IsActive = x.IsActive
        }).ToList();
    }

    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<DepartmentDto> CreateDepartmentAsync(TaoPhongBanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var daCo = await _phongBan.FirstOrDefaultAsync(x => x.Code == input.Code.Trim());

        if (daCo is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCauHinhDaTonTai)
                .WithData("code", input.Code).WithData("loai", "phong-ban").WithData("id", daCo.Id);
        }

        var pb = new Department(GuidGenerator.Create(), input.Code, input.Name, input.StoreId, input.ParentId);

        if (!input.IsActive)
        {
            pb.Tat();
        }

        await _phongBan.InsertAsync(pb, autoSave: true);

        return (await GetDepartmentsAsync()).First(x => x.Id == pb.Id);
    }

    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, TaoPhongBanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var pb = await _phongBan.FindAsync(id)
            ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayCauHinh)
                .WithData("id", id).WithData("loai", "phong-ban");

        var trung = await _phongBan.FirstOrDefaultAsync(x => x.Code == input.Code.Trim() && x.Id != id);

        if (trung is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCauHinhDaTonTai)
                .WithData("code", input.Code).WithData("loai", "phong-ban").WithData("id", trung.Id);
        }

        pb.DatMa(input.Code);
        pb.DatTen(input.Name);
        pb.DatCuaHang(input.StoreId);
        pb.DatCha(input.ParentId);

        if (input.IsActive)
        {
            pb.Bat();
        }
        else
        {
            pb.Tat();
        }

        await _phongBan.UpdateAsync(pb, autoSave: true);

        return (await GetDepartmentsAsync()).First(x => x.Id == pb.Id);
    }

    // ── PHẠM VI DỮ LIỆU ─────────────────────────────────────────────────────────────────────────

    /// <summary>DS phạm vi **đã cấu hình** (vai trò nào chưa có dòng ⇒ hiệu lực `All`).</summary>
    public async Task<List<RoleDataScopeDto>> GetRoleDataScopesAsync()
    {
        var ds = await _phamVi.GetListAsync();

        return ds.OrderBy(x => x.RoleName, StringComparer.OrdinalIgnoreCase).Select(x => new RoleDataScopeDto
        {
            Id = x.Id,
            RoleName = x.RoleName,
            Scope = x.Scope.ToString(),
            ScopeLabel = NhanPhamVi(x.Scope)
        }).ToList();
    }

    /// <summary>
    /// **ĐẶT PHẠM VI CHO MỘT VAI TRÒ** — tạo mới hoặc cập nhật dòng của vai trò đó.
    /// 🚨 **`Own`/`Store` áp dụng NGAY ở lần gọi API kế tiếp** (phạm vi đọc từ DB, không từ token) —
    /// 📌 đó là lý do `RoleDataScope` không nằm trong claim (xem chú thích entity).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Settings.Manage)]
    public async Task<RoleDataScopeDto> SetRoleDataScopeAsync(DatPhamViDuLieuInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ten = input.RoleName.Trim();
        var dong = await _phamVi.FirstOrDefaultAsync(x => x.RoleName == ten);

        if (dong is null)
        {
            dong = new RoleDataScope(GuidGenerator.Create(), ten, input.Scope);
            await _phamVi.InsertAsync(dong, autoSave: true);
        }
        else
        {
            dong.DatPhamVi(input.Scope);
            await _phamVi.UpdateAsync(dong, autoSave: true);
        }

        // 🔒 `Warning` (không `Information`): đổi phạm vi dữ liệu là **sự kiện phân quyền** — cần vết.
        Logger.LogWarning("ĐÃ ĐỔI PHẠM VI DỮ LIỆU: vai trò {VaiTro} ⇒ {PhamVi} (bởi {NguoiDoi}). [WP07-PHAM-VI]",
            dong.RoleName, dong.Scope, CurrentUser.UserName);

        return new RoleDataScopeDto
        {
            Id = dong.Id,
            RoleName = dong.RoleName,
            Scope = dong.Scope.ToString(),
            ScopeLabel = NhanPhamVi(dong.Scope)
        };
    }

    /// <summary>
    /// **PHẠM VI HIỆU LỰC CỦA CHÍNH TÔI** — 🔑 để FE hiện một dòng *"Bạn đang xem: dữ liệu của bạn"*
    /// thay vì để người dùng **tự đoán** vì sao danh sách ngắn hơn đồng nghiệp.
    /// </summary>
    public async Task<PhamViCuaToiDto> GetPhamViCuaToiAsync()
    {
        var hienTai = await _phamViCuaToi.LayAsync();

        return new PhamViCuaToiDto
        {
            PhamVi = hienTai.PhamVi.ToString(),
            LyDo = hienTai.LyDo,
            NhanVienId = hienTai.NhanVienId,
            KhoCuaHangId = hienTai.KhoCuaHangId
        };
    }

    // ── Nội bộ ──────────────────────────────────────────────────────────────────────────────────

    private async Task<Staff> LayNhanVienAsync(Guid id)
        => await _nhanVien.FindAsync(id)
           ?? throw new BusinessException(CustomerErrorCodes.KhongTimThayCauHinh)
               .WithData("id", id).WithData("loai", "nhan-vien");

    private async Task KiemTrungMaNhanVienAsync(string code, Guid? boQuaId)
    {
        var trung = await _nhanVien.FirstOrDefaultAsync(x => x.Code == code && (boQuaId == null || x.Id != boQuaId));

        if (trung is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCauHinhDaTonTai)
                .WithData("code", code).WithData("loai", "nhan-vien").WithData("id", trung.Id);
        }
    }

    /// <summary>
    /// 🚨 **MỘT TÀI KHOẢN ⇒ MỘT NHÂN VIÊN** — kiểm **trước** để trả mã nghiệp vụ (`0023`) thay vì để
    /// unique index nổ `23505` rồi client thấy *"hệ thống hỏng"*. 📌 DB vẫn là hàng rào cuối (đồng thời thật).
    /// </summary>
    private async Task KiemTrungTaiKhoanAsync(Guid? identityUserId, Guid? boQuaId)
    {
        if (identityUserId is not { } taiKhoan)
        {
            return;
        }

        var trung = await _nhanVien.FirstOrDefaultAsync(
            x => x.IdentityUserId == taiKhoan && (boQuaId == null || x.Id != boQuaId));

        if (trung is not null)
        {
            throw new BusinessException(CustomerErrorCodes.MaCauHinhDaTonTai)
                .WithData("loai", "tai-khoan")
                .WithData("identityUserId", taiKhoan)
                .WithData("nhanVienDangDung", trung.Id)
                .WithData("lyDo", "Tài khoản này ĐÃ gắn với một nhân viên khác.");
        }
    }

    private async Task<StaffDto> DungDtoAsync(Staff nv)
    {
        var cuaHang = (await _cuaHang.GetListAsync()).ToDictionary(x => x.Id);
        var phongBan = (await _phongBan.GetListAsync()).ToDictionary(x => x.Id);
        var phamVi = (await _phamVi.GetListAsync()).ToDictionary(x => x.RoleName, StringComparer.OrdinalIgnoreCase);

        return ToDto(nv, cuaHang, phongBan, phamVi);
    }

    private static StaffDto ToDto(
        Staff x,
        IReadOnlyDictionary<Guid, Store> cuaHang,
        IReadOnlyDictionary<Guid, Department> phongBan,
        IReadOnlyDictionary<string, RoleDataScope> phamVi)
    {
        // 📌 Phạm vi **hiệu lực** của nhân viên = phạm vi của vai trò chính; chưa cấu hình ⇒ `All` (đúng
        // luật của `DataScopeResolver` — hai bên dùng **cùng** một luật mặc định).
        var scope = x.RoleName is { } vaiTro && phamVi.TryGetValue(vaiTro, out var dong)
            ? dong.Scope
            : PhamViDuLieu.All;

        return new StaffDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            IdentityUserId = x.IdentityUserId,
            StoreId = x.StoreId,
            StoreName = x.StoreId is { } storeId && cuaHang.TryGetValue(storeId, out var store) ? store.Name : null,
            DepartmentId = x.DepartmentId,
            DepartmentName = x.DepartmentId is { } pbId && phongBan.TryGetValue(pbId, out var pb) ? pb.Name : null,
            RoleName = x.RoleName,
            PhamViHieuLuc = scope.ToString(),
            Phone = x.Phone,
            Note = x.Note,
            IsActive = x.IsActive
        };
    }

    private static string NhanPhamVi(PhamViDuLieu scope) => scope switch
    {
        PhamViDuLieu.Store => "Dữ liệu của cửa hàng",
        PhamViDuLieu.Own => "Chỉ dữ liệu của tôi",
        _ => "Tất cả"
    };
}
