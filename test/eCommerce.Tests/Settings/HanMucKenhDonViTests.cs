using eCommerce.Settings;

namespace eCommerce.Tests.Settings;

/// <summary>
/// 🚨 **ĐƠN VỊ (`DonVi`) THEO TỪNG DÒNG HẠN MỨC** (`INV-003` mục 5 việc 2 — nợ §16.8 #1).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO CẦN — và vì sao lỗi ở đây là loại TỆ NHẤT:**
///
/// `GiaTri` **không có nghĩa** nếu thiếu đơn vị: 📌 `100` là **100%** hay **100 chiếc**?
/// ⚠️ Và nếu hệ thống lấy **`GiaTri` của DÒNG** nhưng **`DonVi` của SETTING** thì nó đọc `100` (chiếc)
/// thành `100%` ⇒ hạn mức **không chặn gì cả** — 🚨 **và KHÔNG có lỗi nào**, không log, không ném.
///
/// 🔑 **Vì sao cặp sai đó là rủi ro THẬT (không phải giả định):** `DongHanMuc` và `KetQuaHanMuc` là **hai**
/// chỗ chứa `DonVi`, nên có **hai** đường để lấy sai. 🔒 Cách chặn: đặt việc trộn **ngay cạnh** việc chọn
/// dòng (`ChonHanMuc`) — và **khoá lại bằng test** (file này).
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
///
/// 🔒 **Luật kiểm:**
/// <list type="number">
///   <item>Dòng có `DonVi` **tường minh** ⇒ **THẮNG** setting chung (kể cả khi setting là đơn vị khác);</item>
///   <item>Dòng có `DonVi = null` ⇒ **kế thừa** setting chung; **KHÔNG** mặc định ngầm về `Percent`/`Absolute`;</item>
///   <item>🔑 **ĐƠN VỊ ĐI KÈM GIÁ TRỊ** — khi chọn dòng theo kho, `DonVi` phải là **của dòng đó**, không phải
///     của dòng cả kênh hay của setting;</item>
///   <item>Không có dòng nào khớp ⇒ `DonVi` = setting chung (nhất quán với `GiaTri` mặc định).</item>
/// </list>
/// </summary>
public class HanMucKenhDonViTests
{
    private static readonly Guid KhoA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static HanMucKenh.DongHanMuc Dong(
        Guid? kho,
        decimal giaTri,
        DonViHanMuc? donVi = null,
        HanhDongHanMuc hanhDong = HanhDongHanMuc.Block)
        => new(Guid.NewGuid(), kho, giaTri, hanhDong, donVi);

    private static HanMucKenh.KetQuaHanMuc Chon(
        PhamViHanMuc phamVi,
        IEnumerable<HanMucKenh.DongHanMuc> dong,
        DonViHanMuc donViChung,
        decimal macDinh = 0m,
        Guid? kho = null)
        => HanMucKenh.ChonHanMuc(
            phamVi, dong, kho ?? KhoA, donViChung, macDinh, HanhDongHanMuc.Block, out _);

    // ── (1) DÒNG CÓ ĐƠN VỊ TƯỜNG MINH ⇒ THẮNG SETTING ────────────────────────────────────────

    [Fact]
    public void Dong_co_DonVi__THANG__setting_chung()
    {
        // 🔑 Ca cốt lõi của cả tính năng: kênh B cam kết **số lượng tuyệt đối** trong khi chính sách chung là
        // **phần trăm**. 📌 Nếu dòng KHÔNG thắng thì `100` (chiếc) bị đọc thành `100%` ⇒ **không chặn gì**.
        var kq = Chon(PhamViHanMuc.Channel, [Dong(null, 100m, DonViHanMuc.Absolute)], DonViHanMuc.Percent);

        Assert.Equal(DonViHanMuc.Absolute, kq.DonVi);
        Assert.Equal(100m, kq.GiaTri);
    }

