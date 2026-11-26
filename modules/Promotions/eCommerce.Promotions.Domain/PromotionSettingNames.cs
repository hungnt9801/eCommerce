namespace eCommerce.Promotions;

/// <summary>
/// **TÊN KHOÁ SETTING** của module (`PRO-002` §3.6 / `Q1`). Quy ước `<Module>:<Key>`.
/// 📌 Đặt ở **module** (không nhét vào Kernel) — chỗ đọc giá trị trả kiểu **của Promotions**.
/// </summary>
public static class PromotionSettingNames
{
    /// <summary>Bật/tắt toàn module ở đường `Sales` (cờ tắt = đường lùi, `PRO-002` §9.3). Mặc định `true`.</summary>
    public const string Enabled = "Promotions:Enabled";

    public const string MaxNodes = "Promotions:MaxNodes";
    public const string MaxDepth = "Promotions:MaxDepth";
    public const string MaxLeaves = "Promotions:MaxLeaves";
    public const string MaxDeclaredCombinations = "Promotions:MaxDeclaredCombinations";
    public const string MaxCandidatesPerCart = "Promotions:MaxCandidatesPerCart";
    public const string LeafVisitBudget = "Promotions:LeafVisitBudget";
    public const string ComparisonBudget = "Promotions:ComparisonBudget";
}
