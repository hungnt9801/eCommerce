using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Rules;
using eCommerce.Promotions.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Promotions;

/// <summary>
/// **Service ĐỌC rule catalog** (`PRO-002` §6.2; `D-285` ①(ii), `D-289`, `D-290`).
///
/// 🔐 `[Authorize]` cấp **CLASS** (`Promotions.Default`) + method hẹp `Promotions.View` (luật A1).
/// 🚫 **Không** ghi, không khoá, không idempotency — chỉ đọc registry + `ProPromotionRuleCosts`.
/// 📌 Vòng lặp trên **`PromotionRuleRegistry.SystemNames`** (**không** duyệt bảng DB) ⇒ mỗi rule **đúng một**
/// dòng, kể cả khi seeder chưa chạy (`IsSeeded = false`, dùng `PromotionRuleCostCatalog.Cua` — `D-289`).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Promotions.Default)]
public class PromotionRuleCatalogAppService : ApplicationService, IPromotionRuleCatalogAppService
{
    /// <summary>Trần số tên liệt kê trong một dòng `LogWarning` (`D-289` ①(4) — "trần ~10 tên + …").</summary>
    private const int TranTenNhatKy = 10;

    private readonly IRepository<PromotionRuleCost, string> _ruleCosts;
    private readonly PromotionRuleRegistry _registry;
    private readonly ILogger<PromotionRuleCatalogAppService> _logger;

    public PromotionRuleCatalogAppService(
        IRepository<PromotionRuleCost, string> ruleCosts,
        PromotionRuleRegistry registry,
        ILogger<PromotionRuleCatalogAppService> logger)
    {
        _ruleCosts = ruleCosts;
        _registry = registry;
        _logger = logger;
    }

    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<IReadOnlyList<PromotionRuleCatalogItemDto>> GetRulesAsync()
    {
        IReadOnlyCollection<string> ten = _registry.SystemNames;

        var dong = ten.Count == 0
            ? new Dictionary<string, PromotionRuleCost>(StringComparer.Ordinal)
            : (await _ruleCosts.GetListAsync(x => ten.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);

        var ketQua = new List<PromotionRuleCatalogItemDto>(ten.Count);
        List<string>? thieu = null;

        foreach (var name in ten)
        {
            // Nhãn LUÔN lấy từ code (`D-290`) — không lấy từ DB nên không chịu khe deploy/seeder.
            var nhan = PromotionRuleCostCatalog.Cua(name).Label;

            if (dong.TryGetValue(name, out var dongChiPhi))
            {
                ketQua.Add(new PromotionRuleCatalogItemDto
                {
                    SystemName = name,
                    Label = nhan,
                    RequiresVerifiedCod = dongChiPhi.RequiresVerifiedCod,
                    DeclaredCostFactor = dongChiPhi.DeclaredCostFactor,
                    RetiredAtUtc = dongChiPhi.RetiredAtUtc,
                    IsSeeded = true
                });
            }
            else
            {
                // `D-289`: registry ∈ nhưng CHƯA có dòng ⇒ trả mặc định `Cua` + IsSeeded = false, KHÔNG ném.
                var macDinh = PromotionRuleCostCatalog.Cua(name);
                ketQua.Add(new PromotionRuleCatalogItemDto
                {
                    SystemName = name,
                    Label = nhan,
                    RequiresVerifiedCod = macDinh.RequiresVerifiedCod,
                    DeclaredCostFactor = macDinh.DeclaredCostFactor,
                    RetiredAtUtc = null,
                    IsSeeded = false
                });
                (thieu ??= new List<string>()).Add(name);
            }
        }

        if (thieu is { Count: > 0 })
        {
            _logger.LogWarning(
                "rule-catalog: {SoLuong} SystemName trong registry chưa có dòng ProPromotionRuleCosts " +
                "(đang dùng mặc định 1/false, IsSeeded=false): {Ten}",
                thieu.Count,
                LietKe(thieu));
        }

        return ketQua;
    }

    private static string LietKe(IReadOnlyList<string> ten)
        => ten.Count <= TranTenNhatKy
            ? string.Join(", ", ten)
            : string.Join(", ", ten.Take(TranTenNhatKy)) + ", …";
}
