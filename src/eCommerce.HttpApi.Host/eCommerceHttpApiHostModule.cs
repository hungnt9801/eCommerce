using System.Reflection;
using eCommerce.EntityFrameworkCore;
using eCommerce.Messaging.EventCoverage;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    typeof(Tax.EntityFrameworkCore.TaxEntityFrameworkCoreModule),
    // 🚨 BẮT BUỘC: thiếu dòng này thì assembly Address.Application KHÔNG nằm trong đồ thị module
    // ⇒ ABP không nạp, không quét app service, không tạo endpoint ⇒ API trả 404 body rỗng (bài học C8/C16).
    // 🚨 BAT BUOC: thieu dong nay thi cac type trong shared/eCommerce.Kernel KHONG duoc dang ky DI.
    typeof(eCommerce.Kernel.eCommerceKernelModule),
    typeof(Address.Application.AddressApplicationModule),
    typeof(EInvoice.Application.EInvoiceApplicationModule),
    typeof(Signing.Application.SigningApplicationModule),
    typeof(Pricing.PricingApplicationModule)
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

                // 🚨🚨 F-403 — SỬA LỖI "MỌI API GẮN QUYỀN TRẢ 403 DÙ ĐÃ CẤP QUYỀN" (đo được 14/10/2026).
                //
                // `JwtBearer` mặc định BẬT `MapInboundClaims` ⇒ nó ĐỔI TÊN claim theo khuôn WS-Federation cũ:
                //     `role` → `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`
                //     `sub`  → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`
                //     `email`→ `.../claims/emailaddress`   ·   `unique_name` → `.../claims/name`
                //
                // Nhưng **ABP đọc tên NGẮN của OIDC** (`AbpClaimTypes`: `sub`, `role`, `email`,
                // `preferred_username`). Hệ quả đo được bằng log thật của `ChanDoanTuChoiQuyenHandler`:
                //     `claim vai trò : .../claims/role=admin`        ← token CÓ vai trò
                //     `ABP current user: id='' · IsAuthenticated=False · Roles=[(RỖNG)]`   ← ABP KHÔNG thấy
                //     `quyền: 'eCommerce.Channels.Sync' · định nghĩa: ✅ · cấp: ❌`
                // ⇒ `RolePermissionValueProvider` không có role nào để tra ⇒ **403**, dù DB đã cấp quyền.
                //
                // 🔎 Vì sao `[Authorize]` trần VẪN chạy (endpoint chỉ-cần-đăng-nhập trả 200): ASP.NET chỉ cần
                // `Identity.IsAuthenticated`, thứ KHÔNG phụ thuộc tên claim. Chỉ **máy phân quyền của ABP**
                // (đọc `sub`/`role` theo tên) mới hỏng — nên lỗi này **rất khó đoán** nếu không đo.
                //
                // ⚠️ Đã kiểm chứng toàn bộ mã nguồn: **không** chỗ nào dùng `ClaimTypes.*` hay `Identity.Name`
                // ⇒ tắt ánh xạ KHÔNG làm hỏng chỗ nào khác (xem ma trận 401/403/200 sau khi sửa).
                options.MapInboundClaims = false;

                // Giữ `Identity.Name` có giá trị (trước đây nhờ `unique_name` được ánh xạ sang `.../claims/name`).
                // Đặt TƯỜNG MINH để việc tắt ánh xạ không âm thầm làm `Name` rỗng — ABP dùng `preferred_username`.
                options.TokenValidationParameters.NameClaimType = "preferred_username";

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

        // 🚨 MỤC (2) — HẠ TẦNG HTTP CHO CƠ QUAN THUẾ (EINV-002 phát hiện F3: trước đây repo
        // KHÔNG có IHttpClientFactory). `AddHttpClient` nằm trong shared framework của ứng dụng web
        // ⇒ KHÔNG cần thêm gói NuGet nào.
        //
        // 📌 BaseUrl đọc từ cấu hình `TaxAuthority:BaseUrl`; hiện chưa có endpoint thật của CQT nên
        // để giá trị mặc định và GHI RÕ là chưa kiểm chứng được.
        // Nguồn danh mục ĐVHC chính thức (ADDR-001). BaseUrl đọc từ cấu hình để đổi môi trường.
        context.Services.AddHttpClient<eCommerce.Address.ProvincesOpenApiSource>(http =>
        {
            var baseUrl = configuration["Address:Sync:BaseUrl"];
            http.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl)
                ? "https://provinces.open-api.vn/"
                : baseUrl);
            http.Timeout = TimeSpan.FromSeconds(60);
        });
        context.Services.AddHttpClient(eCommerce.TaxAuthority.TaxAuthorityClient.TenClientHttp, http =>
        {
            var baseUrl = configuration["TaxAuthority:BaseUrl"];
            http.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:9999/" : baseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        context.Services.AddTransient<eCommerce.TaxAuthority.TaxAuthorityClient>();
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

        // 🚨 VÁ LỖ HỔNG QUAN SÁT (F-403, đo được 14/10/2026): khi request có token hợp lệ nhưng THIẾU QUYỀN,
        // ASP.NET Core trả 403 và KHÔNG ghi một dòng log nào ở phía server ⇒ lỗi "mọi API gắn quyền đều 403"
        // ẩn được rất lâu (chỉ nhìn thấy ở phía client). Handler dưới đây ghi MỘT dòng Warning cho mỗi lần
        // từ chối, kèm dữ kiện chẩn đoán: quyền nào không đạt, quyền đó CÓ ĐƯỢC ĐỊNH NGHĨA hay không
        // (đúng nguyên nhân đã xảy ra thật ở Phase 0 — XC-004 D2/E3), các LOẠI claim đang có, giá trị claim
        // vai trò, và ICURRENTUSER.ROLES (góc nhìn của ABP).
        //
        // ⚠️ Handler này KHÔNG đổi quyết định cho phép/từ chối — nó chỉ ghi nhận rồi chuyển tiếp cho handler
        // mặc định ⇒ mã trạng thái 401/403 giữ nguyên. Việc đó được KIỂM CHỨNG bằng ma trận 401/403/200.
        context.Services.AddSingleton<
            Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
            Authorization.ChanDoanTuChoiQuyenHandler>();

        // Auto API controllers: mọi class implements IApplicationService tự thành REST endpoint.
        // 🚨 **TRẢ NỢ (vòng 29):** map vi phạm ràng buộc DB (`23505`/`23514`/`23503`) thành **lỗi nghiệp vụ**
        // thay vì `500`. Đã ĐO được `500` ở 3 chỗ trước khi làm việc này (ghi sổ trùng · giữ chỗ trùng nguồn ·
        // hai request đồng thời). 📌 Phải đăng ký ở **tầng Host** vì lỗi chỉ xuất hiện lúc `SaveChanges`
        // (sau khi app service đã trả về) — không tầng nào phía trong bắt được.
        Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
        {
            options.Filters.Add<HttpApi.Host.Filters.BoLocViPhamRangBuocDb>();
        });

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

        // 🚨 KIỂM TRA ĐỘ PHỦ SỰ KIỆN (14/10/2026) — biến lỗi IM LẶNG thành lỗi ỒN ÀO.
        //
        // Lỗi thật: ChannelShopSyncer publish ChannelOrderPulledEto cho TỪNG đơn hàng kéo từ
        // Shopee/Lazada, nhưng KHÔNG có handler nào nhận. Đồng bộ vẫn báo THÀNH CÔNG và TIẾN CON TRỎ
        // ĐỒNG BỘ => sàn không gửi lại => đơn hàng MẤT VĨNH VIỄN mà không có lỗi nào được ghi.
        //
        // Vì sao kiểm tra lúc KHỞI ĐỘNG (không phải test): đây là bất biến về CẤU HÌNH DI của ứng dụng
        // đang chạy, không phải về logic — test không nạp cùng bộ module nên không thấy được.
        KiemTraDoPhuSuKien(context);

        // 🚨 KIỂM TRA BẤT BIẾN TÊN CLAIM (F-403, 14/10/2026) — cùng lý do: đây là bất biến về CẤU HÌNH
        // của ứng dụng đang chạy. Bật lại `MapInboundClaims` sẽ làm MỌI API gắn quyền trả 403 mà
        // KHÔNG có lỗi biên dịch và KHÔNG exception — đúng loại lỗi im lặng cần chặn.
        KiemTraBatBienTenClaim(context);

        // 📊 Đo độ trễ request và phát http_request_duration_seconds (XC-005).
        // Đặt sớm để bao được cả thời gian xác thực/phân quyền.
        app.UseMiddleware<Observability.HttpRequestDurationMiddleware>();

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
    /// <summary>
    /// Báo cáo **độ phủ sự kiện** ngay khi khởi động: sự kiện nào được publish mà **không có người nhận**
    /// bị ghi log **Error** ⇒ biến lỗi **im lặng** (mất dữ liệu) thành lỗi **ồn ào ở mọi lần boot**.
    ///
    /// 🚨 Dùng **CHUNG** <see cref="IEventCoverageProbe"/> với quyết định
    /// **fail-closed** trong `ChannelShopSyncer` (lỗi #22). Trước đây chỗ này tự quét riêng ⇒ log lúc boot
    /// và hành vi lúc đồng bộ **có thể lệch nhau**; lệch theo hướng nguy hiểm nghĩa là log nói
    /// *"có người nhận"* trong khi đơn vẫn mất. Nay **một nguồn sự thật duy nhất**.
    ///
    /// ⚠️ Phạm vi quét là các assembly `eCommerce*` **ĐÃ NẠP** ⇒ log ghi rõ **số assembly đã quét** để nếu
    /// phạm vi không đầy đủ thì **nhìn thấy được**, thay vì im lặng cho ra kết quả "sạch" giả tạo
    /// (bài học C16: quét theo "assembly đã nạp" rất dễ thiếu).
    /// </summary>
    private static void KiemTraDoPhuSuKien(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider
            .GetRequiredService<ILogger<eCommerceHttpApiHostModule>>();

        var probe = context.ServiceProvider
            .GetRequiredService<IEventCoverageProbe>();

        var ketQua = probe.KetQua.PhuSong;
        var soAssembly = probe.KetQua.SoAssemblyDaQuet;

        if (ketQua.CoSuKienBiBoQuen)
        {
            logger.LogError(
                "🚨 PHÁT HIỆN {SoLuong} SỰ KIỆN **MANG DỮ LIỆU** KHÔNG CÓ NGƯỜI NHẬN (đã quét {SoAssembly} assembly). " +
                "Sự kiện được publish nhưng không ai xử lý ⇒ DỮ LIỆU SẼ MẤT IM LẶNG.",
                ketQua.SuKienKhongCoNguoiNhan.Count, soAssembly);

            foreach (var ten in ketQua.SuKienKhongCoNguoiNhan)
            {
                logger.LogError("   • {TenSuKien} — KHÔNG có handler nào đăng ký nhận.", ten);
            }
        }

        // 🚨 SỰ KIỆN **THÔNG BÁO** (gắn `INotificationEvent`) không có người nhận ⇒ mức **Warning**, KHÔNG Error.
        // VÌ SAO TÁCH: dữ liệu của loại này **đã nằm trong DB** (ví dụ token mới đã ghi vào `ChnChannelShops`),
        // mất sự kiện chỉ khiến việc *thông báo* không xảy ra. Nếu gộp chung với loại mang dữ liệu thì log
        // boot sẽ có **dương tính giả** ở mọi lần khởi động ⇒ người đọc **bắt đầu bỏ qua** cảnh báo ⇒ và
        // **lỗi thật** (như lỗi #22) sẽ lọt qua. Đó chính là thứ cần tránh.
        if (ketQua.CoSuKienThongBaoBiBoQuen)
        {
            logger.LogWarning(
                "{SoLuong} sự kiện THÔNG BÁO chưa có người nhận (đã quét {SoAssembly} assembly) — KHÔNG mất dữ liệu " +
                "(dữ liệu đã ở trong DB), nhưng nếu bạn MONG ĐỢI ai đó xử lý thì đây là thiếu sót cần bổ sung.",
                ketQua.SuKienThongBaoKhongCoNguoiNhan.Count, soAssembly);

            foreach (var ten in ketQua.SuKienThongBaoKhongCoNguoiNhan)
            {
                logger.LogWarning("   • {TenSuKien} — chưa có handler (là sự kiện thông báo).", ten);
            }
        }

        if (!ketQua.CoSuKienBiBoQuen && !ketQua.CoSuKienThongBaoBiBoQuen)
        {
            logger.LogInformation(
                "Độ phủ sự kiện OK: {SoSuKien} hợp đồng sự kiện, tất cả đều có người nhận (đã quét {SoAssembly} assembly).",
                ketQua.SuKienCoNguoiNhan.Count, soAssembly);
        }
    }

    /// <summary>
    /// 🚨 **Bất biến lúc khởi động cho tên claim (F-403).**
    ///
    /// `JwtBearer` mặc định **ĐỔI TÊN** claim của token sang khuôn WS-Federation cũ
    /// (`role` → `.../claims/role`, `sub` → `.../claims/nameidentifier`), trong khi **ABP đọc tên NGẮN OIDC**
    /// (`AbpClaimTypes`: `sub`, `role`, `email`). Bật lại ánh xạ ⇒ `ICurrentUser` **rỗng** ⇒ máy phân quyền
    /// của ABP từ chối **MỌI** người ⇒ **mọi API gắn quyền trả `403`**, mà:
    ///   • **không** lỗi biên dịch · **không** exception · app vẫn khởi động bình thường,
    ///   • endpoint **chỉ cần đăng nhập** vẫn trả `200` ⇒ tưởng hệ thống vẫn tốt.
    ///
    /// Vì vậy phải kiểm tra **lúc chạy thật** và **la ở mức Error**. Đây không phải chuyện lý thuyết: lỗi này
    /// đã tồn tại thật và được đo ra ngày 14/10/2026 (xem `docs/runtime-verification-report.md` §10).
    /// </summary>
    private static void KiemTraBatBienTenClaim(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider
            .GetRequiredService<ILogger<eCommerceHttpApiHostModule>>();

        try
        {
            var jwt = context.ServiceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<
                    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);

            var loi = new List<string>();

            if (jwt.MapInboundClaims)
            {
                loi.Add(
                    "`MapInboundClaims = true` ⇒ claim `role`/`sub` bị ĐỔI TÊN sang khuôn WS-Federation, " +
                    "trong khi ABP đọc tên NGẮN OIDC ⇒ `ICurrentUser` RỖNG ⇒ MỌI API gắn quyền trả 403.");
            }

            if (!string.Equals(jwt.TokenValidationParameters.NameClaimType, "preferred_username", StringComparison.Ordinal))
            {
                loi.Add(
                    $"`NameClaimType = '{jwt.TokenValidationParameters.NameClaimType}'` (mong đợi " +
                    "'preferred_username') ⇒ `Identity.Name` có thể RỖNG dù token hợp lệ.");
            }

            if (loi.Count > 0)
            {
                foreach (var dong in loi)
                {
                    logger.LogError("🚨 BẤT BIẾN TÊN CLAIM BỊ VI PHẠM: {Dong}", dong);
                }
            }
            else
            {
                logger.LogInformation(
                    "Bất biến tên claim OK: MapInboundClaims = false · NameClaimType = '{NameClaimType}' " +
                    "(khớp tên claim NGẮN mà ABP đọc).",
                    jwt.TokenValidationParameters.NameClaimType);
            }
        }
        catch (Exception ex)
        {
            // Không đọc được cấu hình ⇒ KHÔNG được im lặng bỏ qua (đây là bất biến bảo mật).
            logger.LogError(ex, "Không kiểm tra được bất biến tên claim của JwtBearer.");
        }
    }
}