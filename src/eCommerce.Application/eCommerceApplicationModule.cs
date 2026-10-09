using Volo.Abp.Application;
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
    typeof(AbpSettingManagementApplicationModule)
)]
[DependsOn(typeof(AbpAccountApplicationModule))]
    public class eCommerceApplicationModule : AbpModule
{
}
