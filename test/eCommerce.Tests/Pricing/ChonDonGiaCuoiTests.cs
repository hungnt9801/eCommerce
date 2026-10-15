using eCommerce.Kernel.Money;
using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// Test **chọn đơn giá cuối** (`PRC-002` **B5**) — hàm **THUẦN**.
///
/// 📌 **Luật:** bậc thang **thắng** giá cơ sở khi có hiệu lực cho số lượng này; không có ⇒ dùng giá cơ sở.
/// Quyết định này **tách khỏi** `GiaCoSoResolver` **có chủ ý**: nếu bậc thang nằm cùng "cấp" với giá bảng giá
/// mà `Priority` bằng nhau thì `GiaCoSoResolver` **NÉM `Pricing:0001`** ⇒ biến tính năng giảm giá theo số lượng
/// thành **sự cố checkout chết**.
/// </summary>
public class ChonDonGiaCuoiTests
{
    [Fact]
    public void KHONG_co_bac_thang_thi_dung_gia_co_so__giu_nguyen_nguon_va_cap()
    {
        var kq = ChonDonGiaCuoi.Chon(Money.Vnd(100_000), "PriceList:BG-1#SKU-1", "BangGia", null);

        Assert.Equal(100_000m, kq.DonGia.Amount);
        Assert.Equal("PriceList:BG-1#SKU-1", kq.Nguon);
        Assert.Equal("BangGia", kq.Cap);
    }

    [Fact]
    public void CO_bac_thang_thi_bac_thang_THANG__va_cap_doi_thanh_BacThang()
    {
        var bac = new ChonBacThang.Bac(10, Money.Vnd(90_000), "TierPriceSet:BT-1#10");

        var kq = ChonDonGiaCuoi.Chon(Money.Vnd(100_000), "PriceList:BG-1#SKU-1", "BangGia", bac);

        Assert.Equal(90_000m, kq.DonGia.Amount);
        Assert.Equal("TierPriceSet:BT-1#10", kq.Nguon);
        Assert.Equal("BacThang", kq.Cap);
    }

    [Fact]
    public void Bac_thang_LECH_loai_tien_thi_NEM__cam_tu_quy_doi()
    {
        var bac = new ChonBacThang.Bac(1, Money.Usd(10), "n");

        Assert.Throws<ArgumentException>(
            () => ChonDonGiaCuoi.Chon(Money.Vnd(100_000), "nguon", "BangGia", bac));
    }

    [Fact]
    public void Bac_thang_dat_HON_gia_co_so_van_THANG__do_la_quyet_dinh_cua_nguoi_cau_hinh()
    {
        // 📌 Ghi nhận rõ: hệ thống **không** tự "chọn giá rẻ hơn" — nó chọn theo **luật đã công bố**.
        // Tự chọn giá rẻ hơn nghe "có lợi cho khách" nhưng làm **doanh thu không dự đoán được** và
        // người cấu hình **không hiểu vì sao** giá ra như vậy.
        var bac = new ChonBacThang.Bac(5, Money.Vnd(120_000), "TierPriceSet:BT-1#5");

        var kq = ChonDonGiaCuoi.Chon(Money.Vnd(100_000), "PriceList:BG-1#SKU-1", "BangGia", bac);

        Assert.Equal(120_000m, kq.DonGia.Amount);
    }
}
