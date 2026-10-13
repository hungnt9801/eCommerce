using eCommerce.EntityFrameworkCore;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Server.AspNetCore;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;

namespace eCommerce;

/// <summary>
/// Host của ứng dụng (modular monolith, API-only).
///
/// 🔐 PHASE 0 — xác thực: host này vừa là **auth server** (OpenIddict: /connect/token)
/// vừa là **resource server** ([Authorize] trên các app service).
///
/// ⚠️ CHƯA có UI đăng nhập (solution không có project Web) ⇒ lấy token bằng cách gọi
/// <c>POST /connect/token</c> với grant <c>password</c> hoặc <c>client_credentials</c>
/// (xem README mục "Lấy token để gọi API"). UI login/Admin là việc của giai đoạn P1.
///
/// Muốn có UI: tạo project Web (xem docs/abp-modules-plan.md) rồi thêm
/// <c>Volo.Account</c> + theme (<c>Volo.BasicTheme</c> hoặc <c>Volo.Abp.LeptonXTheme.Lite</c>)
/// trỏ vào project đó — ABP CLI chỉ thêm được phần UI khi có project Web.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpOpenIddictAspNetCoreModule),
    // 🚨 C6a: đăng ký SignInManager<IdentityUser> — THIẾU thì password grant chết.
    typeof(Volo.Abp.Identity.AspNetCore.AbpIdentityAspNetCoreModule),
    // 🚨 C6b: đặt default challenge scheme — THIẾU thì API không token trả 500 thay vì 401.
    typeof(Volo.Abp.AspNetCore.Authentication.JwtBearer.AbpAspNetCoreAuthenticationJwtBearerModule),
    typeof(eCommerceEntityFrameworkCoreModule),
    typeof(eCommerceApplicationModule),
    typeof(ChannelHub.ChannelHubApplicationModule),
    typeof(PaymentHub.PaymentHubApplicationModule),
    typeof(Tax.EntityFrameworkCore.TaxEntityFrameworkCoreModule)
)]
public class eCommerceHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<Volo.Abp.Uow.AbpUnitOfWorkDefaultOptions>(options =>
        {
            // Mọi thao tác ghi đi qua Unit of Work của ABP ⇒ transaction tự động ở tầng EF Core.
            options.TransactionBehavior = Volo.Abp.Uow.UnitOfWorkTransactionBehavior.Auto;
        });

        // Connection string lấy từ appsettings.json (xem ConnectionStrings:Default).
        Configure<Volo.Abp.Data.AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = configuration.GetConnectionString(eCommerceConsts.DefaultConnectionStringName);
        });

        // Redis dùng cho cả cache và distributed lock.
        Configure<Volo.Abp.Caching.AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = "eCommerce:";
        });

        Configure<Volo.Abp.DistributedLocking.AbpDistributedLockOptions>(options =>
        {
            options.KeyPrefix = "eCommerce";
        });

        // 🔒 Provider lock thật (Redis). Bắt buộc khi chạy nhiều instance:
        // nếu chỉ có lock in-process, hai instance vẫn có thể refresh token cùng lúc → mất token.
        //
        // ⚠️ BÀI HỌC KHI CHẠY THẬT: worker dọn token của OpenIddict resolve IDistributedLockProvider
        // ngay lúc khởi động. Nếu Redis chưa lên, ConnectionMultiplexer.Connect() NÉM exception
        // ⇒ cả ứng dụng không start được. Vì vậy đặt AbortOnConnectFail = false để multiplexer
        // tiếp tục thử lại ở nền thay vì làm sập ứng dụng (đúng cả khi Redis restart lúc đang chạy).
        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var redisConfiguration = configuration["Redis:Configuration"] ?? "127.0.0.1:6379";
            var redisOptions = ConfigurationOptions.Parse(redisConfiguration);
            redisOptions.AbortOnConnectFail = false;

            var connection = ConnectionMultiplexer.Connect(redisOptions);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });

        // Cấu hình ChannelHub / PaymentHub từ appsettings.json.
        Configure<ChannelHub.ChannelHubOptions>(configuration.GetSection("ChannelHub"));
        Configure<PaymentHub.Payments.PaymentHubOptions>(configuration.GetSection("PaymentHub"));

        // Auto API controllers: mọi class implements IApplicationService tự thành REST endpoint.
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(eCommerceApplicationModule).Assembly);
            options.ConventionalControllers.Create(typeof(ChannelHub.ChannelHubApplicationModule).Assembly);
        });

        // ⚠️ OpenIddict mặc định BẮT BUỘC HTTPS cho mọi endpoint.
        // Development: cho phép HTTP để test bằng localhost (đặt trong appsettings.Development.json).
        // Production: để false (mặc định) và chạy sau HTTPS.
        var disableTransportSecurity = configuration.GetValue("AuthServer:DisableTransportSecurityRequirement", false);
        Configure<OpenIddictServerAspNetCoreOptions>(options =>
        {
            options.DisableTransportSecurityRequirement = disableTransportSecurity;
        });

        // ⚠️ BẢO MẬT: KHÔNG còn AddAlwaysAllowAuthorization().
        // Mọi API có [Authorize] giờ được bảo vệ bởi OpenIddict + permission của ABP.
        // Ở Development, ABP tự tạo chứng chỉ ký/mã hoá dev
        // (AbpOpenIddictAspNetCoreOptions.AddDevelopmentEncryptionAndSigningCertificate = true).
        // ⚠️ Production BẮT BUỘC cấu hình chứng chỉ thật và tắt tuỳ chọn dev này.

        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "eCommerce API",
                Version = "v1",
                Description = "Modular monolith: Commerce core + ChannelHub + PaymentHub (auth qua OpenIddict)"
            });
            options.DocInclusionPredicate((_, _) => true);
            options.CustomSchemaIds(type => type.FullName);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseCors();

        // 🔐 Bắt buộc: xác thực TRƯỚC phân quyền. OpenIddict chèn handler vào
        // AuthenticationMiddleware nên /connect/token được xử lý ở đây.
        app.UseAuthentication();
        app.UseAuthorization();

        // Lưu ý: dynamic background worker của ChannelHub được supervisor tự đăng ký lúc chạy,
        // không cần cấu hình worker nào ở đây.
        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "eCommerce API");
        });

        app.UseConfiguredEndpoints();
    }
}
