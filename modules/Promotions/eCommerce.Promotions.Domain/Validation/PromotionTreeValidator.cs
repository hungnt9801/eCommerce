using Volo.Abp;

namespace eCommerce.Promotions.Validation;

/// <summary>Node của cây điều kiện đầu vào validator (đọc từ DTO hoặc từ DB — cùng một hình dạng).</summary>
public sealed record PromotionConditionNode(
    Guid Id,
    Guid? ParentId,
    bool IsGroup,
    PromotionConditionGroupType? InteractionType,
    string? RuleSystemName,
    int SortOrder,
    int Depth);

/// <summary>Số đo cây để UI hiển thị "đã dùng / trần" (`PRO-002` §6.2 `SaveTreeAsync`).</summary>
public sealed record PromotionTreeValidationResult(
    int NodeCount,
    int Depth,
    int LeafCount,
    int DeclaredCost,
    int MaxDeclaredCombinations);

/// <summary>
/// **GUARD GHI** (`PRO-002` §3.5) — hàm **THUẦN**, dùng chung cho `SaveTreeAsync` và `ApproveAsync`.
///
/// Thứ tự kiểm **cố định** (`PRO-002` §4.2 bước 4 / `TC-06`): **node → độ sâu → lá → tổ hợp khai báo**,
/// rồi cấu trúc (đúng 1 gốc · không node lai · không chu trình · phân loại rule). Sai ⇒ **NÉM**
/// `0001` / `0002` / `0003` / `0004` / `0010`.
/// </summary>
public static class PromotionTreeValidator
{
    public static PromotionTreeValidationResult Validate(
        IReadOnlyList<PromotionConditionNode> nodes,
        PromotionLimits limits,
        IReadOnlyCollection<string> registryNames,
        IReadOnlySet<string> knownRuleRows,
        IReadOnlyDictionary<string, int> declaredCostByRule)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(limits);

        var nodeCount = nodes.Count;
        var leafs = nodes.Where(x => !x.IsGroup).ToList();
        var leafCount = leafs.Count;
        var depth = nodes.Count == 0 ? 0 : nodes.Max(x => x.Depth);

        // ── 1) TRẦN NODE / ĐỘ SÂU / LÁ ────────────────────────────────────────────────────────────
        if (nodeCount > limits.MaxNodes || depth > limits.MaxDepth || depth > PromotionConsts.DoSauCung
            || leafCount > limits.MaxLeaves)
        {
            throw new BusinessException(PromotionErrorCodes.TreeTooLarge)
                .WithData("NodeCount", nodeCount)
                .WithData("Depth", depth)
                .WithData("LeafCount", leafCount)
                .WithData("MaxNodes", limits.MaxNodes)
                .WithData("MaxDepth", limits.MaxDepth)
                .WithData("MaxLeaves", limits.MaxLeaves);
        }

        // ── 2) TRẦN TỔ HỢP KHAI BÁO (BR-06) ───────────────────────────────────────────────────────
        var declaredCost = TinhDeclaredCost(leafs, declaredCostByRule);
        if (declaredCost > limits.MaxDeclaredCombinations)
        {
            throw new BusinessException(PromotionErrorCodes.CombinationExplosion)
                .WithData("DeclaredCost", declaredCost)
                .WithData("Max", limits.MaxDeclaredCombinations);
        }

        // ── 3) CẤU TRÚC ────────────────────────────────────────────────────────────────────────────
        KiemTraCauTruc(nodes, registryNames, knownRuleRows);

