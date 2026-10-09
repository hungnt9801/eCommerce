using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;

namespace eCommerce;

/// <summary>
/// Lớp hằng số, enum và kiểu dùng chung giữa mọi tầng.
/// Không được tham chiếu EF Core / HTTP ở đây.
///
/// 📌 Phase 0: các module ABP được thêm bằng ABP CLI. Attribute đã được GỘP lại cho dễ đọc.
/// ⚠️ LƯU Ý ĐÃ SỬA: ABP CLI ghi <c>BackgroundJobsDomainSharedModule</c> (THIẾU tiền tố
/// <c>Abp</c>) ⇒ lỗi biên dịch CS0246. Tên đúng là <c>AbpBackgroundJobsDomainSharedModule</c>.
/// Đây là lỗi của CLI 10.7.0, cần kiểm tra lại sau mỗi lần nâng version CLI.
/// </summary>
[DependsOn(
    typeof(AbpDddDomainSharedModule),
    typeof(AbpIdentityDomainSharedModule),
    typeof(AbpPermissionManagementDomainSharedModule),
    typeof(AbpSettingManagementDomainSharedModule),
    typeof(AbpFeatureManagementDomainSharedModule),
    typeof(AbpAuditLoggingDomainSharedModule),
    typeof(AbpBackgroundJobsDomainSharedModule),
    typeof(AbpOpenIddictDomainSharedModule)
)]
public class eCommerceDomainSharedModule : AbpModule
{
}