    [Fact]
    public void Dong_co_DonVi__THANG__theo_CHIEU_NGUOC_LAI()
    {
        // 🔒 Kiểm **cả hai chiều**: nếu chỉ kiểm một chiều thì một phép gán cứng (`DonVi = Absolute`) vẫn xanh.
        // 📌 Kênh A cam kết theo **%** trong khi chính sách chung là **số lượng**.
        var kq = Chon(PhamViHanMuc.Channel, [Dong(null, 50m, DonViHanMuc.Percent)], DonViHanMuc.Absolute);

        Assert.Equal(DonViHanMuc.Percent, kq.DonVi);
        Assert.Equal(50m, kq.GiaTri);
    }

    // ── (2) `DonVi = null` ⇒ KẾ THỪA (không mặc định ngầm) ───────────────────────────────────

    [Fact]
    public void Dong_KHONG_khai_DonVi__KE_THUA__setting_chung()
    {
        // 🔑 `null` **CÓ NGHĨA** = *"không override"*. 📌 Và đây là điều làm migration **an toàn**: dữ liệu cũ
        // (cột `DonVi` = `NULL`) giữ nguyên hành vi.
        var kq = Chon(PhamViHanMuc.Channel, [Dong(null, 200m)], DonViHanMuc.Absolute);

        Assert.Equal(DonViHanMuc.Absolute, kq.DonVi);
    }

    [Theory]
    [InlineData(DonViHanMuc.Percent)]
    [InlineData(DonViHanMuc.Absolute)]
    public void Dong_KHONG_khai_DonVi__ke_thua_DUNG_ca_hai_gia_tri(DonViHanMuc chung)
    {
        // ⚠️ Chống **"mặc định ngầm"**: một cài đặt `?? DonViHanMuc.Percent` (thay vì `?? donVi`) sẽ làm ca
        // `Absolute` ĐỎ — và ca `Percent` vẫn xanh ⇒ 🔑 phải kiểm **cả hai** giá trị mới bắt được.
        var kq = Chon(PhamViHanMuc.Channel, [Dong(null, 10m)], chung);

        Assert.Equal(chung, kq.DonVi);
    }

    // ── (3) 🔑 ĐƠN VỊ ĐI KÈM GIÁ TRỊ — không lấy lẫn giữa các dòng ──────────────────────────

    [Fact]
    public void Chon_dong_theo_KHO__DonVi_phai_la_CUA_DONG_DO()
    {
        // 🚨 **Ca quan trọng nhất về mặt "lấy lẫn":** có **HAI** dòng — dòng cả kênh (`Percent`) và dòng
        // theo kho (`Absolute`). Khi giữ chỗ ở kho A, hệ thống phải lấy **cả** `GiaTri` **và** `DonVi`
        // của **cùng một dòng**.
        // 📌 Nếu lấy `GiaTri` của dòng theo kho mà `DonVi` của dòng cả kênh thì `30` (chiếc) bị đọc thành `30%`.
        var kq = Chon(
            PhamViHanMuc.ChannelWarehouse,
            [Dong(null, 50m, DonViHanMuc.Percent), Dong(KhoA, 30m, DonViHanMuc.Absolute)],
            DonViHanMuc.Percent,
            kho: KhoA);

        Assert.Equal(HanMucKenh.NguonHanMuc.BangTheoKenhVaKho, kq.Nguon);
        Assert.Equal(30m, kq.GiaTri);
        Assert.Equal(DonViHanMuc.Absolute, kq.DonVi);
    }

