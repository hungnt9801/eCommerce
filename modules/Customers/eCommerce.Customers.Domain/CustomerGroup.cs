using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Customers;

/// <summary>
/// **NHÓM KHÁCH** (`CusGroups`) — `CUS-001` **ADR-09**: là **DỮ LIỆU**, không hard-code.
///
/// **VÌ SAO KHÔNG DÙNG ENUM `Retail/Wholesale/Vip`:** nhóm khách là **đầu vào của bảng giá** (`PRC-001` §15.1 Q5),
/// tức nó **sẽ đổi theo chương trình kinh doanh**. Hard-code ⇒ mỗi lần thêm nhóm phải **release lại phần mềm**
/// (cùng lý do `XC-006` **D1** đã chốt cho miền thuế).
/// </summary>
public class CustomerGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã nhóm (duy nhất trong tenant) — ví dụ `RETAIL`, `WHOLESALE`, `VIP`.</summary>
    public string Code { get; protected set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Mô tả/điều kiện vào nhóm — để người vận hành hiểu **vì sao** khách ở nhóm này.</summary>
    public string? Description { get; set; }

    protected CustomerGroup() { }

    public CustomerGroup(Guid id, string code, string name) : base(id)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(CustomerErrorCodes.ThieuThongTinBatBuoc).WithData("truong", nameof(name));
        }

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }
}
