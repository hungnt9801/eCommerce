using eCommerce.Inventory;
using eCommerce.Settings;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// Test **bản ghi lượt đối soát** (`REC-001` v2 — `ReconRuns` + `ReconRunMismatches`).
///
/// **VÌ SAO KIỂM Ở TẦNG ENTITY:** những con số trong bảng này là thứ người vận hành **dùng để quyết định**
/// (*"lệch mới hay đã lâu"*, *"lượt đó có vượt ngưỡng không"*). Một dòng **vô lý** (ví dụ số vượt ngưỡng
/// lớn hơn tổng số lệch) làm **hỏng cả báo cáo xu hướng** mà **không** ném lỗi ở đâu.
/// 🔒 Vì vậy: **NÉM** ngay ở nơi tạo/chốt, và **suy ra kết luận ở MỘT chỗ** (không để chỗ gọi tự quyết).
/// </summary>
public class ReconRunTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = T0.AddSeconds(2);

    private static ReconRun Tao(
        int soCapDaDo = 10,
        DonViNguong donVi = DonViNguong.Percent,
        decimal giaTri = 0.5m,
        HanhDongLech hanhDong = HanhDongLech.Warn,
        bool hopLe = true)
        => new(Guid.NewGuid(), T0, T1, soCapDaDo, donVi, giaTri, hanhDong, hopLe);

    [Fact]
    public void Tao__luu_ANH_CHUP_chinh_sach__khong_chi_ket_qua()
    {
        var luot = Tao(donVi: DonViNguong.Absolute, giaTri: 3m, hanhDong: HanhDongLech.RequireConfirmation, hopLe: false);

        // 🚨 Đây là lý do bảng tồn tại: đổi setting về sau KHÔNG được viết lại ngữ cảnh của lượt đã qua.
        Assert.Equal(DonViNguong.Absolute, luot.DonViNguong);
        Assert.Equal(3m, luot.GiaTriNguong);
        Assert.Equal(HanhDongLech.RequireConfirmation, luot.HanhDong);
        Assert.False(luot.NguongHopLe);   // cấu hình lạ ⇒ lượt này chạy bằng MẶC ĐỊNH an toàn — phải đọc được
    }

    [Fact]
    public void Moc_ket_thuc_TRUOC_moc_bat_dau__NEM__khong_luu_so_lieu_khong_doi_soat_duoc()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReconRun(Guid.NewGuid(), T1, T0, 10, DonViNguong.Percent, 0.5m, HanhDongLech.Warn, true));
    }

    [Fact]
    public void ChotKetQua__KHONG_vuot__ket_luan_KhongVuot_du_co_lech()
    {
        var luot = Tao();

        luot.ChotKetQua(soLech: 5, soLechVuotNguong: 0);

        Assert.Equal(5, luot.SoLech);
        Assert.Equal(0, luot.SoLechVuotNguong);
        Assert.False(luot.CoVuotNguong);
        Assert.Equal(KetLuanDoiSoat.KhongVuot, luot.KetLuan);
    }

    [Fact]
    public void ChotKetQua__vuot_nguong_va_hanh_dong_WARN__ket_luan_VuotCanhBao()
    {
        var luot = Tao(hanhDong: HanhDongLech.Warn);

        luot.ChotKetQua(soLech: 5, soLechVuotNguong: 2);

        Assert.True(luot.CoVuotNguong);
        Assert.Equal(KetLuanDoiSoat.VuotCanhBao, luot.KetLuan);
    }

    [Fact]
    public void ChotKetQua__vuot_nguong_va_hanh_dong_REQUIRE_CONFIRMATION__ket_luan_VuotCanXacNhan()
    {
        var luot = Tao(hanhDong: HanhDongLech.RequireConfirmation);

        luot.ChotKetQua(soLech: 5, soLechVuotNguong: 1);

        // 🔑 CÙNG dữ liệu, HAI kết luận khác nhau — đúng vì chính sách khác nhau. Đây là "ô chọn CÓ tác dụng".
        Assert.Equal(KetLuanDoiSoat.VuotCanXacNhan, luot.KetLuan);
    }

    [Theory]
    [InlineData(-1, 0)]    // số lệch âm
    [InlineData(0, -1)]    // số vượt ngưỡng âm
    [InlineData(3, 4)]     // số VƯỢT lớn hơn TỔNG số lệch ⇒ vô lý
    public void ChotKetQua__so_lieu_VO_LY__NEM(int soLech, int soVuotNguong)
    {
        var luot = Tao();

        Assert.Throws<ArgumentException>(() => luot.ChotKetQua(soLech, soVuotNguong));
    }

    [Fact]
    public void ChotKetQua__bien__so_vuot_BANG_tong_so_lech__hop_le()
    {
        var luot = Tao();

        luot.ChotKetQua(soLech: 4, soLechVuotNguong: 4);

        Assert.True(luot.CoVuotNguong);
        Assert.Equal(4, luot.SoLechVuotNguong);
    }

    [Fact]
    public void Luot_KHONG_co_lech__van_la_mot_ban_ghi_HOP_LE__day_chinh_la_bang_chung_da_do()
    {
        var luot = Tao(soCapDaDo: 7);

        luot.ChotKetQua(soLech: 0, soLechVuotNguong: 0);

        // 📌 `0` lệch + `0` vượt KHÔNG phải "không có dữ liệu": `SoCapDaDo = 7` nói rằng **đã đo 7 cặp**.
        // Đây là điểm khác biệt cốt lõi so với việc chỉ đọc metric (metric `0` không phân biệt "khớp" với "chưa đo").
        Assert.Equal(7, luot.SoCapDaDo);
        Assert.Equal(KetLuanDoiSoat.KhongVuot, luot.KetLuan);
    }
}

/// <summary>Test **dòng lệch** của một lượt (`ReconRunMismatches`).</summary>
public class ReconRunMismatchTests
{
    [Fact]
    public void ChenhLech__co_DAU__de_doc_duoc_lech_ve_phia_nao()
    {
        var thua = new ReconRunMismatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 105, 100, vuotNguong: false);
        var thieu = new ReconRunMismatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-2", 95, 100, vuotNguong: false);

        Assert.Equal(5, thua.ChenhLech);      // ảnh chụp NHIỀU hơn sổ cái
        Assert.Equal(-5, thieu.ChenhLech);    // ảnh chụp ÍT hơn sổ cái
    }

    [Fact]
    public void VuotNguong__luu_o_TUNG_DONG__de_tra_loi_duoc_cap_nao_da_vuot()
    {
        var runId = Guid.NewGuid();

        var d1 = new ReconRunMismatch(Guid.NewGuid(), runId, Guid.NewGuid(), "SKU-1", 101, 100, vuotNguong: false);
        var d2 = new ReconRunMismatch(Guid.NewGuid(), runId, Guid.NewGuid(), "SKU-2", 200, 100, vuotNguong: true);

        Assert.False(d1.VuotNguong);
        Assert.True(d2.VuotNguong);
    }
}
