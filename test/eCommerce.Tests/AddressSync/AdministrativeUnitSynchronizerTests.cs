using eCommerce.Address;
using eCommerce.Address.Sync;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// Test cho **lõi đồng bộ danh mục ĐVHC** (`ADDR-001`) — trọng tâm là **4 HÀNG RÀO CHỐNG PHÁ HOẠI**.
///
/// 🎯 Vì sao đây là phần quan trọng nhất: đồng bộ danh mục là thao tác **CÓ TÍNH PHÁ HOẠI** — nó
/// **ĐÓNG** những mã không còn trong danh mục nguồn. Nếu nguồn trả về **một phần** (API lỗi, timeout
/// giữa chừng, tải phải trang rỗng) thì "đóng những mã không còn" sẽ **đóng gần hết địa chỉ đang dùng**
/// ⇒ mọi vận đơn mới **fail-closed** ⇒ **sập luồng bán hàng chỉ vì một lần gọi API hỏng**.
/// </summary>
public class AdministrativeUnitSynchronizerTests
{
    private static readonly DateTime TaiLuc = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    private static AdministrativeUnit DonVi(AdministrativeLevel cap, string ma, string ten, bool dangHoatDong = true)
    {
        var dv = AdministrativeUnit.TaoMoi(AdministrativeSchemaVersion.V2, cap, ma, ten, parentCode: cap == AdministrativeLevel.Tinh ? null : "01");

        if (!dangHoatDong)
        {
            dv.Dong("Đóng để test");
        }

        return dv;
    }

