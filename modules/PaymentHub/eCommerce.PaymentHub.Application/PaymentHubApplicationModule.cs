using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.PaymentHub;

/// <summary>
/// Tầng application của PaymentHub: đăng ký các provider cổng thanh toán.
/// Thêm cổng mới (MoMo, payOS, ZaloPay, SePay, Casso) = thêm một class + một dòng ở đây.
/// </summary>
[DependsOn(
    typeof(PaymentHubDomainModule),
    typeof(AbpDddApplicationModule)
)]
public class PaymentHubApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<Payments.VnPayPaymentProvider>();

        // TODO(thêm cổng):
        // context.Services.AddTransient<Payments.MomoPaymentProvider>();
        // context.Services.AddTransient<Payments.PayOsPaymentProvider>();
        // context.Services.AddTransient<Payments.SePayPaymentProvider>();   // webhook-first (đối soát VietQR)
        // context.Services.AddTransient<Payments.CassoPaymentProvider>();   // webhook-first (biến động số dư)
    }
}
