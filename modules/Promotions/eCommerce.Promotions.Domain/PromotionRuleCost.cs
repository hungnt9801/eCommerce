using Volo.Abp.Domain.Entities;

namespace eCommerce.Promotions;

/// <summary>
/// **Bảng tra chi phí tĩnh của rule** (`PRO-002` §3.5, `ProPromotionRuleCosts`) — **TOÀN CỤC, KHÔNG `TenantId`**.
/// 🔒 PK = `RuleSystemName` (chuỗi). Nguồn sự thật là **code** (registry); seeder **upsert**, dòng ngoài registry
/// ⇒ `RetiredAtUtc = now` nếu `NULL`; **0 DELETE** (`D-154`).
/// </summary>
public class PromotionRuleCost : Entity<string>
{
    public string CostKind { get; private set; } = default!;
    public int DeclaredCostFactor { get; private set; }
    public bool RequiresVerifiedCod { get; private set; }
    public DateTime? RetiredAtUtc { get; private set; }

    protected PromotionRuleCost()
    {
    }

    public static PromotionRuleCost TaoMoi(string ruleSystemName, string costKind, int declaredCostFactor, bool requiresVerifiedCod)
        => new()
        {
            Id = ruleSystemName,
            CostKind = costKind,
            DeclaredCostFactor = declaredCostFactor,
            RequiresVerifiedCod = requiresVerifiedCod,
            RetiredAtUtc = null
        };

    /// <summary>Upsert theo registry: cập nhật thuộc tính + **hạ** `RetiredAtUtc` về `NULL` (rule sống lại).</summary>
    public void CapNhatTheoRegistry(string costKind, int declaredCostFactor, bool requiresVerifiedCod)
    {
        CostKind = costKind;
        DeclaredCostFactor = declaredCostFactor;
        RequiresVerifiedCod = requiresVerifiedCod;
        RetiredAtUtc = null;
    }

    /// <summary>Đánh dấu rule bị gỡ khỏi registry — **giữ dòng**, **không** xoá (`D-154`).</summary>
    public void DanhDauGo(DateTime nowUtc)
    {
        RetiredAtUtc ??= nowUtc;
    }
}
