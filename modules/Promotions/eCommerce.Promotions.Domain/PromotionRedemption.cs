using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Promotions;

/// <summary>
/// **Sổ cái lượt + snapshot** (`ProPromotionRedemptions`, `BR-16`/`CR-5`) — **CHỈ THÊM DÒNG**;
/// chỉ các cột chuyển trạng thái (`Status`/`ReleasedAtUtc`/`ReversedAtUtc`/`ReasonCode`) được `UPDATE`.
/// `OrderId` là **tham chiếu mềm**, KHÔNG FK sang `Sales` (`ADR-07`).
/// </summary>
public class PromotionRedemption : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PromotionId { get; private set; }
    public Guid? VoucherCodeId { get; private set; }
    public Guid? OrderId { get; private set; }
    public PromotionRedemptionStatus Status { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public int UsageSequence { get; private set; }
    public string? ReasonCode { get; private set; }
    public Guid? CustomerId { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public decimal DiscountAmount { get; private set; }
    public string Currency { get; private set; } = default!;
    public string BreakdownJson { get; private set; } = "{}";
    public int TreeVersion { get; private set; }
    public DateTime EvaluatedAtUtc { get; private set; }

    protected PromotionRedemption()
    {
    }

    public static PromotionRedemption TaoMoi(
        Guid id,
        Guid? tenantId,
        Guid promotionId,
        Guid? voucherCodeId,
        Guid? orderId,
        PromotionRedemptionStatus status,
        DateTime? expiresAtUtc,
        int usageSequence,
        Guid? customerId,
        string idempotencyKey,
        decimal discountAmount,
        string currency,
        string breakdownJson,
        int treeVersion,
        DateTime evaluatedAtUtc)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            PromotionId = promotionId,
            VoucherCodeId = voucherCodeId,
            OrderId = orderId,
            Status = status,
            ExpiresAtUtc = expiresAtUtc,
            UsageSequence = usageSequence,
            CustomerId = customerId,
            IdempotencyKey = idempotencyKey,
            DiscountAmount = discountAmount,
            Currency = currency,
            BreakdownJson = breakdownJson,
            TreeVersion = treeVersion,
            EvaluatedAtUtc = evaluatedAtUtc,
            AppliedAtUtc = status == PromotionRedemptionStatus.Applied ? evaluatedAtUtc : null
        };
}
