using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;

namespace eCommerce.EntityFrameworkCore;

/// <summary>
/// Đăng ký DbContext + provider PostgreSQL. Đây là project DUY NHẤT biết DBMS cụ thể;
/// tầng Domain/Application không biết gì về PostgreSQL (đổi sang SQL Server/MySQL =
/// chỉ sửa file này + appsettings).
///
/// 📌 Phase 0 — module ABP do ABP CLI thêm; attribute đã gộp lại cho dễ đọc.
/// ⚠️ BUG CỦA CLI 10.7.0: ghi thiếu tiền tố <c>Abp</c> cho module BackgroundJobs
/// (<c>BackgroundJobsEntityFrameworkCoreModule</c>) ⇒ đã sửa thành
/// <c>AbpBackgroundJobsEntityFrameworkCoreModule</c>.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainModule),
    typeof(ChannelHub.EntityFrameworkCore.ChannelHubEntityFrameworkCoreModule),
    typeof(PaymentHub.EntityFrameworkCore.PaymentHubEntityFrameworkCoreModule),
    typeof(Messaging.EntityFrameworkCore.MessagingEntityFrameworkCoreModule),
    typeof(Address.EntityFrameworkCore.AddressEntityFrameworkCoreModule),
    typeof(EInvoice.EntityFrameworkCore.EInvoiceEntityFrameworkCoreModule),
    typeof(Signing.EntityFrameworkCore.SigningEntityFrameworkCoreModule),
    typeof(Pricing.EntityFrameworkCore.PricingEntityFrameworkCoreModule),
    // 🆕 `WP-08` — module REPORTING: mapping read-model báo cáo (`ConfigureReporting`).
    typeof(Reporting.ReportingEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule),
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule)
)]
public class eCommerceEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<eCommerceDbContext>(options =>
        {
            // Tự tạo repository cho entity có DbSet trên eCommerceDbContext
            // ⇒ inject IRepository<T, Guid> ở mọi tầng.
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        // 🚨 BẮT BUỘC — BÀI HỌC KHI CHẠY THẬT:
        // Phải chọn provider cho AbpDbContextOptions, không chỉ cho eCommerceDbContext.
        // Mỗi module ABP (OpenIddict, AuditLogging, Identity…) tự đăng ký DbContext RIÊNG
        // (OpenIddictDbContext, AbpAuditLoggingDbContext…). Thiếu dòng dưới đây thì runtime báo:
        //   "No configuration found for Microsoft.EntityFrameworkCore.DbContext!
        //    Use services.Configure<AbpDbContextOptions>(...) to configure it."
        // ⇒ mọi API chạm DB đều trả 500. Lỗi này KHÔNG lộ khi build/migration, chỉ lộ khi chạy.
        Configure<AbpDbContextOptions>(options =>
        {
            options.UseNpgsql();
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **OUTBOX / INBOX — BẬT TƯỜNG MINH (`XC-002` D2, 24/10/2026).**
        //
        // 🔑 **VÌ SAO PHẢI BẬT TƯỜNG MINH:** nếu thiếu `Outboxes`, `AddToOutboxAsync` trả **false** và ABP
        // **rơi im lặng** về *publish trực tiếp* — hệ thống chạy bình thường, test xanh, nhưng **không có**
        // một dòng nào trong outbox ⇒ mất event khi process chết giữa lúc publish. 📌 Đây đúng loại lỗi
        // *"có trên giấy, không có trong DB"* mà `S10` đã trả giá (4/15 metric).
        //
        // 🔒 `InboxProcessorFailurePolicy.Retry` — **CẤM** `RetryLater`/`Discard` (ABP sẽ
        // `MarkAsDiscardAsync` + chỉ ghi log warning ⇒ event biến mất khỏi tầm quan sát).
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Outboxes.Configure(config => config.UseDbContext<eCommerceDbContext>());
            options.Inboxes.Configure(config => config.UseDbContext<eCommerceDbContext>());
        });

        Configure<AbpEventBusBoxesOptions>(options =>
        {
            // 2 giây = mặc định ABP; ghi ra đây để **đọc được** (không phải giá trị ẩn).
            // 🆕 `probe-outbox.ps1` (24/10/2026) cần **TẠM DỪNG relay** để tạo được một bản ghi outbox
            // "chưa phát" rồi khẳng định: process chết ⇒ bản ghi **còn** ⇒ restart ⇒ **phát lại**. 📌 Núm
            // cấu hình này chỉ để **kiểm chứng**, mặc định vẫn 2 s; production **không** được đặt lớn.
            options.PeriodTimeSpan = TimeSpan.FromSeconds(
                context.Services.GetConfiguration().GetValue("AbpEventBusBoxes:PeriodSeconds", 2));

            options.InboxProcessorFailurePolicy = InboxProcessorFailurePolicy.Retry;
            options.InboxProcessorMaxRetryCount = 10;
            options.WaitTimeToDeleteProcessedInboxEvents = TimeSpan.FromHours(2);
        });
    }

    /// <summary>
    /// 🚨 **Đăng ký worker đếm outbox** (`XC-002` NFR-003 · `messaging_outbox_pending`).
    /// 📌 Thiếu dòng này thì worker **không bao giờ chạy** mà **không có lỗi nào**
    /// (`BackgroundWorkerCoCheTests` chặn bằng phép quét mã nguồn).
    /// </summary>
    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<Workers.OutboxPendingWorker>();
    }
}
