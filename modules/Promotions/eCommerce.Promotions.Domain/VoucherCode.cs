using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Promotions;

/// <summary>
/// **Mã giảm giá** (`CR-2`, bảng `ProVoucherCodes`). Bộ đếm `UsedCount + ReservedCount ≤ MaxUsage` được cưỡng chế
/// bằng **một** `CHECK` ở DB (thay bản cũ — `ADR-09`, `CR-3`); **sổ cái** `ProPromotionRedemptions` mới là nguồn sự thật.
/// </summary>
public class VoucherCode : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid PromotionId { get; private set; }
    public string Code { get; private set; } = default!;
    public int MaxUsage { get; private set; }
    public int UsedCount { get; private set; }
    public int ReservedCount { get; private set; }
    public VoucherCodeStatus Status { get; private set; }
    public byte[]? RowVersion { get; private set; }

    protected VoucherCode()
    {
    }

    public static VoucherCode TaoMoi(Guid id, Guid? tenantId, Guid promotionId, string code, int maxUsage, VoucherCodeStatus status = VoucherCodeStatus.Generated)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            PromotionId = promotionId,
            Code = code.Trim(),
            MaxUsage = maxUsage,
            UsedCount = 0,
            ReservedCount = 0,
            Status = status
        };

    // ── Máy trạng thái mã (`PRO-001-arch` §3.2) — **NÉM** `0006` ở mọi chuyển sai (`BR-10`). ────────────
    // 🚨 Bộ đếm `UsedCount`/`ReservedCount` **KHÔNG** có setter công khai và **KHÔNG** đổi qua entity —
    //    mọi thay đổi đi bằng `UPDATE` có điều kiện ở service để tránh mất lượt khi hai luồng song song.

    /// <summary>`Generated → Issued`. Sai ⇒ **NÉM** `0006`.</summary>
    public void Issue(DateTime now)
    {
        _ = now;
        if (Status != VoucherCodeStatus.Generated)
        {
            NemChuyenSai(VoucherCodeStatus.Issued);
        }

        Status = VoucherCodeStatus.Issued;
    }

    /// <summary>`Issued → Revoked`. Sai ⇒ **NÉM** `0006` (kể cả `Generated → Revoked`).</summary>
    public void Revoke(DateTime now)
    {
        _ = now;
        if (Status != VoucherCodeStatus.Issued)
        {
            NemChuyenSai(VoucherCodeStatus.Revoked);
        }

        Status = VoucherCodeStatus.Revoked;
    }

    /// <summary>
    /// Ngoại lệ **duy nhất** của `BR-18`: đổi `MaxUsage` (không đổi giá trị/`promotion`).
    /// `newMaxUsage &lt; UsedCount + ReservedCount` ⇒ **NÉM** `0116`; `≤ 0` cũng bị từ chối.
    /// </summary>
    public void DoiGioiHan(int newMaxUsage)
    {
        if (newMaxUsage < 1 || newMaxUsage < UsedCount + ReservedCount)
        {
            throw new BusinessException(PromotionErrorCodes.OverrideLimitTooLow)
                .WithData("id", Id)
                .WithData("newMaxUsage", newMaxUsage)
                .WithData("daDung", UsedCount + ReservedCount);
        }

        MaxUsage = newMaxUsage;
    }

    private void NemChuyenSai(VoucherCodeStatus den)
        => throw new BusinessException(PromotionErrorCodes.InvalidStatusTransition)
            .WithData("from", Status.ToString())
            .WithData("to", den.ToString());
}