    /// <summary>Danh mục nguồn gồm 1 tỉnh + các xã chỉ định.</summary>
    private static DanhMucNguon Nguon(params (string Ma, string Ten)[] xa)
    {
        var ds = new List<DonViNguon>
        {
            new(2, AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội")
        };

        foreach (var (ma, ten) in xa)
        {
            ds.Add(new DonViNguon(2, AdministrativeLevel.Xa, ma, ten, "01"));
        }

        return new DanhMucNguon(ds, "provinces.open-api.vn", TaiLuc);
    }

    private static KeHoachDongBo Lap(
        IReadOnlyList<AdministrativeUnit> hienCo,
        DanhMucNguon nguon,
        double nguong = 1.0)
        // ⚠️ Mặc định 1.0 (KHÔNG chặn) vì dữ liệu test rất nhỏ: đóng 1/2 đơn vị = 50% sẽ vượt ngưỡng 20%
        // và hàng rào 4 chặn đúng như thiết kế. Các test kiểm chính hàng rào 4 truyền `nguong: 0.20` tường minh.
        => AdministrativeUnitSynchronizer.LapKeHoach(2, hienCo, nguon, nguong, soDonViToiThieu: 2);

    // ── Đồng bộ bình thường ────────────────────────────────────────────────

    [Fact]
    public void Danh_muc_giong_het_thi_KHONG_co_thay_doi()
    {
        var hienCo = new List<AdministrativeUnit>
        {
            DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            DonVi(AdministrativeLevel.Xa, "00004", "Phường Ba Đình")
        };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        Assert.False(kh.CoThayDoi);
        Assert.Empty(kh.ThayDois);
    }

    [Fact]
    public void Ma_MOI_thi_them_moi()
    {
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội") };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        Assert.Equal(1, kh.SoThemMoi);
        Assert.Equal(LoaiThayDoi.ThemMoi, Assert.Single(kh.ThayDois).Loai);
    }

    [Fact]
    public void Ten_doi_thi_DoiTen_chu_KHONG_tao_ban_ghi_moi()
    {
        var cu = DonVi(AdministrativeLevel.Xa, "00004", "Phường Ba Đình (cũ)");
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), cu };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        var td = Assert.Single(kh.ThayDois);
        Assert.Equal(LoaiThayDoi.DoiTen, td.Loai);
        // ⚠️ QUAN TRỌNG: trỏ vào CHÍNH entity cũ (giữ nguyên Id để hoá đơn/vận đơn cũ còn tham chiếu).
        Assert.Same(cu, td.HienCo);
        Assert.Equal(1, kh.SoDoiTen);
    }

    [Fact]
    public void Ma_BIEN_MAT_thi_DONG_chu_KHONG_XOA()
    {
        var xaCu = DonVi(AdministrativeLevel.Xa, "00099", "Phường cũ");
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), xaCu };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        var dong = Assert.Single(kh.ThayDois, x => x.Loai == LoaiThayDoi.Dong);
        Assert.Same(xaCu, dong.HienCo);
        Assert.Equal(1, kh.SoDong);
    }

    [Fact]
    public void ApDung_thi_dong_bang_co_IsActive_false_nhung_entity_VAN_CON()
    {
        var xaCu = DonVi(AdministrativeLevel.Xa, "00099", "Phường cũ");
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), xaCu };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));
        var deThem = AdministrativeUnitSynchronizer.ApDung(kh);

        Assert.False(xaCu.IsActive);                  // bị đóng
        Assert.Single(deThem);                        // 1 xã mới được tạo
        Assert.Equal("00004", deThem[0].Code);
        Assert.Equal(2, hienCo.Count);                // ⬅️ KHÔNG xoá khỏi danh sách
    }

    [Fact]
    public void Ma_tung_dong_nay_co_lai_thi_MO_LAI()
    {
        var daDong = DonVi(AdministrativeLevel.Xa, "00004", "Phường Ba Đình", dangHoatDong: false);
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), daDong };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        Assert.Equal(LoaiThayDoi.MoLai, Assert.Single(kh.ThayDois).Loai);
        Assert.Equal(1, kh.SoMoLai);
    }

    [Fact]
    public void Chi_xet_dung_PHIEN_BAN_schema_dang_dong_bo()
    {
        // 🚨 Mã ở v1 và v2 KHÔNG cùng ý nghĩa (bài học ADDR-001: mã 00004 là hai xã khác nhau).
        // Nếu không lọc theo phiên bản thì v1 sẽ bị đóng oan khi đồng bộ v2.
        var cuaV1 = AdministrativeUnit.TaoMoi(AdministrativeSchemaVersion.V1, AdministrativeLevel.Xa, "00004", "Phường Trúc Bạch", parentCode: "01");
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), cuaV1 };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        Assert.DoesNotContain(kh.ThayDois, x => ReferenceEquals(x.HienCo, cuaV1));
        Assert.True(cuaV1.IsActive);
    }

    // ── 🚨 HÀNG RÀO 1: danh mục rỗng ──────────────────────────────────────

    [Fact]
    public void HANG_RAO_1_danh_muc_RONG_thi_TU_CHOI()
    {
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội") };
        var rong = new DanhMucNguon([], "provinces.open-api.vn", TaiLuc);

        var ex = Assert.Throws<BusinessException>(() => Lap(hienCo, rong));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucRong, ex.Code);
        // ⇒ nếu áp dụng, danh mục rỗng sẽ ĐÓNG toàn bộ đơn vị đang dùng.
    }

    // ── 🚨 HÀNG RÀO 2: quá ít đơn vị ──────────────────────────────────────

    [Fact]
    public void HANG_RAO_2_danh_muc_QUA_IT_thi_TU_CHOI()
    {
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội") };
        var it = new DanhMucNguon([new DonViNguon(2, AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội")], "nguon", TaiLuc);

        var ex = Assert.Throws<BusinessException>(
            () => AdministrativeUnitSynchronizer.LapKeHoach(2, hienCo, it, 0.20, soDonViToiThieu: 10));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucQuaIt, ex.Code);
    }

    // ── 🚨 HÀNG RÀO 3: thiếu cấp bắt buộc ─────────────────────────────────

    [Fact]
    public void HANG_RAO_3_thieu_cap_TINH_thi_TU_CHOI()
    {
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội") };

        // Toàn xã, KHÔNG có tỉnh nào ⇒ chắc chắn dữ liệu sai/thiếu.
        var thieuTinh = new DanhMucNguon(
        [
            new DonViNguon(2, AdministrativeLevel.Xa, "00004", "Phường Ba Đình", "01"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00005", "Phường Trúc Bạch", "01")
        ], "nguon", TaiLuc);

        var ex = Assert.Throws<BusinessException>(() => Lap(hienCo, thieuTinh));

        Assert.Equal(AdministrativeSyncErrorCodes.ThieuCapBatBuoc, ex.Code);
    }

    // ── 🚨 HÀNG RÀO 4: đóng hàng loạt (hàng rào BẮT ĐÚNG CA NGUY HIỂM NHẤT) ──

    [Fact]
    public void HANG_RAO_4_dong_qua_nguong_thi_TU_CHOI()
    {
        // Có 5 xã đang hoạt động; nguồn chỉ còn 1 tỉnh + 2 xã ⇒ phải đóng 3/5 = 60% > 20%.
        var hienCo = new List<AdministrativeUnit>
        {
            DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            DonVi(AdministrativeLevel.Xa, "00004", "Phường Ba Đình"),
            DonVi(AdministrativeLevel.Xa, "00005", "Phường Trúc Bạch"),
            DonVi(AdministrativeLevel.Xa, "00006", "Phường A"),
            DonVi(AdministrativeLevel.Xa, "00007", "Phường B")
        };

        var ex = Assert.Throws<BusinessException>(() => Lap(hienCo, Nguon(("00004", "Phường Ba Đình")), nguong: 0.20));

        Assert.Equal(AdministrativeSyncErrorCodes.DongHangLoatVuotNguong, ex.Code);
        Assert.Equal(3, ex.Data["soDong"]);
        Assert.Equal(5, ex.Data["tongDangHoatDong"]);
    }

    [Fact]
    public void HANG_RAO_4_cho_phep_khi_dong_trong_nguong()
    {
        // 1/5 = 20% — ĐÚNG bằng ngưỡng ⇒ được phép (ngưỡng là "vượt quá mới chặn").
        var hienCo = new List<AdministrativeUnit>
        {
            DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            DonVi(AdministrativeLevel.Xa, "00004", "Phường Ba Đình"),
            DonVi(AdministrativeLevel.Xa, "00005", "Phường Trúc Bạch"),
            DonVi(AdministrativeLevel.Xa, "00006", "Phường A"),
            DonVi(AdministrativeLevel.Xa, "00099", "Phường sắp mất")
        };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình"), ("00005", "Phường Trúc Bạch"), ("00006", "Phường A")), nguong: 0.20);

        Assert.Equal(1, kh.SoDong);
    }

    [Fact]
    public void HANG_RAO_4_khong_chan_khi_danh_muc_dang_RONG()
    {
        // Hệ thống mới, chưa có đơn vị nào ⇒ không có gì để "đóng hàng loạt" ⇒ được phép nạp.
        var kh = Lap([], Nguon(("00004", "Phường Ba Đình")));

        // Nguồn có 2 đơn vị (1 tỉnh + 1 xã) ⇒ cả hai đều là "thêm mới".
        Assert.Equal(2, kh.SoThemMoi);
        Assert.Equal(0, kh.SoDong);
    }

    // ── ApDung ─────────────────────────────────────────────────────────────

    [Fact]
    public void ApDung_tao_entity_moi_dung_phien_ban_cap_ma_ten()
    {
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội") };
        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        var moi = Assert.Single(AdministrativeUnitSynchronizer.ApDung(kh));

        Assert.Equal(2, moi.SchemaVersion);
        Assert.Equal(AdministrativeLevel.Xa, moi.Level);
        Assert.Equal("00004", moi.Code);
        Assert.Equal("Phường Ba Đình", moi.Name);
        Assert.Equal("01", moi.ParentCode);
        Assert.True(moi.IsActive);
    }

    [Fact]
    public void ApDung_doi_ten_thi_entity_doi_ten_ngay()
    {
        var cu = DonVi(AdministrativeLevel.Xa, "00004", "Tên cũ");
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), cu };
        var kh = Lap(hienCo, Nguon(("00004", "Tên mới")));

        AdministrativeUnitSynchronizer.ApDung(kh);

        Assert.Equal("Tên mới", cu.Name);
    }

    [Fact]
    public void LapKeHoach_KHONG_thay_doi_gi_truoc_khi_ApDung()
    {
        // Bất biến quan trọng: tính kế hoạch là THUẦN — phải xem được kế hoạch TRƯỚC khi áp dụng.
        var cu = DonVi(AdministrativeLevel.Xa, "00099", "Phường cũ");
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"), cu };

        var kh = Lap(hienCo, Nguon(("00004", "Phường Ba Đình")));

        Assert.True(cu.IsActive);      // chưa bị đóng
        Assert.True(kh.CoThayDoi);      // kế hoạch ĐÃ tính ra thay đổi, nhưng chưa áp dụng
    }
}
