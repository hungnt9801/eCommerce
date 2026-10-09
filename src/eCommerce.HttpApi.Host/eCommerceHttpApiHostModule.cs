using eCommerce.EntityFrameworkCore;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;

namespace eCommerce;

/// <summary>
/// Host của ứng dụng (modular monolith).
///
/// ⚠️ Đây là skeleton API-only: CHƯA có UI/auth module.
/// Khi có mạng, thêm bằng ABP CLI (mỗi lệnh tự cấu hình DbContext + UI):
///     abp add-module Volo.Abp.Identity          # user/role/permission
///     abp add-module Volo.Abp.OpenIddict        # auth server của ABP 10 (thay IdentityServer)
///     abp add-module Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite   # UI login (bản Lite miễn phí)
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpDistributedLockingModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(eCommerceEntityFrameworkCoreModule),
    typeof(eCommerceApplicationModule),
    typeof(ChannelHub.ChannelHubApplicationModule),
    typeof(PaymentHub.PaymentHubApplicationModule)
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
        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var redisConfiguration = configuration["Redis:Configuration"] ?? "127.0.0.1:6379";
            var connection = ConnectionMultiplexer.Connect(redisConfiguration);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });

        // Cấu hình ChannelHub từ section "ChannelHub".
        Configure<ChannelHub.ChannelHubOptions>(configuration.GetSection("ChannelHub"));
        Configure<PaymentHub.Payments.PaymentHubOptions>(configuration.GetSection("PaymentHub"));

        // Auto API controllers: mọi class implements IApplicationService tự thành REST endpoint.
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(eCommerceApplicationModule).Assembly);
            options.ConventionalControllers.Create(typeof(ChannelHub.ChannelHubApplicationModule).Assembly);
        });

        // ⚠️ CHỈ DÙNG CHO PoC: bỏ qua xác thực để test API ngay.
        // Thay bằng Identity + OpenIddict trước khi lên production.
        context.Services.AddAlwaysAllowAuthorization();

        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "eCommerce API",
                Version = "v1",
                Description = "Modular monolith: Commerce core + ChannelHub + PaymentHub"
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
