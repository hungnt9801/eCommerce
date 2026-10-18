using eCommerce.Inventory;
using eCommerce.Reconciliation;
using eCommerce.Settings;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🆕 `S7` — **RETENTION LỊCH SỬ ĐỐI SOÁT** (`REC-001` §17.8 #1 — nợ *"bảng chỉ tăng"*).
///
/// **VÌ SAO MỌI CA PHẢI LÀ HÀM THUẦN:** luật này **XOÁ chứng từ**. 📌 Một luật xoá dữ liệu chỉ kiểm được bằng
/// DB thật sẽ **không** được kiểm thường xuyên — và đúng loại luật **không được kiểm** là loại gây mất dữ liệu.
/// 🔒 Vì vậy toàn bộ quyết định *"dòng này có được xoá không"* nằm ở `DoiSoatRetention` (3 tham số), còn job
/// và API chỉ **gọi** nó.
///
/// 🚨 **ĐO CẢ HAI CHIỀU Ở MỌI CA:** *"luôn giữ"* và *"luôn xoá"* **trông giống hệt nhau** nếu chỉ kiểm một phía
/// (bài học `handover` §5.2). Vì thế mỗi bất biến dưới đây có **ít nhất một ca giữ** và **một ca xoá**.
/// </summary>
public class DoiSoatRetentionTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);

    // ── MỐC CẮT ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TinhMocUtc__lui_dung_so_ngay()
    {
        Assert.Equal(Now.AddDays(-180), DoiSoatRetention.TinhMocUtc(Now, 180));
        Assert.Equal(Now.AddDays(-1), DoiSoatRetention.TinhMocUtc(Now, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TinhMocUtc__so_ngay_KHONG_DUONG__NEM_0006__vi_0_la_TAT_chu_khong_phai_XOA_HET(int soNgay)
    {
        // 🚨 Đây là ca nguy hiểm nhất của feature: nếu `0` được hiểu là "giữ 0 ngày" thì một lần gõ sai
        // trong ô cấu hình sẽ **xoá sạch bằng chứng đối soát** — và **không có lỗi nào** để lần ra.
        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => DoiSoatRetention.TinhMocUtc(Now, soNgay));

        Assert.Equal(ReconErrorCodes.ChinhSachLuuTruKhongHopLe, ex.Code);
    }

    // ── LUẬT "MỘT DÒNG CÓ ĐƯỢC DỌN KHÔNG" ──────────────────────────────────────────────────────

    private static bool Duoc(DateTime tao, bool daXuLy = true, bool choXacNhan = false)
        => DoiSoatRetention.DuocDonDep(tao, DoiSoatRetention.TinhMocUtc(Now, 180), daXuLy, choXacNhan);

    [Fact]
    public void Dong_DA_XU_LY_va_CU_hon_moc__DUOC_don()
    {
        Assert.True(Duoc(Now.AddDays(-200)));
    }

    [Fact]
    public void Dong_CHUA_XU_LY_du_cu_hon_moc__KHONG_bao_gio_bi_don()
    {
        // Việc còn nợ. 📌 Xoá nó làm màn hình *"sạch"* trong khi lệch **vẫn còn** — đúng kiểu hỏng
        // mà cả `REC-001` tồn tại để chặn.
        Assert.False(Duoc(Now.AddDays(-9999), daXuLy: false));
    }

    [Fact]
    public void Dong_cua_luot_DANG_CHO_XAC_NHAN__KHONG_bi_don_du_da_xu_ly_va_rat_cu()
    {
        // Cảnh báo chưa được người có quyền đóng (`ReconRun.XacNhan`) ⇒ giữ bằng chứng.
        Assert.False(Duoc(Now.AddDays(-9999), daXuLy: true, choXacNhan: true));
    }

    [Fact]
    public void Dong_MOI_hon_moc__KHONG_bi_don()
    {
        Assert.False(Duoc(Now.AddDays(-179)));
        Assert.False(Duoc(Now));
    }

    [Fact]
    public void Dong_DUNG_BANG_MOC__KHONG_bi_don__so_sanh_la_NGHIEM_NGAT()
    {
        // 📌 `< moc` (không phải `<=`): mốc là "trước mốc". Ca này khoá chiều của phép so sánh — nếu ai đó
        // đổi thành `<=` thì dòng ở đúng mốc bị xoá **sớm một nhịp**, và **không ai thấy** (chỉ lệch 1 dòng).
        var moc = DoiSoatRetention.TinhMocUtc(Now, 180);

        Assert.False(DoiSoatRetention.DuocDonDep(moc, moc, daXuLy: true, luotDangChoXacNhan: false));
        Assert.True(DoiSoatRetention.DuocDonDep(moc.AddTicks(-1), moc, daXuLy: true, luotDangChoXacNhan: false));
    }

    [Fact]
    public void Moc_lui_theo_dung_so_ngay__nguong_180_va_30_cho_ket_qua_KHAC_nhau_tren_cung_mot_dong()
    {
        // 🚨 Chống "hằng số trá hình": nếu ai đó viết cứng `AddDays(-180)` trong hàm, ca này ĐỎ.
        // 📌 Cùng một dòng 60 ngày tuổi: bị dọn khi chính sách 30 ngày, được giữ khi chính sách 180 ngày.
        var dong = Now.AddDays(-60);

        var moc30 = DoiSoatRetention.TinhMocUtc(Now, 30);
        var moc180 = DoiSoatRetention.TinhMocUtc(Now, 180);

        Assert.True(DoiSoatRetention.DuocDonDep(dong, moc30, daXuLy: true, luotDangChoXacNhan: false));
        Assert.False(DoiSoatRetention.DuocDonDep(dong, moc180, daXuLy: true, luotDangChoXacNhan: false));
    }

    // ── ĐỌC SETTING ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("30", 30)]
    [InlineData("0", 0)]         // 🔒 `0` = TẮT, và là giá trị HỢP LỆ ở tầng cấu hình
    [InlineData("180.9", 180)]   // cắt phần thập phân, không làm tròn lên (không giữ dữ liệu LÂU hơn ý người dùng)
    public void DocSoNgayLuuTru__doc_dung_gia_tri_va_coi_0_la_TAT(string tho, int mongDoi)
    {
        var soNgay = ChinhSach.DocSoNgayLuuTru(tho, out var hopLe);

        Assert.Equal(mongDoi, soNgay);
        Assert.True(hopLe);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DocSoNgayLuuTru__setting_RONG__dung_mac_dinh_NHUNG_bao_KHONG_hop_le(string? tho)
    {
        // 📌 **Vì sao RỖNG là "không hợp lệ" (không phải "chưa đặt ⇒ hợp lệ"):** setting này **có**
        // `SettingDefinition` với `DefaultValue = 180` ⇒ `ISettingProvider` **luôn** trả về `180` khi chưa ai
        // đặt. Rỗng chỉ xảy ra khi **định nghĩa setting biến mất** (hoặc DB bị sửa tay) — tức là một ca
        // cấu hình **có vấn đề**, và màn hình admin phải **nói ra** thay vì im lặng dùng mặc định.
        var soNgay = ChinhSach.DocSoNgayLuuTru(tho, out var hopLe);

        Assert.Equal(DoiSoatRetention.SoNgayMacDinh, soNgay);
        Assert.False(hopLe);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("0,5")]   // dấu phẩy: `DocSo` từ chối (đã trả giá: "0,5" từng bị đọc thành 5)
    public void DocSoNgayLuuTru__gia_tri_LA__dung_mac_dinh_va_bao_KHONG_hop_le(string tho)
    {
        var soNgay = ChinhSach.DocSoNgayLuuTru(tho, out var hopLe);

        Assert.Equal(DoiSoatRetention.SoNgayMacDinh, soNgay);
        Assert.False(hopLe);
    }
}
