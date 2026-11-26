using eCommerce.Application.Checkout;
using eCommerce.Application.Contracts.Checkout;
using Volo.Abp;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// **D6** (`PRO-002` §12.1): client gửi `CheckoutInput.OrderDiscount ≠ 0` ⇒ **NÉM** trước mọi tác dụng phụ
/// ⇒ **0 đơn / 0 giữ chỗ**. Test dựng `CheckoutAppService` với `null!` cho phụ thuộc vì guard chạy **trước**
/// khi chạm bất kỳ phụ thuộc nào.
/// </summary>
public class PromotionCheckoutGuardTests
{
    private static CheckoutAppService Service()
        => new(null!, null!, null!, null!, null!);

    private static CheckoutInput Input(decimal orderDiscount) => new()
    {
        ChannelCode = "web",
        CustomerId = null,
        WarehouseId = Guid.NewGuid(),
        Items = new List<DongCheckoutDto> { new() { Sku = "SKU-1", Quantity = 1, ListPrice = 100_000m } },
        OrderDiscount = orderDiscount,
        IdempotencyKey = "checkout-1"
    };

    [Fact]
    public async Task OrderDiscount_khac_0__nem_400_va_khong_giau_cho()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Service().TinhVaGiuChoAsync(Input(5m)));

        Assert.Equal(eCommerce.Orders.SalesErrorCodes.KhongNhanGiamGiaNhapTay, ex.Code);
    }

    [Fact]
    public async Task OrderDiscount_0__KHONG_bi_chan_boi_guard_D6()
    {
        // Guard D6 KHÔNG ném. (Với phụ thuộc null, lỗi khác sẽ xảy ra muộn hơn — ta chỉ khẳng định KHÔNG
        // phải lỗi D6.)
        Exception? loi = null;
        try
        {
            await Service().TinhVaGiuChoAsync(Input(0m));
        }
        catch (Exception ex)
        {
            loi = ex;
        }

        Assert.False(loi is BusinessException be
                     && be.Code == eCommerce.Orders.SalesErrorCodes.KhongNhanGiamGiaNhapTay);
    }

    [Fact]
    public void Co_gat_Promotions_Enabled_trong_Checkout()
    {
        var goc = new DirectoryInfo(AppContext.BaseDirectory);
        while (goc is not null && !File.Exists(Path.Combine(goc.FullName, "eCommerce.slnx")))
        {
            goc = goc.Parent;
        }

        var src = File.ReadAllText(Path.Combine(
            goc!.FullName, "modules/Sales/eCommerce.Sales.Application/CheckoutAppService.cs"));

        Assert.Contains("PromotionContractConsts.EnabledSettingName", src);
        Assert.Contains("giamGiaKhuyenMai", src);
    }
}
