using eCommerce.Kernel.Money;
using Volo.Abp;

namespace eCommerce.Promotions.Engine;

/// <summary>
/// **TÍNH SỐ TIỀN GIẢM** của một candidate (hàm thuần, `Money` + `IRoundingPolicy`).
/// `Percentage` **luôn trên `Subtotal` gốc** (`D-160`) và **làm tròn đúng MỘT lần** theo loại tiền (`TC-48`);
/// trần `MaxDiscountAmount` áp **sau** khi tính (`D-158`). `FreeShipping` bị chặn ở v1 ⇒ luôn 0.
/// </summary>
public static class PromotionDiscountCalculator
{
    public static Money Tinh(
        PromotionDiscountKind kind,
        decimal discountValue,
        decimal? maxDiscountAmount,
        Money subtotal,
        IRoundingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(subtotal);
        ArgumentNullException.ThrowIfNull(policy);

        switch (kind)
        {
            case PromotionDiscountKind.Percentage:
            {
                var tho = subtotal * (discountValue / 100m);
                var lamTron = policy.Round(tho);

                if (maxDiscountAmount is not null)
                {
                    var tran = Money.Cua(maxDiscountAmount.Value, subtotal.Currency);
                    if (lamTron.CompareTo(tran) > 0)
                    {
                        return tran;
                    }
                }

                return lamTron;
            }

            case PromotionDiscountKind.FixedAmount:
                return policy.Round(Money.Cua(discountValue, subtotal.Currency));

            default:
                // `D-291` ③ P3 #8 — **NÉM** lỗi cấu hình (fail-closed), KHÔNG trả `Money.Zero` im lặng cho
                // giá trị lạ. `FreeShipping` bị `CHECK` + validation chặn từ `CreateAsync` (`D-158`) nên chỉ
                // tới được đây khi cấu hình/enum hỏng — thà đỏ còn hơn giảm 0 đồng im lặng.
                throw new AbpException(
                    $"Kiểu giảm giá không hỗ trợ ở v1: '{kind}' (chỉ Percentage/FixedAmount — PRO-002 D-151).");
        }
    }
}
