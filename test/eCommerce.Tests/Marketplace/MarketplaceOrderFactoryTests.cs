using eCommerce.ChannelHub.Events;
using eCommerce.Marketplace;
using Volo.Abp;

namespace eCommerce.Tests.Marketplace;

/// <summary>
/// Test cho **lõi thuần** dựng đơn từ sự kiện kéo đơn của sàn (`MarketplaceOrderFactory`) — `SAL-005`.
///
/// 🚨 TEST QUAN TRỌNG NHẤT Ở ĐÂY LÀ **D9 — "trừ giảm giá HAI LẦN"**.
/// `OrderLine.LineTotal` **đã trừ** `DiscountAmount` của dòng, rồi `Order.Recalculate()` lại trừ tiếp
/// `DiscountTotal`. Nếu ai đó ghi **cùng một khoản giảm giá** vào cả hai nơi thì `GrandTotal` **thấp giả**
/// ⇒ **sai tiền, sai đối soát** — và **không có lỗi biên dịch, không exception**. Đúng họ lỗi im lặng
/// mà dự án đã trả giá nhiều lần.
/// </summary>
public class MarketplaceOrderFactoryTests
{
    /// <summary>Sinh Id tất định để test so sánh được (production dùng `IGuidGenerator`).</summary>
    private static Func<Guid> SinhId()
    {
        var dem = 0;
        return () => new Guid(++dem, 0, 0, new byte[8]);
    }

    private static ChannelOrderPulledEto SuKien(
        decimal channelDiscount = 0m,
        decimal shippingFee = 0m,
        string channelCode = "shopee",
        string externalOrderId = "SN-1",
        decimal lineDiscount = 20_000m) => new()
        {
            ChannelShopId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ChannelCode = channelCode,
            ExternalShopId = "SHOP-1",
            ExternalOrderId = externalOrderId,
            PlacedAtUtc = new DateTime(2026, 10, 15, 8, 30, 0, DateTimeKind.Utc),
            Currency = "VND",
            ShippingFee = shippingFee,
            ChannelDiscount = channelDiscount,
            BuyerNote = "Giao giờ hành chính",
            Lines =
            [
                new ChannelOrderPulledLineEto
                {
                    ExternalLineId = "L1", Sku = "SKU-A", Quantity = 2, UnitPrice = 100_000m, DiscountAmount = lineDiscount
                }
            ]
        };

    [Fact]
    public void D9_giam_gia_DONG_va_CAP_DON_khong_bi_tru_HAI_LAN()
    {
        // Dòng: 2 × 100.000 − 20.000 (giảm giá DÒNG) = 180.000
        // Giảm giá CẤP ĐƠN: 30.000 · Phí vận chuyển: 25.000
        // ✅ ĐÚNG : 180.000 − 30.000 + 0 + 25.000 = 175.000
        // ❌ SAI  : nếu ghi cùng khoản giảm giá dòng vào cả DiscountTotal ⇒ 155.000 (thấp giả 20.000)
        var order = MarketplaceOrderFactory.TaoOrder(SuKien(channelDiscount: 30_000m, shippingFee: 25_000m), SinhId());

        Assert.Equal(180_000m, order.Subtotal);        // khẳng định giảm giá DÒNG nằm ở dòng
        Assert.Equal(30_000m, order.DiscountTotal);    // khẳng định giảm giá CẤP ĐƠN nằm ở đơn
        Assert.Equal(0m, order.TaxTotal);
        Assert.Equal(25_000m, order.ShippingTotal);
        Assert.Equal(175_000m, order.GrandTotal);

        // Khẳng định NGƯỢC: con số của ca "trừ hai lần" KHÔNG được xuất hiện.
        Assert.NotEqual(155_000m, order.GrandTotal);
    }

    [Fact]
    public void Khong_co_giam_gia_thi_tong_bang_Subtotal_cong_phi_van_chuyen()
    {
        var order = MarketplaceOrderFactory.TaoOrder(SuKien(lineDiscount: 0m, shippingFee: 15_000m), SinhId());

        Assert.Equal(200_000m, order.Subtotal);
        Assert.Equal(0m, order.DiscountTotal);
        Assert.Equal(215_000m, order.GrandTotal);
    }

