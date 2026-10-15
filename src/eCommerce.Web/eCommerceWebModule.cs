using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.Tracing;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace eCommerce.Web;

/// <summary>
/// **Tiến trình UI QUẢN TRỊ** (`GĐ1` §7.0 #2) — MVC/Razor + theme Basic (miễn phí) + màn hình đăng nhập.
///
/// 🎯 QUYẾT ĐỊNH KIẾN TRÚC: Web dùng **CÙNG đồ thị module** với Host và **cùng DB** ⇒ app service được
/// gọi **trong tiến trình**. Nhờ vậy không cần dựng thêm tầng HTTP giữa UI và nghiệp vụ, và UI luôn
/// dùng đúng permission/validation như API.
///
/// 🚨🚨 **HAI BẪY IM LẶNG ĐÃ XỬ LÝ Ở ĐÂY — build xanh KHÔNG chỉ ra:**
///
/// 1. **Background worker/job chạy 2 LẦN** vì đồ thị module giống Host: `ChannelSyncSupervisorWorker`
///    (đồng bộ sàn), `EInvoiceSubmissionWorker` (gửi cơ quan thuế), `AdministrativeUnitSyncWorker`,
///    `TaxUnclassifiedSkusWorker`… Mỗi worker có mặt ở **cả hai tiến trình** ⇒ **tiêu quota gấp đôi**,
///    gọi API bên ngoài gấp đôi, và tranh chấp con trỏ đồng bộ.
///    ⇒ **TẮT TƯỜNG MINH** bằng `AbpBackgroundWorkerOptions.IsEnabled = false` +
///    `AbpBackgroundJobsOptions.IsJobExecutionEnabled = false`.
///    ⚠️ Đây là cấu hình, **không phải logic** ⇒ test thường không thấy; chỉ thấy khi chạy thật.
///
/// 2. **Không có scheme xác thực mặc định** ⇒ mọi `[Authorize]` thất bại trả **`500`** thay vì chuyển
///    hướng về trang đăng nhập (bài học C6b của Host). ⇒ đặt **cookie** làm scheme mặc định.
///
/// 📌 **KHÔNG** thêm Redis cache/distributed lock vào tiến trình này: Web **không chạy worker** nên
/// không cần khoá liên tiến trình, và bỏ Redis ⇒ giảm một phụ thuộc có thể làm sập UI khi Redis chưa lên.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
    typeof(AbpAccountWebModule),
    typeof(EntityFrameworkCore.eCommerceEntityFrameworkCoreModule),
    typeof(eCommerceApplicationModule),
    // 🚨 **`Pricing` PHẢI ĐƯỢC NẠP Ở WEB** (phát hiện vòng 58): `DonHangAppService` — dùng bởi trang mặt tiền đầu tiên
    // `PhanBoController` — có tham số ctor `IPriceCalculationAppService` ⇒ thiếu module này thì DI **KHÔNG dựng được**
    // controller ⇒ **`HTTP 500`**: *"None of the constructors found … Cannot resolve parameter
    // 'eCommerce.Pricing.IPriceCalculationAppService'"*.
    // 📌 **Vì sao Web phải nạp module nghiệp vụ (không chỉ Host):** Web chạy **in-process** ⇒ mọi app service dùng ở
    // trang Web phải **dựng được ngay trong tiến trình Web**. 📌 **Quy tắc cho MỌI trang mặt tiền sau này:**
    // dùng app service của module nào ⇒ **phải nạp module đó ở Web** — và **`eCommerceWebModule` khai CÙNG đồ thị
    // module với Host** (ghi ở doc comment) nhưng `[DependsOn]` **chưa** phản ánh điều đó ⇒ lệch giữa **ý định** và **mã**.
    typeof(eCommerce.Pricing.PricingApplicationModule)
)]
public class eCommerceWebModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        // ── Cùng DB với Host (một nguồn dữ liệu duy nhất).
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default =
                configuration.GetConnectionString(eCommerceConsts.DefaultConnectionStringName);
        });

        // Mọi thao tác ghi đi qua Unit of Work (transaction tự động) — giống Host.
        Configure<Volo.Abp.Uow.AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Auto;
        });

        // ── 🚨 BẪY 1: TẮT worker/job trong tiến trình UI (xem ghi chú ở class).
        Configure<AbpBackgroundWorkerOptions>(options =>
        {
            options.IsEnabled = false;
        });

        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = false;
        });

        // ── 🚨 BẪY 2 — ĐÃ SỬA LẦN 2 (bài học đo được, 15/10/2026): **KHÔNG được tự đặt scheme mặc định**.
        //
        // Lần đầu tôi viết ở đây:
        //     context.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        //                    .AddCookie(o => o.LoginPath = "/Account/Login");
        // với suy nghĩ "thiếu scheme mặc định ⇒ `[Authorize]` trả 500 thay vì chuyển hướng".
        // Build **xanh**, test **xanh**, và **chuyển hướng 302 về /Account/Login trông vẫn đúng**.
        //
        // ✅ ĐÃ ĐO BẰNG ĐỘT BIẾN (không phải suy luận) — cùng một kịch bản đăng nhập thật, chỉ khác dòng trên:
        //     • CÓ  dòng trên ⇒ sau khi POST đăng nhập, URL cuối là `/Account/Login?ReturnUrl=%2F`
        //       ⇒ **vẫn chưa đăng nhập**, trang quản trị KHÔNG render.
        //     • KHÔNG có     ⇒ URL cuối là `/` ⇒ **vào được** trang quản trị (`200`).
        // LÝ DO: module Account/Identity đăng nhập bằng scheme `IdentityConstants.ApplicationScheme`
        // (`"Identity.Application"`), còn dòng trên đã **thay** scheme mặc định thành `"Cookies"`
        // ⇒ `[Authorize]` đi tìm cookie ở **nhầm** scheme ⇒ luôn coi là chưa đăng nhập.
        //
        // ⇒ **ĐÚNG:** để `AbpAccountWebModule`/`AbpIdentityAspNetCoreModule` tự đặt scheme mặc định
        // (nó đặt đúng `IdentityConstants.ApplicationScheme` và `LoginPath = /Account/Login`).
        // Đây là bài học chung: **đừng "sửa" cấu hình xác thực mặc định của ABP** — sai ở đây **không**
        // gây lỗi biên dịch, **không** exception, và **trông vẫn đúng** ở bước chuyển hướng.
        //
        // ⚠️ Nếu sau này cần thêm provider ngoài (Google/OIDC), dùng dạng **bổ sung**:
        //     context.Services.AddAuthentication().AddGoogle(...)
        // — **KHÔNG** truyền scheme mặc định vào `AddAuthentication(...)`.

        // ── KHÔNG cấu hình OpenIddict ở đây: tiến trình UI **không** làm auth server.
        // Auth server DUY NHẤT là `eCommerce.HttpApi.Host` (xem lý do ở `Directory.Packages.props`).
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Theme cần file tĩnh (CSS/JS nhúng).
        app.UseStaticFiles();

        app.UseRouting();

        // Đa ngôn ngữ + gắn CorrelationId TRƯỚC xác thực (để mọi log có mã tương quan).
        app.UseAbpRequestLocalization();
        app.UseCorrelationId();

        // 🔐 BẮT BUỘC: xác thực TRƯỚC phân quyền (OpenIddict chèn handler ở tầng này).
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseConfiguredEndpoints();
    }
}
