using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using eCommerce.Kernel.Observability;
using eCommerce.Promotions.Contracts;
using eCommerce.Promotions.Engine;
using eCommerce.Promotions.Rules;

namespace eCommerce.Promotions.Tests;

/// <summary>Hạ tầng test: đồng hồ giả, setting giả, data source giả, builder candidate.</summary>
internal sealed class DongHoGia : IClock
{
    public DongHoGia(DateTime now) => Now = now;

    public DateTime Now { get; set; }
    public DateTimeKind Kind => DateTimeKind.Utc;
    public bool SupportsMultipleTimezone => false;
    public DateTime Normalize(DateTime dateTime) => dateTime;
    public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;
    public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime) => dateTime;
    public DateTime ConvertToUtc(DateTime dateTime) => dateTime;
}

internal sealed class SettingGia : ISettingProvider
{
    private readonly Dictionary<string, string?> _giaTri;

    public SettingGia(Dictionary<string, string?>? giaTri = null)
        => _giaTri = giaTri ?? new Dictionary<string, string?>(StringComparer.Ordinal);

    public Task<string?> GetOrNullAsync(string name)
        => Task.FromResult(_giaTri.TryGetValue(name, out var v) ? v : null);

    public Task<List<SettingValue>> GetAllAsync(string[] names)
        => Task.FromResult(names.Select(n => new SettingValue(n, _giaTri.TryGetValue(n, out var v) ? v : null)).ToList());

    public Task<List<SettingValue>> GetAllAsync()
        => Task.FromResult(_giaTri.Select(kv => new SettingValue(kv.Key, kv.Value)).ToList());
}

internal sealed class DataSourceGia : IPromotionDataSource
{
    public PromotionCandidateData? Voucher { get; set; }
    public List<PromotionCandidateData> Autos { get; } = new();
    public HashSet<string> KnownRows { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> Costs { get; } = new(StringComparer.Ordinal);
    public int CustomerUsage { get; set; }
    public int SoLanNapVoucher { get; private set; }
    public int SoLanNapTuDong { get; private set; }

    public Task<PromotionCandidateData?> FindVoucherCandidateAsync(string code, CancellationToken cancellationToken = default)
    {
        SoLanNapVoucher++;
        return Task.FromResult(Voucher);
    }

    public Task<IReadOnlyList<PromotionCandidateData>> ListAutoCandidatesAsync(DateTime nowUtc, string channelCode, CancellationToken cancellationToken = default)
    {
        SoLanNapTuDong++;
        return Task.FromResult<IReadOnlyList<PromotionCandidateData>>(Autos.ToList());
    }

    public Task<IReadOnlySet<string>> LoadKnownRuleRowsAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(KnownRows);

    public Task<IReadOnlyDictionary<string, int>> LoadDeclaredCostsAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(Costs);

    public Task<int> CountCustomerUsageAsync(Guid promotionId, Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(CustomerUsage);
}

internal static class HaTang
{
    public static PromotionRuleRegistry Registry() => new(new IPromotionConditionRule[]
    {
        new MinOrderAmountRule(),
        new ChannelScopeRule(),
        new CustomerGroupRule(),
        new FirstOrderOnlyRule()
    });

    public static PromotionEngine Engine(
        DataSourceGia data, IClock? clock = null, Dictionary<string, string?>? settings = null)
        => new(
            data,
            Registry(),
            new SettingGia(settings),
            clock ?? new DongHoGia(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<PromotionEngine>.Instance,
            new eCommerceMetrics(new System.Diagnostics.Metrics.Meter("promo-test-" + Guid.NewGuid())));

    // Child order: (id, isGroup, interaction, rule, sortOrder, depth, parent)
    public static PromotionCandidateData Candidate(
        Guid promotionId,
        IReadOnlyList<PromotionConditionData> conditions,
        bool isAutomatic = true,
        int priority = 0,
        bool isCumulative = true,
        PromotionDiscountKind kind = PromotionDiscountKind.FixedAmount,
        decimal value = 0m,
        decimal? maxDiscount = null,
        decimal? minOrder = null,
        string? channelScope = null,
        int? maxUsageTotal = null,
        int used = 0,
        int? maxUsagePerCustomer = null,
        Guid? voucherCodeId = null,
        int? voucherMaxUsage = null,
        int voucherUsed = 0,
        string currency = "VND",
        DateTime? startsAt = null)
        => new(
            promotionId,
            isAutomatic ? PromotionKind.Discount : PromotionKind.Voucher,
            isAutomatic,
            priority,
            isCumulative,
            currency,
            kind,
            value,
            maxDiscount,
            minOrder,
            channelScope,
            1,
            maxUsageTotal,
            used,
            0,
            maxUsagePerCustomer,
            startsAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null,
            voucherCodeId,
            voucherMaxUsage,
            voucherUsed,
            0,
            conditions);

    /// <summary>Cây một lá `MinOrderAmount` (ngưỡng null ⇒ true).</summary>
    public static IReadOnlyList<PromotionConditionData> CayMotLa(string rule = "MinOrderAmount")
    {
        var root = Guid.NewGuid();
        return new[]
        {
            new PromotionConditionData(root, null, false, null, rule, 0, 1)
        };
    }

    /// <summary>Cây AND/OR có `n` lá cùng `rule`.</summary>
    public static IReadOnlyList<PromotionConditionData> CayNhom(PromotionConditionGroupType loai, string rule, int n)
    {
        var root = Guid.NewGuid();
        var ds = new List<PromotionConditionData>
        {
            new(root, null, true, loai, null, 0, 1)
        };

        for (var i = 0; i < n; i++)
        {
            ds.Add(new PromotionConditionData(Guid.NewGuid(), root, false, null, rule, i, 2));
        }

        return ds;
    }
}
