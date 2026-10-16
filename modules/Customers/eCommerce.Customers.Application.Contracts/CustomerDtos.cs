namespace eCommerce.Customers;

/// <summary>Dữ liệu khách trả ra ngoài (API/UI) — `CUS-001` §6.1.</summary>
public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Email — **PII**. 📌 Vẫn trả ra cho **người có quyền** (đó là quyền *"xem dữ liệu của mình"*
    /// theo Luật 91/2025/QH15), nhưng **cấm** đưa giá trị này vào **log** (`ADR-11`).</summary>
    public string? Email { get; set; }

    /// <summary>SĐT — **PII**, cùng lưu ý như <see cref="Email"/>.</summary>
    public string? Phone { get; set; }

    public Guid? IdentityUserId { get; set; }

    /// <summary>`0 = Inactive` · `1 = Active` · `2 = Blocked` (xem `CustomerStatus`).</summary>
    public string Status { get; set; } = default!;

    /// <summary>`true` ⇒ khách **KHÔNG** bị chặn (đường bán hàng dùng cờ này — `ADR-04`).</summary>
    public bool DungDuoc { get; set; }

    /// <summary>`true` = **khách vãng lai** (chưa gắn tài khoản) — `ADR-03`.</summary>
    public bool LaKhachVangLai { get; set; }

    public Guid? CustomerGroupId { get; set; }
    public decimal? LifetimeValueAmount { get; set; }
    public string? LifetimeValueCurrency { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>Tạo khách mới (idempotent theo <see cref="IdempotencyKey"/>).</summary>
public class TaoKhachInput
{
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// 🔑 **Khoá idempotency** (`ADR-05`) — **BẮT BUỘC** ở biên vào (webhook/queue).
    /// 🚨 Phải sinh từ **dữ liệu nghiệp vụ ỔN ĐỊNH** (ví dụ `"shopee:order:12345"`), **KHÔNG** `Guid.NewGuid()`:
    /// khoá ngẫu nhiên thì **mỗi lần retry là một khách mới** — đúng lỗi `XC-002` **D7**.
    /// 📌 Để trống = **không** dùng idempotency (đường tạo tay của nhân viên, đã có người xác nhận).
    /// </summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Nâng cấp khách vãng lai thành khách có tài khoản (`ADR-07`).</summary>
public class NangCapKhachInput
{
    /// <summary>Id tài khoản ABP Identity (`AbpUsers.Id`) sẽ trở thành **chủ sở hữu** của hồ sơ khách.</summary>
    public Guid IdentityUserId { get; set; }
}

/// <summary>
/// 🚨 **HỢP ĐỒNG ĐỌC SNAPSHOT KHÁCH CHO MODULE KHÁC** — `CUS-001` **ADR-12**.
///
/// **VÌ SAO PHẢI CÓ INTERFACE NÀY (không cho module khác join thẳng bảng):**
/// quy ước **#3** bất di bất dịch của dự án: *"mọi module sở hữu entity của mình; **module → app là phụ thuộc
/// NGƯỢC, cấm**"* (`docs/structure-and-techstack.md`). Nếu `Sales`/`Pricing` join thẳng `CusCustomers` thì:
/// <list type="bullet">
///   <item>đổi **cấu trúc** bảng khách ⇒ **vỡ** những module không liên quan (mà không có lỗi biên dịch);</item>
///   <item>bất biến của khách (ví dụ **che PII**) bị **vượt qua** ở mọi chỗ đọc thẳng.</item>
/// </list>
/// 📌 Tiền lệ thật: `LOY-001` **D8** đã chọn **ETO** thay vì gọi thẳng vì cùng lý do.
///
/// 🚨 **CỐ Ý KHÔNG kế thừa `IApplicationService` (đã ĐO được, không phải suy đoán):** phiên bản đầu để
/// interface này là `IApplicationService` **và** để `CustomerAppService` hiện thực nó ⇒ swagger thật cho ra
/// `POST /api/app/customer/tim-theo-id/{id}` — tức ba method **chỉ đọc** bị ABP sinh thành **`POST`**
/// (mặc định của conventional controller) ⇒ **sai ngữ nghĩa HTTP** (proxy/CDN coi `POST` là **không
/// idempotent** ⇒ không cache, công cụ vận hành không dám gọi lại).
/// ⇒ Nay: interface **thuần hợp đồng nội bộ**; lớp hiện thực là `CustomerDirectory`
/// (`ITransientDependency`, **không** phải application service) ⇒ **không** sinh endpoint.
/// 📌 `ADR-12` nói rõ hợp đồng này được gọi **trong cùng tiến trình** (interface), **không** qua HTTP.
///
/// 🔒 **Hợp đồng này CHỈ ĐỌC** (không có `Set*`): module khác **không** được đổi trạng thái khách —
/// đó là việc của người vận hành qua app service riêng (có quyền + có audit).
/// </summary>
public interface ICustomerDirectory
{
    /// <summary>Tra khách theo Id. Trả `null` nếu không có (hoặc đã **xoá mềm** — `ADR-02`).</summary>
    Task<CustomerDto?> TimTheoIdAsync(Guid customerId);

    /// <summary>Tài khoản đăng nhập này đã gắn với khách nào chưa (`ADR-07`: claim là **một-một**).</summary>
    Task<CustomerDto?> TimTheoTaiKhoanAsync(Guid identityUserId);

    /// <summary>
    /// **Khách có được phép giao dịch không** (`ADR-04`).
    /// 🚨 `customerId` là `Guid?` vì **khách vãng lai là hạng nhất** (`ADR-03`): `null` ⇒ **LUÔN `true`**
    /// (đơn không có khách vẫn hợp lệ). 📌 Trả `bool` (không ném) để đường bán hàng dùng được ở **điều kiện**
    /// thay vì phải bọc `try/catch`.
    /// </summary>
    Task<bool> ChoPhepGiaoDichAsync(Guid? customerId);
}
