using eCommerce.Inventory;
using eCommerce.Settings;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **entity hạn mức theo kênh** (`INV-003` v2 — `InvChannelLimits`).
///
/// **VÌ SAO KIỂM Ở TẦNG ENTITY:** đây là **dữ liệu cấu hình do người vận hành nhập** ⇒ chỗ dễ sinh
/// *"cấu hình sai mà im lặng"*. Ví dụ: mã kênh có khoảng trắng thừa ⇒ lúc giữ chỗ so khớp **không trúng**
/// ⇒ hạn mức **không bao giờ** áp, và **không có lỗi nào**. 🔒 Vì vậy: **chuẩn hoá ở MỘT chỗ** (hàm tạo)
/// và **NÉM** khi dữ liệu vô nghĩa thay vì tự đoán ý người nhập.
/// </summary>
public class InvChannelLimitTests
{
    private static readonly Guid Kho = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Ma_kenh_duoc_CHUAN_HOA__trim_va_chu_thuong__o_MOT_cho_duy_nhat()
    {
        var hm = new InvChannelLimit(Guid.NewGuid(), "  Shopee  ", 10m);

        // 🔑 Đây là điều kiện để `StockReservationService` (so khớp `x.ChannelCode == kenhChuanHoa`) trúng.
        Assert.Equal("shopee", hm.ChannelCode);
        Assert.Equal("shopee", InvChannelLimit.ChuẩnHoáKenh("SHOPEE"));
        Assert.Equal("shopee", InvChannelLimit.ChuẩnHoáKenh(" shopee "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Thieu_ma_kenh__NEM_loi_nghiep_vu__khong_tao_dong_vo_nghia(string? maKenh)
    {
        // Một dòng hạn mức KHÔNG gắn kênh sẽ KHÔNG BAO GIỜ khớp lúc giữ chỗ ⇒ người vận hành tưởng đã siết kênh.
        var loi = Assert.Throws<BusinessException>(() => new InvChannelLimit(Guid.NewGuid(), maKenh!, 10m));

        Assert.Equal(InventoryErrorCodes.HanMucKenhThieuMaKenh, loi.Code);
    }

    [Fact]
    public void Gia_tri_AM__NEM_loi_nghiep_vu__vi_am_va_0_cung_nghia_la_TAT()
    {
        // Cho phép số âm ⇒ hai cách diễn đạt cùng một ý (âm và 0) ⇒ chỗ đọc hiểu khác nhau ⇒ hạn mức
        // âm thầm khác nhau giữa các đường. Chọn: TỪ CHỐI, không đoán.
        var loi = Assert.Throws<BusinessException>(() => new InvChannelLimit(Guid.NewGuid(), "shopee", -1m));

        Assert.Equal(InventoryErrorCodes.HanMucKenhGiaTriAm, loi.Code);

        // Và cùng luật cho đường SỬA (không chỉ đường tạo) — nếu không thì sửa được giá trị mà tạo thì không.
        var hm = new InvChannelLimit(Guid.NewGuid(), "shopee", 10m);
        var loiSua = Assert.Throws<BusinessException>(() => hm.DatHanMuc(-0.0001m, HanhDongHanMuc.Block));

        Assert.Equal(InventoryErrorCodes.HanMucKenhGiaTriAm, loiSua.Code);
        Assert.Equal(10m, hm.GiaTri);   // 🔒 KHÔNG thay đổi gì khi ném (bài học "ném mà để lại rác là gieo bug")
    }

    [Fact]
    public void Gia_tri_0__la_TAT__KHONG_phai_loi()
    {
        var hm = new InvChannelLimit(Guid.NewGuid(), "shopee", 0m);

        Assert.Equal(0m, hm.GiaTri);
        Assert.False(hm.IsDeleted);
    }

    [Fact]
    public void WarehouseId_null__nghia_la_han_muc_CA_KENH()
    {
        var caKenh = new InvChannelLimit(Guid.NewGuid(), "shopee", 10m);
        var theoKho = new InvChannelLimit(Guid.NewGuid(), "shopee", 4m, Kho);

        Assert.Null(caKenh.WarehouseId);
        Assert.Equal(Kho, theoKho.WarehouseId);
    }

    [Fact]
    public void Hanh_dong_mac_dinh_la_BLOCK__fail_closed()
    {
        var hm = new InvChannelLimit(Guid.NewGuid(), "shopee", 10m);

        // 🔒 Mặc định phải là lựa chọn ÍT RỦI RO NHẤT: không bán quá hạn mức. Muốn nới thì chọn `Warn` TƯỜNG MINH.
        Assert.Equal(HanhDongHanMuc.Block, hm.HanhDong);
    }

    [Fact]
    public void DatHanMuc__doi_duoc_gia_tri_va_hanh_dong()
    {
        var hm = new InvChannelLimit(Guid.NewGuid(), "shopee", 10m);

        hm.DatHanMuc(25m, HanhDongHanMuc.Warn);

        Assert.Equal(25m, hm.GiaTri);
        Assert.Equal(HanhDongHanMuc.Warn, hm.HanhDong);
    }

    [Fact]
    public void Tat__dua_gia_tri_ve_0__giu_dong_lai_de_con_vet_cau_hinh()
    {
        var hm = new InvChannelLimit(Guid.NewGuid(), "shopee", 10m);

        hm.Tat();

        Assert.Equal(0m, hm.GiaTri);
        // 📌 Cố ý KHÔNG xoá dòng: người vận hành cần thấy "kênh này TỪNG có hạn mức, nay tắt"
        // (nếu xoá hẳn thì lịch sử chặn đơn `0042` không còn gì để đối chiếu).
    }

    [Fact]
    public void Do_dai_ma_kenh_toi_da__32__dong_bo_voi_ChannelCode_cua_Order_va_SoCai()
    {
        Assert.Equal(32, InvChannelLimit.DoDaiMaKenhToiDa);
    }
}
