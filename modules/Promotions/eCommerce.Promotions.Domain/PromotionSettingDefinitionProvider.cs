using Volo.Abp.Settings;

namespace eCommerce.Promotions;

/// <summary>
/// Khai báo **8 Setting** của module `Promotions` (`PRO-002` §3.6). ABP **tự đăng ký** mọi
/// `SettingDefinitionProvider` theo quy ước (tiền lệ `PricingSettingDefinitionProvider`).
///
/// 📌 Phạm vi **Application | Tenant | Global | Configuration** (loại trừ user) — cùng khuôn `PRC-002`
/// (`D-218`): vừa cho giá trị host-level vừa theo tenant, không bị ghi đè theo từng người dùng.
/// </summary>
public class PromotionSettingDefinitionProvider : SettingDefinitionProvider
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

        var d = PromotionLimits.MacDinh;

        context.Add(
            new SettingDefinition(PromotionSettingNames.Enabled, "true").WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.MaxNodes, d.MaxNodes.ToString()).WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.MaxDepth, d.MaxDepth.ToString()).WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.MaxLeaves, d.MaxLeaves.ToString()).WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.MaxDeclaredCombinations, d.MaxDeclaredCombinations.ToString()).WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.MaxCandidatesPerCart, d.MaxCandidatesPerCart.ToString()).WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.LeafVisitBudget, d.LeafVisitBudget.ToString()).WithProviders(phamVi),
            new SettingDefinition(PromotionSettingNames.ComparisonBudget, d.ComparisonBudget.ToString()).WithProviders(phamVi));
    }
}
