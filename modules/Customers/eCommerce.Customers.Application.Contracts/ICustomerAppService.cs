using Volo.Abp.Application.Dtos;

namespace eCommerce.Customers;

/// <summary>
/// **HỢP ĐỒNG API KHÁCH HÀNG** (`WP-07`) — bề mặt `/api/v1/customers`.
///
/// 🔑 **VÌ SAO CẦN INTERFACE (không inject lớp cụ thể vào controller):** `eCommerce.HttpApi` chỉ tham chiếu
/// tầng **Application.Contracts** của module (đúng luật `03 §2`). 📌 Đây cũng là lý do `IRetailBillAppService`
/// của `WP-06` tồn tại — để controller không phải kéo **toàn bộ** tầng Application (kèm EF) vào project HTTP.
/// </summary>
public interface ICustomerAppService
{
    Task<PagedResultDto<CustomerDto>> GetDanhSachTheoPhanKhucAsync(CustomerListInput input);

    Task<CustomerTabCountsDto> GetTabCountsAsync(CustomerListInput input);

    Task<CustomerDetailDto> GetChiTietAsync(Guid customerId);

    Task<CustomerDto> TaoKhachAsync(TaoKhachInput input);

    Task<CustomerDetailDto> CapNhatPhanKhachAsync(Guid customerId, CapNhatPhanKhachInput input);

    Task<List<CustomerCareDto>> GetLichSuChamSocAsync(Guid customerId);

    Task<CustomerCareDto> TaoChamSocAsync(Guid customerId, TaoChamSocInput input);

    Task<DoiChieuSoDuDto> GetDoiChieuSoDuAsync(Guid customerId);
}

/// <summary>**HỢP ĐỒNG API CẤP KHÁCH** — `/api/v1/customer-levels`.</summary>
public interface ICustomerLevelAppService
{
    Task<List<CustomerLevelDto>> GetListAsync(bool? chiHoatDong = null);

    Task<CustomerLevelDto> GetAsync(Guid id);

    Task<CustomerLevelDto> CreateAsync(TaoCapKhachInput input);

    Task<CustomerLevelDto> UpdateAsync(Guid id, TaoCapKhachInput input);

    Task<CustomerLevelDto> SetActiveAsync(Guid id, bool isActive);
}

/// <summary>**HỢP ĐỒNG API CẤU HÌNH TỔ CHỨC** — `/api/v1/settings/**` (`WP-07`).</summary>
public interface IStaffAppService
{
    Task<List<StaffDto>> GetStaffAsync();

    Task<StaffDto> CreateStaffAsync(TaoNhanVienInput input);

    Task<StaffDto> UpdateStaffAsync(Guid id, TaoNhanVienInput input);

    Task<List<StoreDto>> GetStoresAsync();

    Task<StoreDto> CreateStoreAsync(TaoCuaHangInput input);

    Task<StoreDto> UpdateStoreAsync(Guid id, TaoCuaHangInput input);

    Task<List<DepartmentDto>> GetDepartmentsAsync();

    Task<DepartmentDto> CreateDepartmentAsync(TaoPhongBanInput input);

    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, TaoPhongBanInput input);

    Task<List<RoleDataScopeDto>> GetRoleDataScopesAsync();

    Task<RoleDataScopeDto> SetRoleDataScopeAsync(DatPhamViDuLieuInput input);

    /// <summary>Phạm vi **hiệu lực của chính người gọi** — để UI hiện *"bạn đang xem dữ liệu của bạn"*.</summary>
    Task<PhamViCuaToiDto> GetPhamViCuaToiAsync();
}

/// <summary>
/// **HỢP ĐỒNG MA TRẬN QUYỀN** — `/api/v1/settings/permissions`.
/// 📌 Hiện thực ở **tầng app chính** (`eCommerce.Application`) vì cần `Identity` + `PermissionManagement`;
/// hợp đồng đặt ở đây để controller (project HTTP) không phải tham chiếu tầng Application của app chính.
/// </summary>
public interface IPermissionMatrixAppService
{
    Task<PermissionMatrixDto> GetMatrixAsync();

    Task<DatQuyenKetQuaDto> SetAsync(DatQuyenInput input);
}
