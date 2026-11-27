using eCommerce.Kernel.Money;
using eCommerce.Promotions.Engine;
using Volo.Abp;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **`TC-76`** (`D-291` ③ P3 #8) — nhánh `default` của `PromotionDiscountCalculator` phải **NÉM** lỗi cấu hình
/// (fail-closed), **KHÔNG** trả `Money.Zero` im lặng: một `DiscountKind` lạ (hoặc `FreeShipping` bị chặn ở v1)
/// giảm 0 đồng mà không ai biết là lỗi tiền tệ.
/// </summary>
public class PromotionDiscountCalculatorTests
{
    private static readonly Money Subtotal = Money.Cua(100_000m, Currency.Vnd);

    [Fact]
    public async Task TC76_kieu_giam_gia_la__nem_khong_tra_Money_Zero()
    {
        // `(PromotionDiscountKind)999` — giá trị không có trong enum (mô phỏng enum thêm sau/bị hỏng).
        await Assert.ThrowsAsync<AbpException>(() => Task.FromResult(
            PromotionDiscountCalculator.Tinh(
                (PromotionDiscountKind)999, discountValue: 10m, maxDiscountAmount: null,
                Subtotal, new RoundHalfAwayFromZeroPolicy())));
    }

    [Fact]
    public async Task TC76_FreeShipping_bi_chan_o_v1__nem_khong_im_lang_0_dong()
    {
        await Assert.ThrowsAsync<AbpException>(() => Task.FromResult(
            PromotionDiscountCalculator.Tinh(
                PromotionDiscountKind.FreeShipping, discountValue: 10m, maxDiscountAmount: null,
                Subtotal, new RoundHalfAwayFromZeroPolicy())));
    }

    [Fact]
    public void TC76_hai_kieu_hop_le_van_tinh_dung()
    {
        // Đối chứng: `Percentage`/`FixedAmount` vẫn chạy (không ném) — tránh test chỉ chứng minh "luôn ném".
        var phanTram = PromotionDiscountCalculator.Tinh(
            PromotionDiscountKind.Percentage, 10m, null, Subtotal, new RoundHalfAwayFromZeroPolicy());
        var soTien = PromotionDiscountCalculator.Tinh(
            PromotionDiscountKind.FixedAmount, 15_000m, null, Subtotal, new RoundHalfAwayFromZeroPolicy());

        Assert.Equal(10_000m, phanTram.Amount);
        Assert.Equal(15_000m, soTien.Amount);
    }
}
