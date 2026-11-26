using eCommerce.Promotions.Rules;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace eCommerce.Promotions.Seed;

/// <summary>
/// **META CHI PHÍ TĨNH của 4 rule v1** (`PRO-002` §3.6 / `CR-9`). Nguồn sự thật là **code** (`D-154`).
/// Rule ngoài bảng này (thêm sau) ⇒ mặc định `O1`/`1`/`false`.
/// </summary>
public static class PromotionRuleCostCatalog
{
    public sealed record Meta(string CostKind, int DeclaredCostFactor, bool RequiresVerifiedCod);

    public static readonly IReadOnlyDictionary<string, Meta> V1 = new Dictionary<string, Meta>(StringComparer.Ordinal)
    {
        [MinOrderAmountRule.Ten] = new("O1", 1, false),
        [ChannelScopeRule.Ten] = new("O1", 1, false),
        [CustomerGroupRule.Ten] = new("O1", 1, false),
        [FirstOrderOnlyRule.Ten] = new("O1", 1, false)
    };

    public static Meta Cua(string ruleSystemName)
        => V1.TryGetValue(ruleSystemName, out var meta) ? meta : new Meta("O1", 1, false);
}

/// <summary>
/// **SEEDER `ProPromotionRuleCosts`** (`PRO-002` §6.4, `D-154`) — **upsert theo registry**, dòng ngoài registry
/// ⇒ `RetiredAtUtc = now` nếu `NULL`, **0 DELETE**, idempotent (chạy 2 lần = 0 thay đổi).
/// 📌 Bảng **toàn cục** (không tenant) ⇒ chỉ chạy ở host (`TenantId = null`).
/// </summary>
public class PromotionRuleCostSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<PromotionRuleCost, string> _repo;
    private readonly PromotionRuleRegistry _registry;
    private readonly IClock _clock;

    public PromotionRuleCostSeedContributor(
        IRepository<PromotionRuleCost, string> repo,
        PromotionRuleRegistry registry,
        IClock clock)
    {
        _repo = repo;
        _registry = registry;
        _clock = clock;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId is not null)
        {
            return;
        }

        var now = _clock.Now.ToUniversalTime();
        var dangSong = _registry.SystemNames.ToHashSet(StringComparer.Ordinal);

        var hienCo = await _repo.GetListAsync();
        var theoTen = hienCo.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);

        foreach (var ten in _registry.SystemNames)
        {
            var meta = PromotionRuleCostCatalog.Cua(ten);

            if (theoTen.TryGetValue(ten, out var dong))
            {
                var daKhop = string.Equals(dong.CostKind, meta.CostKind, StringComparison.Ordinal)
                             && dong.DeclaredCostFactor == meta.DeclaredCostFactor
                             && dong.RequiresVerifiedCod == meta.RequiresVerifiedCod
                             && dong.RetiredAtUtc is null;

                if (!daKhop)
                {
                    dong.CapNhatTheoRegistry(meta.CostKind, meta.DeclaredCostFactor, meta.RequiresVerifiedCod);
                    await _repo.UpdateAsync(dong, autoSave: true);
                }
            }
            else
            {
                await _repo.InsertAsync(
                    PromotionRuleCost.TaoMoi(ten, meta.CostKind, meta.DeclaredCostFactor, meta.RequiresVerifiedCod),
                    autoSave: true);
            }
        }

        // Dòng có trong DB nhưng KHÔNG còn trong registry ⇒ đánh dấu gỡ nếu đang NULL. Không xoá.
        foreach (var dong in hienCo)
        {
            if (!dangSong.Contains(dong.Id) && dong.RetiredAtUtc is null)
            {
                dong.DanhDauGo(now);
                await _repo.UpdateAsync(dong, autoSave: true);
            }
        }
    }
}
