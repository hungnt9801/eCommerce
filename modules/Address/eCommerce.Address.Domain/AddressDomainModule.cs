using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Address;

/// <summary>Module tầng Domain của `Address` — sở hữu danh mục &amp; xác minh ĐVHC (`ADDR-001`).</summary>
[DependsOn(
    typeof(AbpDddDomainModule),
    // 🆕 `WP-18`: `AddressSettingDefinitionProvider` (`Address:Cache:TtlSeconds`, `D-242`) chỉ được ABP
    // quét/đăng ký khi `AbpSettingsModule` nằm trong đồ thị module.
    typeof(Volo.Abp.Settings.AbpSettingsModule)
)]
public class AddressDomainModule : AbpModule
{
}
