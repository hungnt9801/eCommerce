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
        // 🚨 **ĐĂNG KÝ `IPaymentProvider` — BẮT BUỘC** (trước 16/10/2026 chỉ có `VnPayPaymentProvider` được
        // đăng ký **cụ thể**, **không** có interface ⇒ không chỗ nào lấy được `IPaymentProvider` qua DI,
        // và điều đó **không lộ ra** vì chưa có app service nào dùng).
        //
        // ⚠️ **`HttpClient` KHÔNG đăng ký ở đây** — và đây là **quyết định có lý do**, không phải thiếu sót:
        // `AddHttpClient` nằm trong **shared framework của ứng dụng web** (`Microsoft.Extensions.Http`),
        // KHÔNG có trong `Volo.Abp.Ddd.Application`. Thêm gói đó vào module **nghiệp vụ** là kéo hạ tầng
        // web vào tầng application (đúng thứ `XC-002` D12 cấm).
        // 📌 Cách của dự án (đã có tiền lệ): đăng ký **typed client** ở **Host** —
        // `eCommerceHttpApiHostModule` đã làm y vậy cho `ProvincesOpenApiSource` và `TaxAuthorityClient`.
        // ⇒ `HttpClient?` của provider được Host inject; nếu Host quên, provider trả **lỗi cấu hình có mã**
        // (xem `VnPayPaymentProvider.RefundAsync`) thay vì `NullReferenceException` bị hiểu nhầm thành
        // *"hoàn tiền thất bại"*.
        context.Services.AddTransient<Payments.VnPayPaymentProvider>();
        context.Services.AddTransient<Payments.IPaymentProvider>(sp =>
            sp.GetRequiredService<Payments.VnPayPaymentProvider>());

        // TODO(thêm cổng):
        // context.Services.AddTransient<Payments.MomoPaymentProvider>();
        // context.Services.AddTransient<Payments.PayOsPaymentProvider>();
        // context.Services.AddTransient<Payments.SePayPaymentProvider>();   // webhook-first (đối soát VietQR)
        // context.Services.AddTransient<Payments.CassoPaymentProvider>();   // webhook-first (biến động số dư)
    }
}
