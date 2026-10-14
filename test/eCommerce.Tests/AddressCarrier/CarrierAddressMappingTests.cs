using eCommerce.Address;
using eCommerce.Address.Carrier;
using Volo.Abp;

namespace eCommerce.Tests.AddressCarrier;

/// <summary>
/// Test cho **`ADDR-002`** — mã địa chỉ theo hãng vận chuyển.
///
/// 🎯 Lỗi im lặng nguy hiểm nhất được khoá ở đây: **gửi hàng tới địa chỉ SUY ĐOÁN**.
///   Khớp theo tên chỉ là **gợi ý** (dấu tiếng Việt, "Phường" vs "Xã", tỉnh sáp nhập). Nếu hệ thống
///   dùng ánh xạ **chưa xác minh** thì hàng đi tới địa chỉ sai — **không thể thu hồi**, và
///   **không có cảnh báo nào** để biết là đã sai.
/// </summary>
public class CarrierAddressMappingTests
{
    private static readonly DateTime Synced = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    private static readonly Guid IdBaDinh = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IdTrucBach = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static List<AdministrativeUnit> DonVi()
    {
        var v2 = AdministrativeSchemaVersion.V2;

        return
        [
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00004", "Phường Ba Đình", parentCode: "01",
                tenantId: null, id: IdBaDinh),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00005", "Phường Trúc Bạch", parentCode: "01",
                tenantId: null, id: IdTrucBach)
        ];
    }

    private static CarrierAddressMapping AnhXa(
        string hang = "GHN",
        string ma = "GHN-P-001",
        Guid? dich = null,
        CarrierMatchMethod cach = CarrierMatchMethod.TheoMa,
        string ten = "Ba Đình")
        => CarrierAddressMapping.TaoMoi(hang, "PROVINCE", ma, ten, dich ?? IdBaDinh, cach, "api/ghn", Synced);

    // ── Trạng thái mặc định: CHƯA XÁC MINH (fail-closed) ────────────────────

    [Fact]
    public void Anh_xa_khop_theo_TEN_mac_dinh_la_CHUA_XAC_MINH()
    {
        var a = AnhXa(cach: CarrierMatchMethod.TheoTen);

        Assert.Equal(CarrierMappingStatus.ChuaXacMinh, a.Status);
    }

    [Fact]
    public void Anh_xa_khop_theo_MA_cung_mac_dinh_la_CHUA_XAC_MINH()
    {
        // Ngay cả khớp theo mã cũng cần xác minh: mã hãng có thể bị TÁI SỬ DỤNG sau sáp nhập.
        Assert.Equal(CarrierMappingStatus.ChuaXacMinh, AnhXa(cach: CarrierMatchMethod.TheoMa).Status);
    }

    [Fact]
    public void Anh_xa_THU_CONG_thi_DA_XAC_MINH()
    {
        Assert.Equal(CarrierMappingStatus.DaXacMinh, AnhXa(cach: CarrierMatchMethod.ThuCong).Status);
    }

    [Fact]
    public void Thieu_thong_tin_bat_buoc_thi_NEM_LOI()
    {
        Assert.Equal(CarrierAddressErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => CarrierAddressMapping.TaoMoi(
                "", "PROVINCE", "X", "Tên", IdBaDinh, CarrierMatchMethod.TheoMa, null, Synced)).Code);

        Assert.Equal(CarrierAddressErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => CarrierAddressMapping.TaoMoi(
                "GHN", "PROVINCE", "X", "Tên", Guid.Empty, CarrierMatchMethod.TheoMa, null, Synced)).Code);
    }

    // ── 🚨 Bất biến trung tâm: CHƯA XÁC MINH ⇒ KHÔNG DÙNG ĐƯỢC ────────────

    [Fact]
    public void Tra_anh_xa_CHUA_XAC_MINH_thi_NEM_LOI_KHONG_doan_bua()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.Tra([AnhXa()], DonVi(), "GHN", "GHN-P-001"));

        Assert.Equal(CarrierAddressErrorCodes.MaChuaXacMinh, ex.Code);
        // ⬅️ Dù ánh xạ CHỈ tới một đơn vị duy nhất và tên khớp — vẫn KHÔNG được dùng.
    }

    [Fact]
    public void Tra_anh_xa_NGHI_NGO_thi_cung_NEM_LOI()
    {
        var a = AnhXa(cach: CarrierMatchMethod.ThuCong);
        a.DanhDauNghiNgo();

        Assert.Equal(CarrierAddressErrorCodes.MaChuaXacMinh,
            Assert.Throws<BusinessException>(
                () => CarrierAddressMappingResolver.Tra([a], DonVi(), "GHN", "GHN-P-001")).Code);
    }

    [Fact]
    public void Sau_khi_XAC_MINH_thi_tra_duoc()
    {
        var a = AnhXa();
        a.XacMinh(CarrierMatchMethod.ThuCong);

        var kq = CarrierAddressMappingResolver.Tra([a], DonVi(), "GHN", "GHN-P-001");

        Assert.Equal("Phường Ba Đình", kq.DonVi.Name);
        Assert.Equal(CarrierMappingStatus.DaXacMinh, kq.AnhXa.Status);
    }

    // ── Scope theo HÃNG ─────────────────────────────────────────────────────

    [Fact]
    public void CUNG_ma_nhung_KHAC_hang_thi_la_HAI_anh_xa_doc_lap()
    {
        var ghn = AnhXa("GHN", "P-001", IdBaDinh, CarrierMatchMethod.ThuCong);
        var ghtk = AnhXa("GHTK", "P-001", IdTrucBach, CarrierMatchMethod.ThuCong);

        var kqGhn = CarrierAddressMappingResolver.Tra([ghn, ghtk], DonVi(), "GHN", "P-001");
        var kqGhtk = CarrierAddressMappingResolver.Tra([ghn, ghtk], DonVi(), "GHTK", "P-001");

        // Cùng mã "P-001" nhưng trỏ tới HAI địa chỉ khác nhau ⇒ nếu không scope theo hãng thì gửi sai.
        Assert.Equal("Phường Ba Đình", kqGhn.DonVi.Name);
        Assert.Equal("Phường Trúc Bạch", kqGhtk.DonVi.Name);
    }

    [Fact]
    public void Ma_hang_khong_ton_tai_thi_NEM_LOI()
    {
        Assert.Equal(CarrierAddressErrorCodes.KhongTimThayMaHang,
            Assert.Throws<BusinessException>(
                () => CarrierAddressMappingResolver.Tra(
                    [AnhXa(cach: CarrierMatchMethod.ThuCong)], DonVi(), "GHN", "KHONG-CO")).Code);
    }

    [Fact]
    public void Trung_lap_cung_hang_cung_ma_thi_NEM_LOI_chu_KHONG_chon_bua_cai_dau()
    {
        var a1 = AnhXa(cach: CarrierMatchMethod.ThuCong);
        var a2 = AnhXa(dich: IdTrucBach, cach: CarrierMatchMethod.ThuCong);

        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.Tra([a1, a2], DonVi(), "GHN", "GHN-P-001"));

        Assert.Equal(CarrierAddressErrorCodes.AnhXaTrungLap, ex.Code);
    }

    // ── Đơn vị đích ────────────────────────────────────────────────────────

    [Fact]
    public void Don_vi_dich_DA_DONG_thi_KHONG_dung_cho_van_don_moi()
    {
        var a = AnhXa(cach: CarrierMatchMethod.ThuCong);
        var donVi = DonVi();
        donVi.Single(u => u.Id == IdBaDinh).Dong("Sáp nhập");

        Assert.Equal(CarrierAddressErrorCodes.DonViDichDaDong,
            Assert.Throws<BusinessException>(
                () => CarrierAddressMappingResolver.Tra([a], donVi, "GHN", "GHN-P-001")).Code);
    }

    [Fact]
    public void Don_vi_dich_khong_tim_thay_thi_NEM_LOI()
    {
        var a = AnhXa(dich: Guid.NewGuid(), cach: CarrierMatchMethod.ThuCong);

        Assert.Equal(CarrierAddressErrorCodes.KhongTimThayMaHang,
            Assert.Throws<BusinessException>(
                () => CarrierAddressMappingResolver.Tra([a], DonVi(), "GHN", "GHN-P-001")).Code);
    }

    // ── Phát hiện sai lệch khi đồng bộ lại ─────────────────────────────────

    [Fact]
    public void Khong_sai_lech_khi_danh_muc_khong_doi()
    {
        var ds = new Dictionary<string, string> { ["GHN-P-001"] = "Ba Đình" };

        Assert.Empty(CarrierAddressMappingResolver.PhatHienSaiLech([AnhXa()], "GHN", ds));
    }

    [Fact]
    public void Phat_hien_MA_BIEN_MAT()
    {
        var ds = new Dictionary<string, string> { ["GHN-P-999"] = "Xã khác" };

        var sai = CarrierAddressMappingResolver.PhatHienSaiLech([AnhXa()], "GHN", ds);

        Assert.Contains(sai, s => s.Loai == LoaiSaiLech.MaBienMat && s.CarrierAddressCode == "GHN-P-001");
        Assert.Contains(sai, s => s.Loai == LoaiSaiLech.MaMoiChuaAnhXa && s.CarrierAddressCode == "GHN-P-999");
    }

    [Fact]
    public void Phat_hien_TEN_DOI()
    {
        var ds = new Dictionary<string, string> { ["GHN-P-001"] = "Phường Ba Đình (mới)" };

        var sai = CarrierAddressMappingResolver.PhatHienSaiLech([AnhXa()], "GHN", ds);

        var tenDoi = Assert.Single(sai.Where(s => s.Loai == LoaiSaiLech.TenDoi));
        Assert.Contains("Ba Đình (mới)", tenDoi.MoTa);
    }

    [Fact]
    public void Chi_xet_dung_HANG_dang_dong_bo()
    {
        var cuaGhtk = AnhXa("GHTK", "X-1");
        var dsGhn = new Dictionary<string, string> { ["GHN-P-001"] = "Ba Đình" };

        // Đồng bộ GHN ⇒ KHÔNG được báo mã của GHTK là "biến mất".
        var sai = CarrierAddressMappingResolver.PhatHienSaiLech([AnhXa(), cuaGhtk], "GHN", dsGhn);

        Assert.DoesNotContain(sai, s => s.CarrierAddressCode == "X-1");
    }

    // ── Đồng bộ lại: tên đổi ⇒ hạ mức tin cậy ──────────────────────────────

    [Fact]
    public void Dong_bo_lai_thay_TEN_DOI_thi_ha_xuong_NGHI_NGO()
    {
        var a = AnhXa(cach: CarrierMatchMethod.ThuCong);
        Assert.Equal(CarrierMappingStatus.DaXacMinh, a.Status);

        a.CapNhatTuDongBo("Phường Ba Đình (sáp nhập)", Synced.AddDays(1));

        // 🚩 Người xác minh trước đó đã xác minh trên THÔNG TIN CŨ ⇒ phải xem lại.
        Assert.Equal(CarrierMappingStatus.NghiNgo, a.Status);
        Assert.Equal("Phường Ba Đình (sáp nhập)", a.CarrierAddressName);
    }

    [Fact]
    public void Dong_bo_lai_TEN_KHONG_doi_thi_giu_nguyen_DA_XAC_MINH()
    {
        var a = AnhXa(cach: CarrierMatchMethod.ThuCong);

        a.CapNhatTuDongBo("Ba Đình", Synced.AddDays(1));

        Assert.Equal(CarrierMappingStatus.DaXacMinh, a.Status);
    }

    [Fact]
    public void LayChuaXacMinh_tra_dung_danh_sach_can_nguoi_xu_ly()
    {
        var chuaXm = AnhXa("GHN", "A-1");
        var daXm = AnhXa("GHN", "A-2", cach: CarrierMatchMethod.ThuCong);
        var nghiNgo = AnhXa("GHTK", "B-1");
        nghiNgo.DanhDauNghiNgo();

        var ds = CarrierAddressMappingResolver.LayChuaXacMinh([chuaXm, daXm, nghiNgo]);

        Assert.Equal(2, ds.Count);
        Assert.DoesNotContain(ds, x => x.CarrierAddressCode == "A-2");
        Assert.Equal("GHN", ds[0].CarrierCode);   // sắp xếp theo hãng rồi mã
    }

    [Fact]
    public void ThuTra_tra_null_thay_vi_nem_loi()
    {
        Assert.Null(CarrierAddressMappingResolver.ThuTra([], "GHN", "X"));
        Assert.NotNull(CarrierAddressMappingResolver.ThuTra([AnhXa()], "ghn", "GHN-P-001"));
    }
}