    [Fact]
    public void So_don_la_TAT_DINH_tu_channelCode_va_externalOrderId()
    {
        // 🎯 SAL-005 D8: hai luồng khác nhau (worker pull ở instance A, webhook ở instance B) nhận CÙNG
        // một đơn sàn phải sinh CÙNG số đơn ⇒ unique index (TenantId, Number) trở thành hàng rào thứ hai.
        var lan1 = MarketplaceOrderFactory.TaoOrder(SuKien(), SinhId());
        var lan2 = MarketplaceOrderFactory.TaoOrder(SuKien(), SinhId());

        Assert.Equal("SHOPEE-SN-1", lan1.Number);
        Assert.Equal(lan1.Number, lan2.Number);
    }

    [Fact]
    public void So_don_qua_64_ky_tu_thi_NEM_LOI_chu_KHONG_cat_bot()
    {
        // 🚨 Cắt bớt tạo ra một số "hợp lệ" nhưng SAI: hai đơn dài khác nhau có thể cắt thành cùng một số
        // ⇒ gộp hai đơn thật làm một, và **không ai phát hiện**. Vì vậy phải NÉM LỖI.
        var dai = new string('9', 60);   // "SHOPEE-" + 60 = 67 ký tự > 64

        var loi = Assert.Throws<BusinessException>(
            () => MarketplaceOrderFactory.TaoOrder(SuKien(externalOrderId: dai), SinhId()));

        Assert.Equal(MarketplaceErrorCodes.SoDonQuaDai, loi.Code);
    }

    [Fact]
    public void Don_san_KHONG_co_dong_hang_thi_NEM_LOI()
    {
        // Đơn không có dòng hàng ⇒ không thể là đơn thật. Tạo ra đơn 0 đồng rồi để nó lặng lẽ vào
        // báo cáo doanh thu là kiểu "im lặng" nguy hiểm.
        var suKien = SuKien();
        suKien.Lines.Clear();

        var loi = Assert.Throws<BusinessException>(() => MarketplaceOrderFactory.TaoOrder(suKien, SinhId()));
        Assert.Equal(MarketplaceErrorCodes.DonKhongCoDongHang, loi.Code);
    }

    [Fact]
    public void Sao_chep_dung_thong_tin_nguon_sang_don()
    {
        var order = MarketplaceOrderFactory.TaoOrder(SuKien(), SinhId());

        Assert.Equal("shopee", order.ChannelCode);
        Assert.Equal("SN-1", order.ExternalOrderId);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), order.ChannelShopId);
        Assert.Equal("VND", order.Currency);
        Assert.Equal("Giao giờ hành chính", order.Note);
        Assert.Single(order.Lines);
    }

    [Fact]
    public void Currency_rong_thi_mac_dinh_VND()
    {
        var suKien = SuKien();
        suKien.Currency = "   ";

        var order = MarketplaceOrderFactory.TaoOrder(suKien, SinhId());

        Assert.Equal("VND", order.Currency);
    }

    [Fact]
    public void Hai_dong_CUNG_SKU_nhung_KHAC_ExternalLineId_thi_thanh_HAI_dong()
    {
        // Chống trùng dòng theo `ExternalLineId` (không phải theo SKU): sàn cho phép cùng một SKU
        // xuất hiện ở hai dòng (ví dụ hai lô giá khác nhau) ⇒ gộp lại là SAI.
        var suKien = SuKien();
        suKien.Lines =
        [
            new ChannelOrderPulledLineEto { ExternalLineId = "L1", Sku = "SKU-A", Quantity = 1, UnitPrice = 100_000m },
            new ChannelOrderPulledLineEto { ExternalLineId = "L2", Sku = "SKU-A", Quantity = 1, UnitPrice = 90_000m }
        ];

        var order = MarketplaceOrderFactory.TaoOrder(suKien, SinhId());

        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(190_000m, order.Subtotal);
    }

    [Fact]
    public void Thieu_channelCode_hoac_externalOrderId_thi_NEM_LOI()
    {
        Assert.Throws<BusinessException>(
            () => MarketplaceOrderFactory.SinhSoDon("", "SN-1"));

        Assert.Throws<BusinessException>(
            () => MarketplaceOrderFactory.SinhSoDon("shopee", "  "));
    }
}
