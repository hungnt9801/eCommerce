using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.PaymentHub;

/// <summary>
/// Module **tầng Application.Contracts** của `PaymentHub` (`SAL-006`).
/// 📌 Trước 16/10/2026 module này **không tồn tại** ⇒ `PaymentTransaction`/`IPaymentProvider` chỉ sống
/// trong module, **không** có DTO/hợp đồng nào cho tầng ngoài (nợ #2 của `sal-006` §16.5).
/// 📌 `AbpDddApplicationContractsModule` nằm ở `Volo.Abp.Ddd.Application.Contracts` (**không** phải `Volo.Abp.Application`).
/// </summary>
[DependsOn(
    typeof(PaymentHubDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class PaymentHubApplicationContractsModule : AbpModule
{
}
