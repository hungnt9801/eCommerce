using Volo.Abp.Application;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;

namespace eCommerce;

/// <summary>
/// DTO + interface application service. Đây là hợp đồng công khai (HTTP API, client khác).
/// Không được tham chiếu entity của tầng Domain ra ngoài qua tầng này.
///
/// 📌 Phase 0: các module ABP được thêm bằng ABP CLI; attribute đã được gộp lại.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpIdentityApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationContractsModule),
    typeof(AbpSettingManagementApplicationContractsModule),
    typeof(AbpFeatureManagementApplicationContractsModule)
)]
[DependsOn(typeof(AbpAccountApplicationContractsModule))]
    public class eCommerceApplicationContractsModule : AbpModule
{
}
