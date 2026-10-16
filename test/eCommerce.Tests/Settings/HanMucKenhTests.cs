using eCommerce.Settings;

namespace eCommerce.Tests.Settings;

/// <summary>
/// 🚨 **CƠ CHẾ CHO "HẠN MỨC THEO KÊNH" (`INV-003` — phần cưỡng chế)**.
///
/// **VÌ SAO CẦN:** hạn mức là **số học nghiệp vụ** nằm **trong đường đặt hàng** (nơi sai một đơn vị là **chặn oan** hoặc **bán quá**).
/// Tách ra hàm **thuần** ⇒ kiểm được **trước** khi tin nó ở `StockReservationService`.
///
/// 🔒 **Luật kiểm ở đây:**
///   (1) `Absolute` ⇒ hạn mức = giá trị cấu hình, **không** phụ thuộc tồn kho;
///   (2) `Percent` ⇒ `floor(tồnKhảDụng × giá trị / 100)` — **làm tròn XUỐNG** (làm tròn lên = cho giữ **nhiều hơn** tỷ lệ đã chọn);
///   (3) `giá trị &lt;= 0` ⇒ hạn mức **0** = **TẮT** chính sách ⇒ **không bao giờ chặn**;
///   (4) **`Block` mới chặn**, `Warn` **không** chặn (đúng lựa chọn của người vận hành);
///   (5) 🔥 **`HanMucVoNghia`**: cấu hình **có ý định** áp (`> 0`) mà hạn mức ra **0** ⇒ phải **phát hiện được**
///       (nếu không: người vận hành đặt `50%`, thấy **không chặn gì**, rồi **tin** hạn mức đang chạy).
/// </summary>
public class HanMucKenhTests
{
    // ── (1) TUYỆT ĐỐI ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5, 0, 5)]
    [InlineData(5, 100, 5)]
    [InlineData(2.9, 100, 2)]   // làm tròn XUỐNG cả với `Absolute` (không cho thêm nửa đơn vị)
    public void TinhHanMuc_ABSOLUTE_lay_dung_gia_tri_cau_hinh__khong_phu_thuoc_ton(double giaTri, int ton, int mongDoi)
    {
        var hanMuc = HanMucKenh.TinhHanMuc(DonViHanMuc.Absolute, (decimal)giaTri, ton);

        Assert.Equal(mongDoi, hanMuc);
    }

    // ── (2) PHẦN TRĂM ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, 100, 10)]
    [InlineData(50, 10, 5)]
    [InlineData(33, 10, 3)]     // 3,3 ⇒ 3 (XUỐNG)
    [InlineData(100, 7, 7)]
    public void TinhHanMuc_PERCENT_lay_ty_le_tren_ton_kha_dung__lam_tron_XUONG(double phanTram, int ton, int mongDoi)
    {
        var hanMuc = HanMucKenh.TinhHanMuc(DonViHanMuc.Percent, (decimal)phanTram, ton);

        Assert.Equal(mongDoi, hanMuc);
    }

    [Fact]
    public void TinhHanMuc_PERCENT_tren_ton_am_hoac_0__tra_0()
    {
        Assert.Equal(0, HanMucKenh.TinhHanMuc(DonViHanMuc.Percent, 50m, 0));
        Assert.Equal(0, HanMucKenh.TinhHanMuc(DonViHanMuc.Percent, 50m, -3));
    }

    // ── (3) TẮT CHÍNH SÁCH + (4) BLOCK vs WARN ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Gia_tri_KHONG_DUONG__la_TAT_chinh_sach__khong_bao_gio_chan(double giaTri)
    {
        var hanMuc = HanMucKenh.TinhHanMuc(DonViHanMuc.Absolute, (decimal)giaTri, 100);

        Assert.Equal(0, hanMuc);
        Assert.False(HanMucKenh.VuotHanMuc(soLuongYeuCau: 999, hanMuc: hanMuc));
        Assert.False(HanMucKenh.NenChan(HanhDongHanMuc.Block, soLuongYeuCau: 999, hanMuc: hanMuc));
    }

    [Fact]
    public void BLOCK__chan_khi_VUOT__khong_chan_khi_BANG_hoac_DUOI()
    {
        Assert.True(HanMucKenh.NenChan(HanhDongHanMuc.Block, soLuongYeuCau: 6, hanMuc: 5));
        Assert.False(HanMucKenh.NenChan(HanhDongHanMuc.Block, soLuongYeuCau: 5, hanMuc: 5));  // BẰNG ⇒ cho qua
        Assert.False(HanMucKenh.NenChan(HanhDongHanMuc.Block, soLuongYeuCau: 4, hanMuc: 5));
    }

    [Fact]
    public void WARN__KHONG_bao_gio_chan_du_vuot_han_muc()
    {
        Assert.True(HanMucKenh.VuotHanMuc(soLuongYeuCau: 99, hanMuc: 1));      // vẫn NHẬN BIẾT là vượt…
        Assert.False(HanMucKenh.NenChan(HanhDongHanMuc.Warn, 99, 1));          // …nhưng KHÔNG chặn
    }

    // ── (5) 🔥 PHÁT HIỆN "TẮT IM LẶNG" ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 1)]     // 50% của 1 = 0,5 ⇒ 0 ⇒ TẮT
    [InlineData(10, 9)]     // 10% của 9 = 0,9 ⇒ 0 ⇒ TẮT
    [InlineData(1, 50)]     // 1% của 50 = 0,5 ⇒ 0 ⇒ TẮT
    public void HanMucVoNghia_PHAT_HIEN_duoc_cau_hinh_co_y_dinh_ap_nhung_han_muc_ra_0(double phanTram, int ton)
    {
        Assert.True(HanMucKenh.HanMucVoNghia(DonViHanMuc.Percent, (decimal)phanTram, ton));
    }

    [Theory]
    [InlineData(50, 10)]    // 5 > 0 ⇒ có hạn mức thật
    [InlineData(100, 1)]    // 1 > 0
    public void HanMucVoNghia_KHONG_bao_dong_khi_han_muc_THAT_SU_duong(double phanTram, int ton)
    {
        Assert.False(HanMucKenh.HanMucVoNghia(DonViHanMuc.Percent, (decimal)phanTram, ton));
    }

    [Fact]
    public void HanMucVoNghia_KHONG_bao_khi_chinh_sach_CHU_DINH_tat_gia_tri_0()
    {
        // `0` = chủ định TẮT ⇒ **không** phải cấu hình sai ⇒ KHÔNG cảnh báo (nếu cảnh báo thì sẽ ồn ào vô ích mỗi lượt giữ chỗ).
        Assert.False(HanMucKenh.HanMucVoNghia(DonViHanMuc.Absolute, 0m, 100));
        Assert.False(HanMucKenh.HanMucVoNghia(DonViHanMuc.Percent, 0m, 100));
    }
}
