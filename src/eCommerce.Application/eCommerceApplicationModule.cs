using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;

namespace eCommerce;

/// <summary>
/// Tầng application: điều phối use case, gọi domain + repository, publish event.
/// Không chứa logic nghiệp vụ cốt lõi (nằm ở Domain) và không truy cập DbContext trực tiếp.
///
/// 📌 QUYẾT ĐỊNH: KHÔNG dùng AutoMapper.
///   • Chỉ cần map một số DTO đơn giản ⇒ viết tay rõ ràng hơn và ít rủi ro hơn.
///   • Volo.Abp.AutoMapper kéo theo AutoMapper 14.0.0, bản này có cảnh báo bảo mật
///     NU1903 (GHSA-rvv3-g6hj-g44x) trong lần restore đầu tiên.
///   • Nếu sau này cần map phức tạp, cân nhắc Mapperly (source generator, không reflection).
///
/// 📌 Phase 0: các module ABP được thêm bằng ABP CLI; attribute đã được gộp lại.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainModule),
    typeof(eCommerceApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    // 🚨 `SAL-005` — nơi nhận đơn sàn (`MarketplaceOrderIntakeHandler`) cần `IIdempotencyGuard`
    // của module Messaging (`XC-002`). Thiếu dòng này ⇒ handler không resolve được ⇒ đơn mất im lặng.
    typeof(Messaging.MessagingDomainModule),
    typeof(Tax.TaxDomainModule)
)]
[DependsOn(typeof(AbpAccountApplicationModule))]
    public class eCommerceApplicationModule : AbpModule
{
    /// <summary>
    /// Đăng ký job đếm SKU chưa phân loại thuế (`tax_unclassified_skus`).
    ///
    /// ⚠️ ĐÂY LÀ CHỖ DỄ SAI NHẤT (đã gặp 2 lần trong dự án): worker phải được **đăng ký tường minh**
    /// thì mới chạy. Nếu chỉ có class mà không có dòng dưới đây thì **KHÔNG có lỗi build**, app vẫn
    /// khởi động bình thường, nhưng job **im lặng không bao giờ chạy** — và metric sẽ mãi mãi là 0.
    /// </summary>
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<Tax.TaxUnclassifiedSkusWorker>();
    }
}
