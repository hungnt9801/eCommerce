namespace eCommerce.Promotions.Engine;

/// <summary>Node cây nạp từ DB để engine đánh giá (cùng hình dạng `PromotionConditionNode`).</summary>
public sealed record PromotionConditionData(
    Guid Id,
    Guid? ParentId,
    bool IsGroup,
    PromotionConditionGroupType? InteractionType,
    string? RuleSystemName,
    int SortOrder,
    int Depth);

/// <summary>Một candidate (promotion) đã nạp đủ dữ liệu để đánh giá **không truy vấn trong vòng lá**.</summary>
public sealed record PromotionCandidateData(
    Guid PromotionId,
    PromotionKind Kind,
    bool IsAutomatic,
    int Priority,
    bool IsCumulative,
    string Currency,
    PromotionDiscountKind DiscountKind,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal? MinOrderAmount,
    string? ChannelScope,
    int TreeVersion,
    int? MaxUsageTotal,
    int UsedCount,
    int ReservedCount,
    int? MaxUsagePerCustomer,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    Guid? VoucherCodeId,
    int? VoucherMaxUsage,
    int VoucherUsedCount,
    int VoucherReservedCount,
    IReadOnlyList<PromotionConditionData> Conditions,
    // 🆕 `D-291` ① P1 #1 — trạng thái để engine guard **fail-closed** ở đường voucher (`Status == Active`
    // **và** `VoucherCode.Status != Revoked`). Mặc định `Active`/`null` để không phá caller cũ; nguồn thật
    // (`EfPromotionDataSource.Map`) **luôn** điền tường minh. `Status` khác `Active` hoặc voucher `Revoked`
    // ⇒ engine coi như **không có candidate** (không áp, 0 đồng).
    PromotionStatus Status = PromotionStatus.Active,
    VoucherCodeStatus? VoucherStatus = null);

/// <summary>
/// **CỔNG NẠP DỮ LIỆU** cho engine — tách khỏi `IRepository` để engine test được **không cần DB**
/// và để mọi truy vấn nằm **ngoài** vòng lá (`ADR-06`/`TC-21`).
/// </summary>
public interface IPromotionDataSource
{
    Task<PromotionCandidateData?> FindVoucherCandidateAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromotionCandidateData>> ListAutoCandidatesAsync(
        DateTime nowUtc, string channelCode, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> LoadKnownRuleRowsAsync(
        IReadOnlyCollection<string> names, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> LoadDeclaredCostsAsync(
        IReadOnlyCollection<string> names, CancellationToken cancellationToken = default);

    Task<int> CountCustomerUsageAsync(Guid promotionId, Guid customerId, CancellationToken cancellationToken = default);
}