    [Fact]
    public void Chon_dong_theo_KHO__khi_dong_do_KHONG_khai_DonVi__ke_thua_setting()
    {
        // 🔒 `null` ở dòng theo kho ⇒ kế thừa setting — **không** kế thừa `DonVi` của dòng cả kênh.
        // 📌 Đây là một quyết định **có chủ ý**: `null` nghĩa là *"theo chính sách"*, mà chính sách là **setting**,
        // không phải một dòng khác. ⚠️ Nếu kế thừa từ dòng cả kênh thì người vận hành **không** đoán được
        // hành vi: đổi dòng cả kênh sẽ **âm thầm** đổi đơn vị của dòng theo kho.
        var kq = Chon(
            PhamViHanMuc.ChannelWarehouse,
            [Dong(null, 50m, DonViHanMuc.Percent), Dong(KhoA, 30m)],
            DonViHanMuc.Absolute,
            kho: KhoA);

        Assert.Equal(30m, kq.GiaTri);
        Assert.Equal(DonViHanMuc.Absolute, kq.DonVi);
    }

    // ── (4) KHÔNG CÓ DÒNG NÀO KHỚP ⇒ ĐƠN VỊ = SETTING ────────────────────────────────────────

    [Fact]
    public void Khong_co_dong_nao__DonVi_la_setting_chung()
    {
        // 🔒 Nhất quán với `GiaTri`: không có dòng ⇒ **cả** giá trị **và** đơn vị đều từ setting.
        // 📌 Nếu `DonVi` lấy từ chỗ khác thì cặp `(GiaTriMacDinh, DonVi)` trở nên **vô nghĩa**.
        var kq = Chon(PhamViHanMuc.Channel, [], DonViHanMuc.Percent, macDinh: 40m);

        Assert.Equal(HanMucKenh.NguonHanMuc.MacDinhChung, kq.Nguon);
        Assert.Equal(40m, kq.GiaTri);
        Assert.Equal(DonViHanMuc.Percent, kq.DonVi);
    }

    [Fact]
    public void PhamVi_Channel__bo_qua_dong_theo_kho__DonVi_cung_lay_tu_dong_CA_KENH()
    {
        // 🔒 Bất biến "đơn vị đi kèm giá trị" áp **cả** khi có dòng bị bỏ qua: `GiaTri` lấy từ dòng cả kênh
        // ⇒ `DonVi` **cũng** phải từ dòng cả kênh (không phải từ dòng theo kho bị bỏ qua).
        var kq = Chon(
            PhamViHanMuc.Channel,
            [Dong(null, 50m, DonViHanMuc.Percent), Dong(KhoA, 30m, DonViHanMuc.Absolute)],
            DonViHanMuc.Absolute,
            kho: KhoA);

        Assert.Equal(HanMucKenh.NguonHanMuc.BangTheoKenh, kq.Nguon);
        Assert.Equal(50m, kq.GiaTri);
        Assert.Equal(DonViHanMuc.Percent, kq.DonVi);
    }

    // ── (5) GHI ĐƯỢC VÀO LOG — nguồn + đơn vị luôn có ───────────────────────────────────────

    [Fact]
    public void KetQua__luon_co_DonVi__de_tra_loi_vi_sao_bi_chan()
    {
        // 🔑 `KetQuaHanMuc.DonVi` là trường **không nullable** ⇒ chỗ gọi **luôn** in được
        // *"vượt hạn mức 30 Absolute"* thay vì *"vượt hạn mức 30"* (mà `30` một mình thì **vô nghĩa**).
        // 📌 Đây là lý do `KetQuaHanMuc` **không** để `DonVi` nullable: một kết quả *"đã chọn"* mà thiếu đơn vị
        // thì **không diễn đạt được** — và chỗ gọi sẽ phải tự đoán.
        foreach (var kq in new[]
                 {
                     Chon(PhamViHanMuc.Channel, [Dong(null, 1m, DonViHanMuc.Percent)], DonViHanMuc.Absolute),
                     Chon(PhamViHanMuc.Channel, [Dong(null, 1m)], DonViHanMuc.Absolute),
                     Chon(PhamViHanMuc.Channel, [], DonViHanMuc.Absolute)
                 })
        {
            Assert.True(Enum.IsDefined(kq.DonVi), $"DonVi phải là giá trị hợp lệ, đang là {kq.DonVi}.");
        }
    }
}