        return new PromotionTreeValidationResult(nodeCount, depth, leafCount, declaredCost, limits.MaxDeclaredCombinations);
    }

    /// <summary>
    /// **Hàm phân loại DUY NHẤT** `0004`/`0010` (`D-154`) — quyết theo **sự có mặt của dòng** ở
    /// `ProPromotionRuleCosts`, **không** theo `RetiredAtUtc`:
    /// • ∈ registry ⇒ hợp lệ;
    /// • ∉ registry **và có dòng** ⇒ `0010` (rule đã đăng ký rồi bị gỡ);
    /// • ∉ registry **và không có dòng** ⇒ `0004` (chưa từng đăng ký).
    /// </summary>
    public static string? PhanLoaiRule(string systemName, IReadOnlyCollection<string> registryNames, IReadOnlySet<string> knownRuleRows)
    {
        if (registryNames.Contains(systemName, StringComparer.Ordinal))
        {
            return null;
        }

        return knownRuleRows.Contains(systemName)
            ? PromotionErrorCodes.RuleNotRegistered
            : PromotionErrorCodes.InvalidConditionNode;
    }

    public static int TinhDeclaredCost(IReadOnlyList<PromotionConditionNode> leafs, IReadOnlyDictionary<string, int> declaredCostByRule)
    {
        var tong = 0;
        foreach (var la in leafs)
        {
            var ten = la.RuleSystemName ?? string.Empty;
            tong += declaredCostByRule.TryGetValue(ten, out var factor) && factor > 0 ? factor : 1;
        }

        return tong;
    }

    private static void KiemTraCauTruc(
        IReadOnlyList<PromotionConditionNode> nodes,
        IReadOnlyCollection<string> registryNames,
        IReadOnlySet<string> knownRuleRows)
    {
        if (nodes.Count == 0)
        {
            // Cây rỗng: không có gốc ⇒ `0004` (không thể duyệt). SaveTree ở Draft có thể từ chối sớm.
            throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                .WithData("lyDo", "Cây điều kiện rỗng — phải có đúng 1 gốc.");
        }

        var ids = nodes.Select(x => x.Id).ToHashSet();

        // Đúng 1 gốc.
        var goc = nodes.Count(x => x.ParentId is null);
        if (goc != 1)
        {
            throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                .WithData("lyDo", "Cây phải có ĐÚNG MỘT node gốc.")
                .WithData("SoGoc", goc);
        }

        // Node lai + parent trỏ ra ngoài + tự làm cha.
        foreach (var n in nodes)
        {
            if (n.IsGroup)
            {
                if (n.InteractionType is null || !string.IsNullOrEmpty(n.RuleSystemName))
                {
                    throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                        .WithData("lyDo", "Node nhóm phải có InteractionType và KHÔNG có RuleSystemName.")
                        .WithData("nodeId", n.Id);
                }
            }
            else
            {
                if (n.InteractionType is not null || string.IsNullOrWhiteSpace(n.RuleSystemName))
                {
                    throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                        .WithData("lyDo", "Node lá phải có RuleSystemName và KHÔNG có InteractionType.")
                        .WithData("nodeId", n.Id);
                }

                var maLoi = PhanLoaiRule(n.RuleSystemName!, registryNames, knownRuleRows);
                if (maLoi == PromotionErrorCodes.RuleNotRegistered)
                {
                    throw new BusinessException(PromotionErrorCodes.RuleNotRegistered)
                        .WithData("SystemName", n.RuleSystemName);
                }

                if (maLoi is not null)
                {
                    throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                        .WithData("lyDo", "Rule chưa từng đăng ký.")
                        .WithData("SystemName", n.RuleSystemName);
                }
            }

            if (n.ParentId is not null)
            {
                if (n.ParentId == n.Id)
                {
                    throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                        .WithData("lyDo", "Node tự làm cha.")
                        .WithData("nodeId", n.Id);
                }

                if (!ids.Contains(n.ParentId.Value))
                {
                    throw new BusinessException(PromotionErrorCodes.InvalidConditionNode)
                        .WithData("lyDo", "ParentId trỏ ra ngoài promotion.")
                        .WithData("nodeId", n.Id);
                }
            }
        }

        // Chu trình: đi ngược ParentId quá NodeCount bước ⇒ có vòng.
        var chaCua = nodes.ToDictionary(x => x.Id, x => x.ParentId);
        foreach (var n in nodes)
        {
            var buoc = 0;
            var hienTai = n.ParentId;
            while (hienTai is not null)
            {
                if (++buoc > nodes.Count)
                {
                    throw new BusinessException(PromotionErrorCodes.ConditionCycleDetected)
                        .WithData("nodeId", n.Id);
                }

                hienTai = chaCua.TryGetValue(hienTai.Value, out var cha) ? cha : null;
            }
        }
    }
}
