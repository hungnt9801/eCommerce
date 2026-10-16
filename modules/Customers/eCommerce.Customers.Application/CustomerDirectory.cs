using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **HIỆN THỰC HỢP ĐỒNG ĐỌC SNAPSHOT KHÁCH CHO MODULE KHÁC** (`CUS-001` **ADR-12**).
///
/// **VÌ SAO TÁCH KHỎI `CustomerAppService` (đã đo được, không phải suy đoán):**
/// phiên bản đầu để `CustomerAppService` **hiện thực luôn** `ICustomerDirectory`. Hệ quả **đo bằng swagger thật**:
/// ba method đọc (`TimTheoIdAsync`, `TimTheoTaiKhoanAsync`, `ChoPhepGiaoDichAsync`) bị ABP sinh thành
/// **`POST`** (mặc định của conventional controller), trong khi chúng **chỉ đọc** —
/// `POST /api/app/customer/tim-theo-id/{id}` là **sai ngữ nghĩa HTTP** (proxy/CDN/browser coi `POST` là
/// **không idempotent** ⇒ có thể **không cache**, và công cụ vận hành không dám gọi lại).
/// 📌 Tách ra lớp **nội bộ** (`ITransientDependency`, **KHÔNG** phải `IApplicationService`) ⇒ ABP
/// **không** sinh endpoint cho nó ⇒ không còn ba route `POST`-đọc-nhầm-ngữ-nghĩa.
///
/// 🔑 **VÌ SAO KHÔNG CHUYỂN SANG `[HttpGet]` TRÊN APP SERVICE:** `ICustomerDirectory` được thiết kế cho
/// **module khác gọi TRONG CÙNG tiến trình** (`ADR-12`: *"module → app là phụ thuộc ngược, cấm"* —
/// nên hợp đồng nằm ở `.Contracts` và được gọi như một interface, **không** qua HTTP).
/// ⇒ Đây **không** phải mặt API; nó là **cổng nội bộ có kiểm soát**. Trộn hai vai trò vào một lớp là
/// mầm của *"một lớp phục vụ hai mục đích khác nhau"* — và đã lộ ra ngay ở lần đo đầu tiên.
///
/// 🔒 **CHỈ ĐỌC** (không có `Set*`): module khác **không** được đổi trạng thái khách — đó là việc của
/// người vận hành qua `CustomerAppService` (có quyền + có audit).
/// </summary>
public class CustomerDirectory : ICustomerDirectory, ITransientDependency
{
    private readonly IRepository<Customer, Guid> _khachRepository;
    private readonly ILogger<CustomerDirectory> _logger;

    public CustomerDirectory(IRepository<Customer, Guid> khachRepository, ILogger<CustomerDirectory> logger)
    {
        _khachRepository = khachRepository;
        _logger = logger;
    }

    public async Task<CustomerDto?> TimTheoIdAsync(Guid customerId)
    {
        var khach = await _khachRepository.FindAsync(customerId);

        return khach is null ? null : ChuyenDto(khach);
    }

    public async Task<CustomerDto?> TimTheoTaiKhoanAsync(Guid identityUserId)
    {
        if (identityUserId == Guid.Empty)
        {
            return null;
        }

        var khach = await _khachRepository.FirstOrDefaultAsync(x => x.IdentityUserId == identityUserId);

        return khach is null ? null : ChuyenDto(khach);
    }

    /// <summary>
    /// **Khách có được phép giao dịch không** (`ADR-04`).
    /// 🔒 **`customerId == null` ⇒ `true`** — khách vãng lai là **hạng nhất** (`ADR-03`): chặn ở đây là
    /// **chặn bán cho người chưa từng tương tác**, phá luồng đặt hàng.
    /// ⚠️ Khách **có** Id nhưng **không tìm thấy** (đã xoá mềm / id sai) ⇒ **`false`** + log `Warning`:
    /// 📌 *"không tìm thấy"* **khác hẳn** *"không có khách"*. Im lặng `true` sẽ để đơn hàng gắn một `CustomerId`
    /// **không tồn tại** — lịch sử sau này **không tra được**.
    /// 🔑 Trả `bool` (**không ném**) để đường bán hàng dùng được ở **điều kiện** thay vì phải bọc `try/catch`.
    /// </summary>
    public async Task<bool> ChoPhepGiaoDichAsync(Guid? customerId)
    {
        if (customerId is null)
        {
            return true;
        }

        var khach = await _khachRepository.FindAsync(customerId.Value);

        if (khach is null)
        {
            _logger.LogWarning(
                "KHÁCH KHÔNG TÌM THẤY khi kiểm quyền giao dịch: id={CustomerId}. Trả `false` (fail-closed) — " +
                "nếu id này đến từ webhook thì hồ sơ khách chưa được tạo hoặc đã bị xoá mềm. [CUS-KHONG-TIM-THAY]",
                customerId.Value);

            return false;
        }

        return khach.DungDuoc;
    }

    /// <summary>Chuyển entity ⇒ DTO. 🔒 Dùng **chung** với app service để hai đường đọc **không** lệch nhau.</summary>
    private static CustomerDto ChuyenDto(Customer k) => CustomerDtoTuEntity.Tu(k);
}

/// <summary>
/// **MỘT chỗ chuyển entity `Customer` ⇒ `CustomerDto`** — dùng chung cho app service **và** hợp đồng đọc.
///
/// 📌 **VÌ SAO TÁCH RA (không chép hai bản):** hai đường đọc mà mỗi đường tự map ⇒ thêm một trường mới là
/// **hai chỗ phải sửa**, và chỗ quên sẽ cho ra DTO **thiếu trường** ở một trong hai đường —
/// **không lỗi biên dịch**, chỉ là API trả thiếu dữ liệu (đúng họ lỗi "hai nguồn sự thật").
/// </summary>
internal static class CustomerDtoTuEntity
{
    internal static CustomerDto Tu(Customer k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        Email = k.Email,
        Phone = k.Phone,
        IdentityUserId = k.IdentityUserId,
        Status = k.Status.ToString(),
        DungDuoc = k.DungDuoc,
        LaKhachVangLai = k.IdentityUserId is null,
        CustomerGroupId = k.CustomerGroupId,
        LifetimeValueAmount = k.LifetimeValueAmount,
        LifetimeValueCurrency = k.LifetimeValueCurrency,
        CreationTime = k.CreationTime
    };
}
