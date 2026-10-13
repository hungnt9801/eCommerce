using eCommerce.EntityFrameworkCore;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

        // 🚨 C6b — CẤU HÌNH AUTHENTICATION MẶC ĐỊNH (thiếu ⇒ API không token trả 404 body RỖNG thay vì 401):
        //
        // Host vừa là auth server vừa là resource server. Phía resource server BẮT BUỘC phải có một
        // scheme mặc định biết cách validate access token do chính OpenIddict phát ra.
        // Trước đây KHÔNG hề gọi AddAuthentication(...) ⇒ khi [Authorize] thất bại, challenge không có
        // scheme nào để gọi ⇒ sinh **404 body rỗng**.
        // Đã chẩn đoán bằng log `Microsoft.AspNetCore.Routing` = Debug:
        //   "Request matched endpoint 'eCommerce.Channels.ChannelAppService.GetShopsAsync'"
        //   ⇒ route KHỚP, lỗi nằm ở tầng xác thực (không phải routing).
        // Authority/Audience đọc từ appsettings (mục "AuthServer").
        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.Audience = configuration["AuthServer:Audience"];

                // ⚠️ DEV chạy http nên phải tắt; PRODUCTION phải BẬT lại (true) — nếu không sẽ
                // chấp nhận metadata qua kênh không mã hoá.
                options.RequireHttpsMetadata = false;

                // 🚨 C9: host TỰ PHÁT và TỰ VALIDATE token trong CÙNG một tiến trình ⇒ claim `aud`
                // do OpenIddict sinh không nhất thiết khớp giá trị "Audience" cấu hình, và việc
                // kiểm tra audience sẽ làm MỌI token hợp lệ bị từ chối (đã đo: có token vẫn 401).
                // ⇒ CHỈ bỏ kiểm tra audience ở Development. Production giữ mặc định (CÓ kiểm tra).
                if (string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development",
                        StringComparison.OrdinalIgnoreCase))
                {
                    options.TokenValidationParameters.ValidateAudience = false;
                }
            });

        // 🚨 VÁ XC-005 (D8 + §11): health check + correlation id.
        // ABP 10.7 KHÔNG kèm gói HealthChecks nào (đã quét DLL xác nhận) ⇒ dùng AddHealthChecks() có sẵn
        // của ASP.NET Core + một check tự viết ⇒ KHÔNG thêm dependency nào.
        context.Services.AddHealthChecks()
            .AddCheck<HealthChecks.DatabaseHealthCheck>("database");

        // Connection string lấy từ appsettings.json (xem ConnectionStrings:Default).
        Configure<Volo.Abp.Data.AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = configuration.GetConnectionString(eCommerceConsts.DefaultConnectionStringName);
        });

        // Redis dùng cho cả cache và distributed lock.
        Configure<Volo.Abp.Caching.AbpDistributedCacheOptions>(options =>
        {
            // 🚨 C14 — ĐỒNG NHẤT CHỮ THƯỜNG: trước đây cache dùng "eCommerce:" (E HOA) còn lock dùng
            // "ecommerce:" (thường) ⇒ hai quy ước tiền tố trong cùng một hệ thống, rất dễ gõ sai khi
            // vận hành (gỡ cache/khoá treo bằng tay). Nay cả hai dùng "ecommerce:".
            options.KeyPrefix = "ecommerce:";
        });

        Configure<Volo.Abp.DistributedLocking.AbpDistributedLockOptions>(options =>
        {
            // 🚨 BUG ĐÃ SỬA (C12) — BẮT ĐƯỢC BẰNG `redis-cli MONITOR`, 13/10/2026:
            //
            // ABP ghép tiền tố với tên khoá **KHÔNG có dấu phân cách**: `KeyPrefix + name`.
            // Trước đây để `"eCommerce"` và tên khoá của module là `channel:shopee:...`
            // ⇒ khoá thật trong Redis là **`eCommercechannel:shopee:...`** (dính liền, chữ E hoa).
            // Hệ quả: (1) KHÔNG khớp quy ước `ecommerce:<module>:...` của `XC-003` BR-002;
            //          (2) rất dễ gõ sai khi vận hành/gỡ khoá treo bằng tay;
            //          (3) hai tiền tố khác hệ thống có thể dính vào nhau.
            // Nay dùng `"ecommerce:"` (chữ thường + CÓ dấu `:`) ⇒ khoá thật đúng quy ước:
            //   `ecommerce:channel:shopee:SHOP-1:token-refresh`
            options.KeyPrefix = "ecommerce:";
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

            // 🚨 C16 — LỖI IM LẶNG NGHIÊM TRỌNG do các agent viết tài liệu phát hiện (13/10/2026).
            //
            // Nếu CHỈ đăng ký 2 assembly trên thì mọi app service của module MỚI sẽ **KHÔNG có endpoint**
            // ⇒ client nhận **404 body RỖNG** — đúng y triệu chứng đã gặp khi truy lỗi C8
            // (`/api/app/channel/shops` trả 404 dù Swagger liệt kê), và **không có lỗi biên dịch nào**.
            //
            // Từ GĐ0 trở đi sẽ có thêm nhiều module (Invoice, Signing, Shipping, Promotion, Reporting…)
            // ⇒ tự động đăng ký cho MỌI assembly `eCommerce*.Application` đã được nạp vào đồ thị module,
            // thay vì phải nhớ sửa tay ở đây mỗi lần thêm module (quên = 404 im lặng).
            var daDangKy = new HashSet<string>(StringComparer.Ordinal)
            {
                typeof(eCommerceApplicationModule).Assembly.GetName().Name!,
                typeof(ChannelHub.ChannelHubApplicationModule).Assembly.GetName().Name!
            };

            var asmModuleMoi = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Where(a => a.GetName().Name is { } ten
                            && ten.StartsWith("eCommerce", StringComparison.Ordinal)
                            && ten.EndsWith(".Application", StringComparison.Ordinal))
                .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
                .ToList();

            foreach (var asm in asmModuleMoi)
            {
                if (daDangKy.Add(asm.GetName().Name!))
                {
                    options.ConventionalControllers.Create(asm);
                }
            }

            // Ghi ra log để vận hành THẤY được module nào đã được mở endpoint (không còn im lặng).
            Console.WriteLine(
                "[C16] ConventionalControllers đã đăng ký cho {0} assembly: {1}",
                daDangKy.Count, string.Join(", ", daDangKy.OrderBy(x => x, StringComparer.Ordinal)));
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

        // 🚨 VÁ XC-005 (D2/BR-001/BR-002): gắn CorrelationId cho MỌI request và trả lại trong response.
        // Trước đây KHÔNG hề gọi middleware này ⇒ `CorrelationId` chỉ là cột rỗng trong bảng audit của ABP,
        // không nối được log giữa các tầng (đã kiểm chứng: 12/12 chỗ xuất hiện đều nằm trong migration).
        app.UseCorrelationId();

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

        // 🚨 VÁ XC-005 (D8): endpoint health check. Trả 200/503, KHÔNG lộ chi tiết (BR-012).
        // ⚠️ `MapHealthChecks` là extension trên IEndpointRouteBuilder (không phải IApplicationBuilder)
        // ⇒ phải gọi qua UseEndpoints. Đặt TRƯỚC UseConfiguredEndpoints (hàm map controller của ABP).
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health-status");
        });

        app.UseConfiguredEndpoints();
    }
}
