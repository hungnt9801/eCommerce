using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Customers;

/// <summary>
/// 🚨 **NHÓM KHÁCH — HỢP ĐỒNG API** (`CUS-001` `ADR-09`, nợ **§16.4 #2** đóng ở `S3`).
///
/// 🔑 **VÌ SAO NHÓM KHÁCH LÀ BẢNG DỮ LIỆU, KHÔNG PHẢI `enum`:** nhóm là **đầu vào của bảng giá**
/// (`PRC-001` Q5) ⇒ nó **đổi theo chương trình kinh doanh**. Hard-code `Retail/Wholesale/Vip` nghĩa là
/// mỗi lần thêm một nhóm phải **release lại phần mềm** — cùng lý do `XC-006` **D1** đã chốt cho miền thuế.
/// 📌 Vì vậy nó cũng cần **đường ghi** (`POST`/`PUT`): một bảng không có API thì thực chất vẫn là
/// *"hằng số trong DB"* — người vận hành phải `INSERT` tay, và 🔒 mọi thay đổi **không có vết kiểm toán**
/// ở tầng ứng dụng.
///
/// ⚠️ **`Code` BẤT BIẾN sau khi tạo:** `CustomerGroup.Code` có setter `protected` ⇒ đổi mã là **không thể**
/// qua API. 📌 Chủ ý: mã nhóm là thứ **bảng giá tham chiếu** (`PRC-*`); đổi mã của một nhóm đang được dùng
/// sẽ làm giá cũ trỏ vào một **mã không còn tồn tại** — âm thầm, không lỗi biên dịch.
/// </summary>
public interface ICustomerGroupAppService : IApplicationService
{
    /// <summary>
    /// Danh sách nhóm khách (có **phân trang**) — 📌 trả `totalCount` để UI nói được *"có bao nhiêu nhóm"*
    /// thay vì để người dùng đoán theo số dòng đang thấy.
    /// </summary>
    Task<PagedResultDto<CustomerGroupDto>> GetDanhSachAsync(int skipCount = 0, int maxResultCount = 100);

    /// <summary>Tạo nhóm mới. **NÉM** `eCommerce:Customers:0008` nếu **mã** đã tồn tại trong tenant.</summary>
    Task<CustomerGroupDto> TaoNhomAsync(TaoNhomKhachInput input);

    /// <summary>
    /// Đổi **tên/mô tả** của nhóm (🔒 **không** đổi được `Code`). **NÉM** `eCommerce:Customers:0001` nếu
    /// không tìm thấy — 📌 im lặng trả `null` sẽ khiến UI hiện *"đã lưu"* trong khi **không có gì được lưu**.
    /// </summary>
    Task<CustomerGroupDto> CapNhatNhomAsync(Guid id, CapNhatNhomKhachInput input);
}

/// <summary>Một nhóm khách trả ra ngoài.</summary>
public class CustomerGroupDto
{
    public Guid Id { get; set; }

    /// <summary>Mã nhóm (`RETAIL`, `WHOLESALE`, `VIP`…) — **bất biến** sau khi tạo.</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>
    /// 🔑 **SỐ KHÁCH ĐANG Ở NHÓM NÀY** — 📌 có mặt vì một lý do **vận hành**: không có nó thì người quản trị
    /// **không biết** một nhóm đang được dùng hay rỗng, và 🔒 *"xoá/đổi nhóm"* trở thành thao tác **mù**
    /// (nhóm rỗng thì vô hại, nhóm có 5.000 khách thì ảnh hưởng giá của 5.000 người).
    /// </summary>
    public int SoKhach { get; set; }
}

/// <summary>Tạo nhóm khách mới (`ADR-09`).</summary>
public class TaoNhomKhachInput
{
    /// <summary>Mã nhóm — **bắt buộc**, tự chuẩn hoá **CHỮ HOA** ở entity (`CustomerGroup`).</summary>
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    /// <summary>Điều kiện vào nhóm (*"vì sao khách ở nhóm này"*) — 📌 để người sau không phải đoán.</summary>
    [StringLength(1024)]
    public string? Description { get; set; }
}

/// <summary>Đổi tên/mô tả nhóm (⚠️ **không** có `Code` — xem ghi chú ở <see cref="ICustomerGroupAppService"/>).</summary>
public class CapNhatNhomKhachInput
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    [StringLength(1024)]
    public string? Description { get; set; }
}
