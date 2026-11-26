using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Rules;
using Volo.Abp;

namespace eCommerce.Promotions.Validation;

/// <summary>Kết quả một lượt duyệt cây (chỉ đọc): ngắn mạch + bộ đếm ngân sách (`ADR-04`/`ADR-05`).</summary>
public sealed record PromotionTreeEvaluation(bool Satisfied, int LeafVisits, int Comparisons, bool BudgetExceeded);

/// <summary>
/// **DUYỆT CÂY LÚC CHẠY** (`PRO-002` §6.1 bước 4) — hàm **THUẦN**, **ngắn mạch**, đếm ngân sách, **KHÔNG I/O**.
///
/// 🔒 Thứ tự duyệt **tất định**: con sắp theo `SortOrder`, rồi `Id` (`BR-09`).
/// 🔒 Ngân sách: `LeafVisits` cộng mỗi lá thăm; `Comparisons` cộng mỗi phép so (rule v1 = O1 ⇒ 1/lá).
/// Vượt ⇒ **dừng** và trả `BudgetExceeded = true`; engine quyết định ném (`0005`, nguồn **mã**) hay bỏ candidate
/// (nguồn **tự động**) theo `PRO-002` §6.1 quy tắc 8.
/// </summary>
public static class PromotionTreeEvaluator
{
    public static PromotionTreeEvaluation DanhGia(
        IReadOnlyList<PromotionConditionNode> nodes,
        PromotionRuleRegistry registry,
        IReadOnlySet<string> knownRuleRows,
        PromotionRuleContext context,
        PromotionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(limits);

        var goc = nodes.Where(x => x.ParentId is null).ToList();
        if (goc.Count != 1)
        {
            throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                .WithData("lyDo", "Cây phải có ĐÚNG MỘT node gốc.")
                .WithData("SoGoc", goc.Count);
        }

        var conCua = nodes
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList());

        var dem = new DemNganSach();
        var thoa = Duyet(goc[0], conCua, registry, knownRuleRows, context, limits, dem);

        return new PromotionTreeEvaluation(thoa && !dem.Vuot, dem.LeafVisits, dem.Comparisons, dem.Vuot);
    }

    private static bool Duyet(
        PromotionConditionNode node,
        IReadOnlyDictionary<Guid, List<PromotionConditionNode>> conCua,
        PromotionRuleRegistry registry,
        IReadOnlySet<string> knownRuleRows,
        PromotionRuleContext context,
        PromotionLimits limits,
        DemNganSach dem)
    {
        if (dem.Vuot)
        {
            return false;
        }

        if (!node.IsGroup)
        {
            dem.LeafVisits++;
            dem.Comparisons++;

            if (dem.LeafVisits > limits.LeafVisitBudget || dem.Comparisons > limits.ComparisonBudget)
            {
                dem.Vuot = true;
                return false;
            }

            var ten = node.RuleSystemName!;
            var rule = registry.Get(ten);
            if (rule is null)
            {
                var maLoi = PromotionTreeValidator.PhanLoaiRule(ten, registry.SystemNames, knownRuleRows);
                throw new BusinessException(maLoi ?? PromotionErrorCodes.InvalidConditionNode)
                    .WithData("SystemName", ten);
            }

            return rule.IsSatisfied(context);
        }

        var con = conCua.TryGetValue(node.Id, out var ds) ? ds : [];

        if (node.InteractionType == PromotionConditionGroupType.And)
        {
            foreach (var c in con)
            {
                if (dem.Vuot)
                {
                    return false;
                }

                if (!Duyet(c, conCua, registry, knownRuleRows, context, limits, dem))
                {
                    return false; // ngắn mạch AND
                }
            }

            return true;
        }

        // Or
        foreach (var c in con)
        {
            if (dem.Vuot)
            {
                return false;
            }

            if (Duyet(c, conCua, registry, knownRuleRows, context, limits, dem))
            {
                return true; // ngắn mạch OR
            }
        }

        return false;
    }

    private sealed class DemNganSach
    {
        public int LeafVisits;
        public int Comparisons;
        public bool Vuot;
    }
}
