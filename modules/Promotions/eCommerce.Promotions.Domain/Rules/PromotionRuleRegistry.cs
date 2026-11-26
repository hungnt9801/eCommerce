using eCommerce.Promotions.Contracts;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Promotions.Rules;

/// <summary>
/// **SỔ ĐĂNG KÝ RULE LÁ** (`PRO-002` §3.4) — tập rule **do DI cung cấp**, khoá theo `SystemName` **ordinal**.
/// Khởi tạo **một lần** (`ISingletonDependency`) ⇒ kết quả **tất định** và không đổi giữa các request.
///
/// 🚨 Khoá trùng ⇒ **NÉM** ngay khi khởi tạo (không để rule này "thắng" rule kia một cách ngẫu nhiên theo thứ tự DI).
/// </summary>
public class PromotionRuleRegistry : ISingletonDependency
{
    private readonly IReadOnlyDictionary<string, IPromotionConditionRule> _rules;

    public PromotionRuleRegistry(IEnumerable<IPromotionConditionRule> rules)
    {
        var tuDien = new Dictionary<string, IPromotionConditionRule>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            var ten = rule.SystemName?.Trim() ?? string.Empty;
            if (ten.Length == 0)
            {
                throw new AbpException("Rule lá có `SystemName` rỗng.");
            }

            if (!tuDien.TryAdd(ten, rule))
            {
                throw new AbpException($"Trùng `SystemName` rule lá: '{ten}' — registry phải xác định.");
            }
        }

        _rules = tuDien;
    }

    /// <summary>Tập tên rule đang có — nguồn cho validator/seeder (thứ tự ordinal để tất định).</summary>
    public IReadOnlyCollection<string> SystemNames
        => _rules.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public bool Contains(string systemName) => _rules.ContainsKey(systemName);

    public IPromotionConditionRule? Get(string systemName)
        => _rules.TryGetValue(systemName, out var rule) ? rule : null;
}
