using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.SettingManagement;

namespace eCommerce;

/// <summary>
/// Tầng domain của ứng dụng: entity, domain service, repository interface, enum nghiệp vụ.
///
/// ⚠️ QUY TẮC KIẾN TRÚC (rút ra từ các nền tảng đã khảo sát):
///  1. Domain KHÔNG được tham chiếu EF Core, HTTP, hay SDK của sàn TMĐT.
///  2. Mọi trường TIỀN TỆ dùng <c>decimal</c>, KHÔNG dùng <c>double</c>
///     (GrandNode 2 dùng double cho tiền — đây là lỗi chúng ta không lặp lại).
///  3. Tồn kho là append-only ledger; không UPDATE số lượng trực tiếp.
///  4. Mọi aggregate có multi-tenant đều implement <see cref="Volo.Abp.MultiTenancy.IMultiTenant"/>.
///
/// 📌 Phase 0 — module ABP do ABP CLI thêm (attribute đã gộp lại cho dễ đọc).
/// ⚠️ BUG CỦA CLI 10.7.0: nó ghi <c>BackgroundJobsDomainModule</c> (THIẾU tiền tố <c>Abp</c>)
/// ⇒ CS0246. Tên đúng: <c>AbpBackgroundJobsDomainModule</c>. Kiểm tra lại sau mỗi lần nâng CLI.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpPermissionManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpOpenIddictDomainModule)
)]
public class eCommerceDomainModule : AbpModule
{
}
