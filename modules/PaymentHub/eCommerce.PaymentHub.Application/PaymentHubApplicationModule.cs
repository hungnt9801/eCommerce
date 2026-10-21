using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
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
    /// <summary>
    /// 🚨 **CỔNG ĐANG DÙNG — MỘT ĐIỂM QUYẾT ĐỊNH DUY NHẤT** (`PAY-001` §7.7.2).
    /// Giá trị hợp lệ: `"VnPay"` (mặc định) hoặc `"Fake"` (chỉ `Development`).
    /// </summary>
    private const string KhoaCauHinhProvider = "PaymentHub:Provider";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var cauHinh = context.Services.GetConfiguration();

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

        var chon = (cauHinh[KhoaCauHinhProvider] ?? string.Empty).Trim();

        // 📌 Rỗng = **VnPay** (mặc định an toàn): một cấu hình thiếu **không** được phép biến thành cổng giả.
        if (chon.Length == 0 || chon.Equals("VnPay", StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddTransient<Payments.IPaymentProvider>(
                sp => sp.GetRequiredService<Payments.VnPayPaymentProvider>());

            return;
        }

        if (!chon.Equals("Fake", StringComparison.OrdinalIgnoreCase))
        {
            // ⚠️ **Fail-fast, KHÔNG đoán:** một giá trị lạ (kể cả gõ sai `Vnpay ` có dấu cách) mà "rơi về mặc định"
            // thì người vận hành tin rằng mình đã chọn cổng X trong khi hệ thống chạy cổng Y.
            throw new AbpInitializationException(
                $"Giá trị `{KhoaCauHinhProvider}` = `{chon}` KHÔNG hợp lệ. Chỉ nhận `VnPay` (mặc định) hoặc `Fake` (chỉ Development).");
        }

        // ── CỔNG GIẢ LẬP (`S8`) — chỉ đăng ký ở đây; **kiểm môi trường ở `OnApplicationInitialization`** ──
        context.Services.AddTransient<Providers.FakePaymentProvider>();
        context.Services.AddTransient<Payments.IPaymentProvider>(
            sp => sp.GetRequiredService<Providers.FakePaymentProvider>());

        // TODO(thêm cổng):
        // context.Services.AddTransient<Payments.MomoPaymentProvider>();
        // context.Services.AddTransient<Payments.PayOsPaymentProvider>();
        // context.Services.AddTransient<Payments.SePayPaymentProvider>();   // webhook-first (đối soát VietQR)
        // context.Services.AddTransient<Payments.CassoPaymentProvider>();   // webhook-first (biến động số dư)
    }

    /// <summary>
    /// 🚨 **CHỐT AN TOÀN CỦA CỔNG GIẢ LẬP — NÉM LÚC KHỞI ĐỘNG, KHÔNG PHẢI LÚC DÙNG** (`PAY-001` §7.7.2).
    ///
    /// 🔑 **Vì sao phải kiểm ở đây chứ không trong `ConfigureServices`:** quyết định đăng ký cần **cấu hình**,
    /// còn quyết định *"môi trường này có được phép dùng cổng giả hay không"* cần **`IHostEnvironment`** — thứ
    /// chỉ có ở giai đoạn khởi tạo ứng dụng. 📌 Và **vì sao phải ném lúc khởi động**: nếu để mặc định hoặc chỉ
    /// ghi log, một lần deploy quên đổi cấu hình ⇒ **production nhận thanh toán giả**, sổ sách sai **mà không
    /// có lỗi nào** cho tới lúc đối soát tiền thật (đúng §7.7.4 #3).
    /// </summary>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var cauHinh = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var chon = (cauHinh[KhoaCauHinhProvider] ?? string.Empty).Trim();

        if (!chon.Equals("Fake", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var moiTruong = context.ServiceProvider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();

        // ⚠️ Nếu KHÔNG lấy được môi trường (ví dụ một host console tự dựng DI tối thiểu) thì **KHÔNG kết luận
        // "an toàn"** cũng không bỏ qua im lặng: ném để buộc người cấu hình sửa — 📌 một chốt an toàn
        // "không kiểm được thì cho qua" là chốt an toàn giả.
        if (moiTruong is null
            || !string.Equals(moiTruong.EnvironmentName,
                Microsoft.Extensions.Hosting.Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new AbpInitializationException(
                $"`{KhoaCauHinhProvider} = Fake` chỉ được phép ở môi trường **Development** "
                + $"(đang là `{moiTruong?.EnvironmentName ?? "(không xác định được)"}`). "
                + "🔒 Cổng giả lập ở môi trường thật nghĩa là ĐƠN ĐƯỢC ĐÁNH DẤU ĐÃ TRẢ TIỀN mà không có đồng nào vào sổ.");
        }
    }
}
