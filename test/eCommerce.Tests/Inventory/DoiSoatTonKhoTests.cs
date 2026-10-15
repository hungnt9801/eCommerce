using eCommerce.Inventory;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **đối soát tồn kho** (`INV-001` **ADR-10**) — hàm **THUẦN** tính tổng có dấu của sổ cái.
///
/// 🚨 **VÌ SAO QUAN TRỌNG:** nếu phép đối soát **sai dấu** hoặc **cộng nhầm `Reservation`** thì nó **báo lệch GIẢ**
/// liên tục ⇒ đội vận hành **mất tin** vào cảnh báo ⇒ **cảnh báo thật sau này bị bỏ qua**. Đó là cái chết điển hình
/// của hệ thống giám sát (*"alert fatigue"*) — và nó tệ hơn **không có** cảnh báo.
/// </summary>
public class DoiSoatTonKhoTests
{
    [Theory]
    [InlineData(StockMovementType.Receipt)]
    [InlineData(StockMovementType.CustomerReturn)]
    [InlineData(StockMovementType.Adjustment)]
    [InlineData(StockMovementType.Transfer)]
    [InlineData(StockMovementType.StocktakeGain)]
    [InlineData(StockMovementType.TransferIn)]
    public void Cac_loai_LAM_TANG_OnHand_co_dau_DUONG(StockMovementType loai)
    {
        Assert.Equal(+1, DoiSoatTonKho.Dau(loai));
        Assert.True(DoiSoatTonKho.AnhHuongOnHand(loai));
    }

    [Theory]
    [InlineData(StockMovementType.Sale)]
    [InlineData(StockMovementType.StocktakeLoss)]
    [InlineData(StockMovementType.TransferOut)]
    public void Cac_loai_LAM_GIAM_OnHand_co_dau_AM(StockMovementType loai)
    {
        Assert.Equal(-1, DoiSoatTonKho.Dau(loai));
        Assert.True(DoiSoatTonKho.AnhHuongOnHand(loai));
    }

    [Theory]
    [InlineData(StockMovementType.Reservation)]
    [InlineData(StockMovementType.ReservationRelease)]
    public void GIU_CHO_KHONG_anh_huong_OnHand__dau_BANG_0(StockMovementType loai)
    {
        // 🚨 Đây là luật dễ sai nhất: `Reservation` đổi **`Reserved`**, KHÔNG đổi `OnHand`.
        // Nếu cộng cả nó vào tổng thì mọi đơn có giữ chỗ đều làm phép đối soát **báo lệch giả**.
        Assert.Equal(0, DoiSoatTonKho.Dau(loai));
        Assert.False(DoiSoatTonKho.AnhHuongOnHand(loai));
    }

    [Fact]
    public void Tong_co_dau__nhap_100_xuat_30_chuyen_di_20_chuyen_den_20_thi_bang_70()
    {
        var butToan = new List<(StockMovementType, int)>
        {
            (StockMovementType.Receipt, 100),
            (StockMovementType.Sale, 30),
            (StockMovementType.TransferOut, 20),   // kho nguồn: -20
            (StockMovementType.TransferIn, 20),    // kho đích:  +20  (khác kho ⇒ tính riêng từng kho)
        };

        Assert.Equal(70, DoiSoatTonKho.Tong(butToan));
    }

    [Fact]
    public void Tong_BO_QUA_giu_cho__khong_lam_lech_ket_qua()
    {
        var khongGiuCho = DoiSoatTonKho.Tong([(StockMovementType.Receipt, 10), (StockMovementType.Sale, 2)]);
        var coGiuCho = DoiSoatTonKho.Tong(
        [
            (StockMovementType.Receipt, 10),
            (StockMovementType.Sale, 2),
            (StockMovementType.Reservation, 5),         // đổi `Reserved`, KHÔNG đổi `OnHand`
            (StockMovementType.ReservationRelease, 5)
        ]);

        Assert.Equal(8, khongGiuCho);
        Assert.Equal(8, coGiuCho);   // 🚨 PHẢI BẰNG NHAU — nếu khác thì phép đối soát báo lệch giả
    }

    [Fact]
    public void Tong_rong_thi_bang_0()
    {
        Assert.Equal(0, DoiSoatTonKho.Tong([]));
    }

    [Fact]
    public void Loai_la_thi_NEM__khong_im_lang_bo_qua()
    {
        // 🚨 Nếu gặp loại lạ mà **bỏ qua im lặng** thì một loại mới thêm sau này sẽ bị **không tính**
        // ⇒ đối soát báo lệch sai ở MỌI cặp có loại đó, mà không ai biết vì sao.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DoiSoatTonKho.Dau((StockMovementType)999));
    }

    [Fact]
    public void ChenhLech_duong_la_anh_chup_THUA__am_la_THIEU()
    {
        Assert.Equal(5, new DoiSoatTonKho.Lech(Guid.NewGuid(), "SKU-1", OnHand: 105, TongSoCai: 100).ChenhLech);
        Assert.Equal(-5, new DoiSoatTonKho.Lech(Guid.NewGuid(), "SKU-1", OnHand: 95, TongSoCai: 100).ChenhLech);
    }
}
