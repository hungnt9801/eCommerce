using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Customers;

/// <summary>
/// Module **tầng Application** của `Customers` (`CUS-001`).
///
/// 📌 **VÌ SAO CẦN MODULE RIÊNG (bài học C8/C16 đã trả giá):** thiếu lớp module này ⇒ assembly
/// `eCommerce.Customers.Application` **KHÔNG** nằm trong đồ thị module ⇒ ABP **không** quét app service
/// ⇒ **không** tạo endpoint ⇒ API trả **404 body rỗng** dù build/test **xanh**.
/// ⇒ Phải được `[DependsOn]` ở Host/Web (xem `eCommerceHttpApiHostModule`).
/// 📌 `AbpDddApplicationModule` nằm ở `Volo.Abp.Application` (**không** phải `Volo.Abp.Domain`).
/// </summary>
[DependsOn(
    typeof(CustomersDomainModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class CustomersApplicationModule : AbpModule
{
}
