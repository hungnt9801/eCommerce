using eCommerce.Kernel.Money;

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
                return Money.Zero(subtotal.Currency);
        }
    }
}
