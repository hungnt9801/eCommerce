using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Customers;

/// <summary>
/// Module tầng Domain của **Customers** (`CUS-001`).
///
/// 🚨 **RANH GIỚI MODULE (ghi rõ để không ai vi phạm):**
///   • **`Address` KHÔNG thuộc module này** (`CUS-001` **ADR-01**): miền địa chỉ do **`AddressHub`** sở hữu.
///     `Customer` **KHÔNG** có cột `Street`/`Ward`/`Province` — đó đúng **anti-pattern "hai mô hình địa chỉ
///     song song"** đã bị ghi nhận (`docs/business-modules-proposal.md`). ⏳ **Hiện `Address` CHƯA tồn tại**
///     ⇒ sổ địa chỉ (`CustomerAddressLink`) **bị chặn có thật** — xem `cus-001` **§16** của tài liệu.
///   • Module khác **CẤM** đọc DB của Customers (`ADR-12`): chỉ đọc qua `ICustomerDirectory` ở `.Contracts`.
///   • Mọi chuyển trạng thái khách **PHẢI NÉM LỖI** (`ADR-04`, quy ước #11) — *"khách `Blocked` mà vẫn đặt
///     được hàng là lỗ hổng kinh doanh"*.
///   • **PII** (email/SĐT): chỉ được đưa vào log qua `PiiMasker` (`ADR-11`).
/// </summary>
[DependsOn(typeof(AbpDddDomainModule), typeof(eCommerce.Kernel.eCommerceKernelModule))]
public class CustomersDomainModule : AbpModule
{
}
