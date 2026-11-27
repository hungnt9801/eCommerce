using eCommerce.Settings;

namespace eCommerce.Promotions;

/// <summary>
/// **Hạn mức cấu hình được** của cây điều kiện + ngân sách chạy (`PRO-002` §3.6, `ADR-03`/`ADR-05`).
/// Mặc định theo `Q1` (đề xuất SRS): `64 / 8 / 32 / 1024 / 50 / 512 / 5000`.
/// </summary>
public sealed record PromotionLimits(
    int MaxNodes,
    int MaxDepth,
    int MaxLeaves,
    int MaxDeclaredCombinations,
    int MaxCandidatesPerCart,
    int LeafVisitBudget,
    int ComparisonBudget)
{
    public static readonly PromotionLimits MacDinh = new(64, 8, 32, 1024, 50, 512, 5000);
}

/// <summary>
/// 🔒 **ĐỌC SETTING — HÀM THUẦN** (không I/O, không ném) ⇒ unit test được không cần DB.
///
/// "Dữ liệu cấu hình cũng là dữ liệu vào": giá trị lạ/âm ⇒ **mặc định an toàn** + `hopLe = false` để chỗ gọi
/// ghi cảnh báo; **KHÔNG** ném (một setting gõ sai không được làm chết checkout).
/// `MaxDepth` **bị kẹp về `1…8`** — trần cứng khớp `CHECK (Depth BETWEEN 1 AND 8)` (`ADR-03`, `TC-45`):
/// cấu hình **không bao giờ** cho qua validator rồi nổ `23514` ở DB.
/// </summary>
public static class PromotionSettings
{
    public const int MaxDepthCung = PromotionConsts.DoSauCung;

    public static PromotionLimits Doc(
        string? maxNodes,
        string? maxDepth,
        string? maxLeaves,
        string? maxDeclaredCombinations,
        string? maxCandidatesPerCart,
        string? leafVisitBudget,
        string? comparisonBudget,
        out bool hopLe)
    {
        var d = PromotionLimits.MacDinh;
        var ok = true;

        var nodes = DocSoDuong(maxNodes, d.MaxNodes, ref ok);
        var depth = DocSoDuong(maxDepth, d.MaxDepth, ref ok);
        var leaves = DocSoDuong(maxLeaves, d.MaxLeaves, ref ok);
        var combos = DocSoDuong(maxDeclaredCombinations, d.MaxDeclaredCombinations, ref ok);
        var candidates = DocSoDuong(maxCandidatesPerCart, d.MaxCandidatesPerCart, ref ok);
        var leafBudget = DocSoDuong(leafVisitBudget, d.LeafVisitBudget, ref ok);
        var compBudget = DocSoDuong(comparisonBudget, d.ComparisonBudget, ref ok);

        // 🚨 Trần CỨNG của độ sâu: chỉ nhận 1…8; ngoài dải ⇒ kẹp + báo không hợp lệ.
        if (depth < 1 || depth > MaxDepthCung)
        {
            ok = false;
            depth = depth < 1 ? 1 : MaxDepthCung;
        }

        hopLe = ok;
        return new PromotionLimits(nodes, depth, leaves, combos, candidates, leafBudget, compBudget);
    }

    /// <summary>Đọc `Promotions:Enabled` — mặc định `true`; chỉ `"false"` (không phân biệt hoa/thường) mới tắt.</summary>
    public static bool DocBat(string? enabled)
        => !string.Equals((enabled ?? string.Empty).Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trần **sinh lô mã** mặc định (`D-302`).</summary>
    public const int MaxBatchSizeMacDinh = 10_000;

    /// <summary>Trần **cứng** sinh lô (`D-302`) — cấu hình không bao giờ vượt số này.</summary>
    public const int MaxBatchSizeCung = 100_000;

    /// <summary>
    /// 🔒 **HÀM THUẦN** đọc `Promotions:MaxBatchSize` (`D-302`): kẹp `1…100000`, giá trị rỗng/không hợp lệ ⇒
    /// **mặc định** `10000`; **không** ném (một setting gõ sai không được làm chết đường sinh mã).
    /// </summary>
    public static int DocMaxBatchSize(string? tho)
    {
        if (string.IsNullOrWhiteSpace(tho))
        {
            return MaxBatchSizeMacDinh;
        }

        var so = ChinhSach.DocSo(tho, MaxBatchSizeMacDinh, out var hopLeDong);
        if (!hopLeDong)
        {
            return MaxBatchSizeMacDinh;
        }

        return KepMaxBatchSize((int)Math.Floor(so));
    }

    /// <summary>Kẹp số vào `1…100000` (hàm thuần, test được).</summary>
    public static int KepMaxBatchSize(int raw)
        => raw < 1 ? 1 : raw > MaxBatchSizeCung ? MaxBatchSizeCung : raw;

    private static int DocSoDuong(string? tho, int macDinh, ref bool ok)
    {
        // Rỗng ⇒ dùng mặc định (hợp lệ — không phải "cấu hình sai").
        if (string.IsNullOrWhiteSpace(tho))
        {
            return macDinh;
        }

        var so = ChinhSach.DocSo(tho, macDinh, out var hopLeDong);
        if (!hopLeDong || so < 1m)
        {
            ok = false;
            return macDinh;
        }

        return (int)Math.Floor(so);
    }
}
