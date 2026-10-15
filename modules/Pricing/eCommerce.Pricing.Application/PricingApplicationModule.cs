using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Pricing;

/// <summary>
/// Module tầng Application của **Pricing** (`PRC-001` §6.1).
/// 📌 App service ở đây được Host **tự động** mở endpoint qua `ConventionalControllers` (quét mọi assembly
/// `eCommerce*.Application` — bài học **C16**: chỉ đăng ký thủ công ⇒ endpoint **404 im lặng**).
/// </summary>
[DependsOn(
    typeof(PricingDomainModule),
    typeof(PricingApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class PricingApplicationModule : AbpModule
{
}
