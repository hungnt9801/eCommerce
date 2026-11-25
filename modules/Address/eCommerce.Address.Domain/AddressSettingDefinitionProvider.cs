using Volo.Abp.Settings;

namespace eCommerce.Address;

/// <summary>Tên các **Setting của module `Address`** (không phải cấu hình `IConfiguration`).</summary>
public static class AddressSettingNames
{
    /// <summary>TTL cache tra cứu danh mục (giây) — mặc định **86 400** (24 h, `D-242`).</summary>
    public const string CacheTtlSeconds = "Address:Cache:TtlSeconds";
}

/// <summary>
/// Khai báo Setting của `Address` — ABP tự đăng ký mọi `SettingDefinitionProvider` theo quy ước
/// (tiền lệ `PricingSettingDefinitionProvider`).
///
/// 📌 Phạm vi `Application | Tenant` (khuôn `D-218`): cho phép giá trị host-level **và** theo tenant,
/// **loại trừ user** — TTL cache là chính sách vận hành, không phải sở thích từng người.
/// </summary>
public class AddressSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        var phamVi = new[]
        {
            DefaultValueSettingValueProvider.ProviderName,
            ConfigurationSettingValueProvider.ProviderName,
            GlobalSettingValueProvider.ProviderName,
            TenantSettingValueProvider.ProviderName
        };

        context.Add(
            new SettingDefinition(AddressSettingNames.CacheTtlSeconds, "86400").WithProviders(phamVi));
    }
}
