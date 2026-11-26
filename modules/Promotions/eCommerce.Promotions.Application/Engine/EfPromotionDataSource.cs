using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Promotions.Engine;

/// <summary>
/// Hiện thực <see cref="IPromotionDataSource"/> trên `eCommerceDbContext` hợp nhất. **CHỈ ĐỌC** — mọi query nằm
/// **ngoài** vòng lá (nạp candidate + cây một lần mỗi candidate).
/// </summary>
public class EfPromotionDataSource : IPromotionDataSource, ITransientDependency
{
    private readonly IRepository<Promotion, Guid> _promotions;
    private readonly IRepository<PromotionCondition, Guid> _conditions;
    private readonly IRepository<PromotionRuleCost, string> _ruleCosts;
    private readonly IRepository<VoucherCode, Guid> _vouchers;
    private readonly IRepository<PromotionRedemption, Guid> _redemptions;

    public EfPromotionDataSource(
        IRepository<Promotion, Guid> promotions,
        IRepository<PromotionCondition, Guid> conditions,
        IRepository<PromotionRuleCost, string> ruleCosts,
        IRepository<VoucherCode, Guid> vouchers,
        IRepository<PromotionRedemption, Guid> redemptions)
    {
        _promotions = promotions;
        _conditions = conditions;
        _ruleCosts = ruleCosts;
        _vouchers = vouchers;
        _redemptions = redemptions;
    }

    public async Task<PromotionCandidateData?> FindVoucherCandidateAsync(string code, CancellationToken cancellationToken = default)
    {
        var voucher = await _vouchers.FirstOrDefaultAsync(x => x.Code == code, cancellationToken: cancellationToken);
        if (voucher is null)
        {
            return null;
        }

        var promotion = await _promotions.FindAsync(voucher.PromotionId, cancellationToken: cancellationToken);
        if (promotion is null)
        {
            return null;
        }

        var conditions = await _conditions.GetListAsync(x => x.PromotionId == promotion.Id, cancellationToken: cancellationToken);
        return Map(promotion, conditions, voucher, isAutomatic: false);
    }

    public async Task<IReadOnlyList<PromotionCandidateData>> ListAutoCandidatesAsync(
        DateTime nowUtc, string channelCode, CancellationToken cancellationToken = default)
    {
        var ds = await _promotions.GetListAsync(
            x => x.Status == PromotionStatus.Active && x.Kind == PromotionKind.Discount,
            cancellationToken: cancellationToken);

        var hopLe = ds
            .Where(x => x.StartsAtUtc.ToUniversalTime() <= nowUtc
                        && (x.EndsAtUtc is null || x.EndsAtUtc.Value.ToUniversalTime() > nowUtc)
                        && (x.ChannelScope is null || string.Equals(x.ChannelScope, channelCode, StringComparison.Ordinal)))
            .ToList();

        if (hopLe.Count == 0)
        {
            return [];
        }

        var ids = hopLe.Select(x => x.Id).ToList();
        var conditions = await _conditions.GetListAsync(x => ids.Contains(x.PromotionId), cancellationToken: cancellationToken);
        var theoPromotion = conditions.GroupBy(x => x.PromotionId).ToDictionary(g => g.Key, g => (IReadOnlyList<PromotionCondition>)g.ToList());

        return hopLe
            .Select(p => Map(p, theoPromotion.GetValueOrDefault(p.Id, []), voucher: null, isAutomatic: true))
            .ToList();
    }

    public async Task<IReadOnlySet<string>> LoadKnownRuleRowsAsync(
        IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
    {
        if (names.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ds = await _ruleCosts.GetListAsync(x => names.Contains(x.Id), cancellationToken: cancellationToken);
        return ds.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, int>> LoadDeclaredCostsAsync(
        IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
    {
        if (names.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var ds = await _ruleCosts.GetListAsync(x => names.Contains(x.Id), cancellationToken: cancellationToken);
        return ds.ToDictionary(x => x.Id, x => x.DeclaredCostFactor, StringComparer.Ordinal);
    }

    public async Task<int> CountCustomerUsageAsync(Guid promotionId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var ds = await _redemptions.GetListAsync(
            x => x.PromotionId == promotionId
                 && x.CustomerId == customerId
                 && x.Status != PromotionRedemptionStatus.Released,
            cancellationToken: cancellationToken);

        return ds.Count;
    }

    private static PromotionCandidateData Map(
        Promotion p, IReadOnlyList<PromotionCondition> conditions, VoucherCode? voucher, bool isAutomatic)
        => new(
            p.Id,
            p.Kind,
            isAutomatic,
            p.Priority,
            p.IsCumulative,
            p.Currency,
            p.DiscountKind,
            p.DiscountValue,
            p.MaxDiscountAmount,
            p.MinOrderAmount,
            p.ChannelScope,
            p.TreeVersion,
            p.MaxUsageTotal,
            p.UsedCount,
            p.ReservedCount,
            p.MaxUsagePerCustomer,
            p.StartsAtUtc,
            p.EndsAtUtc,
            voucher?.Id,
            voucher?.MaxUsage,
            voucher?.UsedCount ?? 0,
            voucher?.ReservedCount ?? 0,
            conditions
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new PromotionConditionData(
                    x.Id, x.ParentId, x.IsGroup, x.InteractionType, x.RuleSystemName, x.SortOrder, x.Depth))
                .ToList());
}
