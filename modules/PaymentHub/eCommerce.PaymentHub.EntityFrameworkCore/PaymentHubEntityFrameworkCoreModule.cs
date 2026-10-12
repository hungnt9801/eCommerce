using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.PaymentHub.EntityFrameworkCore;

/// <summary>Cung cấp mapping EF Core cho entity của PaymentHub (xem hàm ConfigurePaymentHub).</summary>
[DependsOn(
    typeof(PaymentHubDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class PaymentHubEntityFrameworkCoreModule : AbpModule
{
}
