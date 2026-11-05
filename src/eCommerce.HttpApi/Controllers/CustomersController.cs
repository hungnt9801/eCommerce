using eCommerce.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// 🆕 **`WP-07`** — **KHÁCH HÀNG**, bề mặt `/api/v1/customers` cho Web Admin
/// (`07 §WP-07`: *"FE: DS khách 5 tab phân khúc, chi tiết 5 tab"*).
///
/// 🔐 **Quyền tách theo VIỆC** (`eCommerceConsts.Permissions.Customers`):
/// <list type="bullet">
///   <item>`GET` ⇒ `Customers.Default` (xem hồ sơ);</item>
///   <item>`POST /customers` · `PUT …/segment` · `POST …/care` ⇒ `Customers.Manage`
///     (tạo/sửa hồ sơ, ghi biên bản chăm sóc);</item>
///   <item>`POST/PUT …/customer-levels` ⇒ `Customers.Manage` — 📌 cấp khách là **đầu vào chính sách giá**
///     (dù `WP-07` chưa nối bảng giá), nên không gộp vào quyền đọc.</item>
/// </list>
///
/// 🚨 **MỌI đường đọc đi qua app service (không đụng repository):** app service giữ **phạm vi dữ liệu**,
/// **PII masking** khi log và **một** định nghĩa của *"tiền tích luỹ"*. Controller đọc tắt là bỏ qua cả ba.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(eCommerceConsts.Permissions.Customers.Default)]
public class CustomersController : AbpControllerBase
{
    private readonly ICustomerAppService _khach;
    private readonly ICustomerLevelAppService _cap;

    public CustomersController(ICustomerAppService khach, ICustomerLevelAppService cap)
    {
        _khach = khach;
        _cap = cap;
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // KHÁCH HÀNG
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>DS khách theo **tab phân khúc** (`?tab=all|retail|wholesale|agency|debt`).</summary>
    [HttpGet("customers")]
    public Task<PagedResultDto<CustomerDto>> GetListAsync([FromQuery] CustomerListInput input)
        => _khach.GetDanhSachTheoPhanKhucAsync(input);

    /// <summary>Bộ đếm của **5 tab** — cùng định nghĩa với danh sách (`D-41`).</summary>
    [HttpGet("customers/tabs")]
    public Task<CustomerTabCountsDto> GetTabsAsync([FromQuery] CustomerListInput input)
        => _khach.GetTabCountsAsync(input);

    /// <summary>Chi tiết khách (5 tab của màn chi tiết).</summary>
    [HttpGet("customers/{id:guid}")]
    public Task<CustomerDetailDto> GetAsync(Guid id) => _khach.GetChiTietAsync(id);

    /// <summary>Tạo khách — idempotent theo `IdempotencyKey` (`ADR-05`).</summary>
    [HttpPost("customers")]
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public Task<CustomerDto> CreateAsync([FromBody] TaoKhachInput input) => _khach.TaoKhachAsync(input);

    /// <summary>Cập nhật **phân khách** (loại/cấp/hạn mức nợ/nhân viên/MST/ngày sinh).</summary>
    [HttpPut("customers/{id:guid}/segment")]
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public Task<CustomerDetailDto> UpdateSegmentAsync(Guid id, [FromBody] CapNhatPhanKhachInput input)
        => _khach.CapNhatPhanKhachAsync(id, input);

    /// <summary>Lịch sử chăm sóc (tab "Chăm sóc") — mới nhất trước.</summary>
    [HttpGet("customers/{id:guid}/care")]
    public Task<List<CustomerCareDto>> GetCareAsync(Guid id) => _khach.GetLichSuChamSocAsync(id);

    /// <summary>Ghi **một lần chăm sóc** (append-only).</summary>
    [HttpPost("customers/{id:guid}/care")]
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public Task<CustomerCareDto> CreateCareAsync(Guid id, [FromBody] TaoChamSocInput input)
        => _khach.TaoChamSocAsync(id, input);

    /// <summary>Đối chiếu sổ điểm/tiền tích luỹ/công nợ (tab "Công nợ").</summary>
    [HttpGet("customers/{id:guid}/balance")]
    public Task<DoiChieuSoDuDto> GetBalanceAsync(Guid id) => _khach.GetDoiChieuSoDuAsync(id);

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // CẤP KHÁCH (`CusLevels`)
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [HttpGet("customer-levels")]
    public Task<List<CustomerLevelDto>> GetLevelsAsync([FromQuery] bool? chiHoatDong = null)
        => _cap.GetListAsync(chiHoatDong);

    [HttpPost("customer-levels")]
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public Task<CustomerLevelDto> CreateLevelAsync([FromBody] TaoCapKhachInput input) => _cap.CreateAsync(input);

    [HttpPut("customer-levels/{id:guid}")]
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public Task<CustomerLevelDto> UpdateLevelAsync(Guid id, [FromBody] TaoCapKhachInput input)
        => _cap.UpdateAsync(id, input);

    /// <summary>Bật/tắt cấp — 🔒 **không xoá cứng** (khách đang trỏ `LevelId` vào cấp này).</summary>
    [HttpPost("customer-levels/{id:guid}/active")]
    [Authorize(eCommerceConsts.Permissions.Customers.Manage)]
    public Task<CustomerLevelDto> SetLevelActiveAsync(Guid id, [FromQuery] bool isActive)
    {
        if (id == Guid.Empty)
        {
            throw new BusinessException(CustomerErrorCodes.KhongTimThayCapKhach).WithData("id", id);
        }

        return _cap.SetActiveAsync(id, isActive);
    }
}
