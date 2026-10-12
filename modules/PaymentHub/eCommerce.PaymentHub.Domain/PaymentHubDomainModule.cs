using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.PaymentHub;

/// <summary>
/// Domain của module <b>PaymentHub</b> — lớp trừu tượng hoá cổng thanh toán.
///
/// Vì sao tự làm abstraction thay vì dùng <c>Volo.Payment</c> (module PRO của ABP):
///   • <c>Volo.Payment</c> cần license **ABP Team trở lên**;
///   • nó chỉ có Stripe / PayPal / 2Checkout / PayU / Iyzico / Alipay — KHÔNG có cổng Việt Nam;
///   • mô hình redirect/StartAsync của nó không khớp SePay/Casso (webhook-first, đối soát VietQR).
/// Nếu sau này mua license Team, có thể viết adapter <c>IPaymentGateway</c> gọi vào
/// <see cref="IPaymentProvider"/> của module này — không phải viết lại nghiệp vụ.
/// </summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class PaymentHubDomainModule : AbpModule
{
}
