using System.Reflection;
using eCommerce.Promotions.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// 🔒 **KHOÁ ĐĂNG KÝ DI CỦA 4 RULE LÁ** (`PRO-002`, bug thật đo qua probe N97 — commit BE `3d52d51`).
///
/// 🚨 **VÌ SAO:** ABP đăng ký conventional theo **TÊN** (`I&lt;TênLớp&gt;`) — `MinOrderAmountRule` chỉ được expose
/// dưới `IMinOrderAmountRule`, **KHÔNG** khớp `IPromotionConditionRule` ⇒ `PromotionRuleRegistry(IEnumerable&lt;…&gt;)`
/// **rỗng** trong Host runtime ⇒ `GET /api/v1/promotion/rule-catalog` trả `[]` (và FE không dựng được cây).
/// Unit test dựng registry **tay** vẫn xanh ⇒ chỉ lộ khi chạy Host thật. `[ExposeServices(typeof(IPromotionConditionRule))]`
/// ghim cặp này; test dưới **resolve qua DI thật** (khuôn `HoanTienS5Tests.IXuLyKetQuaCong_PHAI_duoc_ExposeServices…`).
///
/// 🔑 Test khoá **cơ chế** (container resolve ra đủ 4 `SystemName`), không chỉ khoá "lời hứa" reflection tĩnh.
/// </summary>
public class PromotionRuleDiTests
{
    private static ServiceProvider DungContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Đăng ký DI theo quy ước ABP cho assembly Domain ⇒ tôn trọng `ISingletonDependency` + `[ExposeServices]`.
        services.AddAssemblyOf<PromotionRuleRegistry>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Di_resolve_IPromotionConditionRule__du_4_rule()
    {
        using var sp = DungContainer();

        var tatCa = sp.GetServices<Contracts.IPromotionConditionRule>().ToList();

        Assert.Equal(4, tatCa.Count);
    }

    [Fact]
    public void Di_resolve_PromotionRuleRegistry__du_4_SystemName_va_khong_nem()
    {
        using var sp = DungContainer();

        var registry = sp.GetRequiredService<PromotionRuleRegistry>();
        var ten = registry.SystemNames.OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(
            new[] { "ChannelScope", "CustomerGroup", "FirstOrderOnly", "MinOrderAmount" },
            ten);
    }

    [Fact]
    public void Bon_rule_deu_khai_ExposeServices_cho_hop_dong()
    {
        foreach (var lop in new[]
                 {
                     typeof(MinOrderAmountRule), typeof(ChannelScopeRule),
                     typeof(CustomerGroupRule), typeof(FirstOrderOnlyRule)
                 })
        {
            var thuocTinh = lop.GetCustomAttributes<Volo.Abp.DependencyInjection.ExposeServicesAttribute>();
            Assert.Contains(thuocTinh, t => t.ServiceTypes.Contains(typeof(Contracts.IPromotionConditionRule)));
        }
    }
}
