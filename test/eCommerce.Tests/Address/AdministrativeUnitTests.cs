using eCommerce.Address;
using Volo.Abp;

namespace eCommerce.Tests.Address;

/// <summary>
/// Test cho **`ADDR-001`** — tra cứu &amp; xác minh đơn vị hành chính (mục (c), nghĩa vụ pháp lý GĐ0).
///
/// 🎯 Ba nhóm lỗi IM LẶNG được khoá lại:
///   1. **Mã ĐVHC không phải khoá toàn cục** — cùng `00004` là *Phường Ba Đình* ở schema v2 nhưng
///      *Phường Trúc Bạch* ở v1 ⇒ tra mã trần sẽ **im lặng trả SAI đơn vị**.
///   2. **Xã không thuộc tỉnh** ⇒ địa chỉ sai đi thẳng vào **vận đơn** và **hoá đơn**.
///   3. **Cấp huyện ở schema 2 cấp** — từ 01/7/2025 (`Luật 72/2025/QH15`) cấp huyện **đã bị bỏ**.
/// </summary>
public class AdministrativeUnitTests
{
    // ── Dữ liệu mẫu: cố ý có CÙNG MÃ nhưng KHÁC đơn vị giữa hai phiên bản ──

    private static List<AdministrativeUnit> DuLieu()
    {
        var v1 = AdministrativeSchemaVersion.V1;
        var v2 = AdministrativeSchemaVersion.V2;

        return
        [
            // ── Schema v2 (2 cấp: 34 tỉnh, KHÔNG huyện) ──
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Tinh, "79", "Thành phố Hồ Chí Minh"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00004", "Phường Ba Đình", parentCode: "01"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00005", "Phường Trúc Bạch", parentCode: "01"),

            // ── Schema v1 (3 cấp: 63 tỉnh, CÓ huyện) ──
            AdministrativeUnit.TaoMoi(v1, AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            AdministrativeUnit.TaoMoi(v1, AdministrativeLevel.QuanHuyen, "001", "Quận Ba Đình", parentCode: "01"),
            // 🚩 CÙNG mã 00004 nhưng là đơn vị KHÁC so với v2:
            AdministrativeUnit.TaoMoi(v1, AdministrativeLevel.Xa, "00004", "Phường Trúc Bạch", parentCode: "001")
        ];
    }

    // ── 1. Định dạng mã theo cấp ────────────────────────────────────────────

    [Theory]
    [InlineData(AdministrativeLevel.Tinh, "01", true)]
    [InlineData(AdministrativeLevel.Tinh, "1", false)]      // thiếu số 0 đầu
    [InlineData(AdministrativeLevel.Tinh, "123", false)]
    [InlineData(AdministrativeLevel.Xa, "00004", true)]
    [InlineData(AdministrativeLevel.Xa, "4", false)]
    [InlineData(AdministrativeLevel.QuanHuyen, "001", true)]
    [InlineData(AdministrativeLevel.QuanHuyen, "01", false)]
    public void Dinh_dang_ma_theo_cap(AdministrativeLevel level, string code, bool hopLe)
    {
        if (hopLe)
        {
            Assert.Equal(code, AdministrativeUnit.ChuanHoaMa(level, code));
        }
        else
        {
            var ex = Assert.Throws<BusinessException>(() => AdministrativeUnit.ChuanHoaMa(level, code));
            Assert.Equal(AddressErrorCodes.MaKhongHopLe, ex.Code);
        }
    }

    [Fact]
    public void Ma_chua_chu_cai_thi_khong_hop_le()
    {
        Assert.Equal(AddressErrorCodes.MaKhongHopLe,
            Assert.Throws<BusinessException>(() => AdministrativeUnit.ChuanHoaMa(AdministrativeLevel.Tinh, "0A")).Code);
    }

    // ── 2. 🚩 BẪY CHÍNH: mã trùng giữa hai phiên bản ────────────────────────

    [Fact]
    public void CUNG_ma_00004_nhung_KHAC_don_vi_giua_v1_va_v2()
    {
        var ds = DuLieu();

        var oV2 = AdministrativeUnitResolver.TraTheoMa(ds, 2, AdministrativeLevel.Xa, "00004");
        var oV1 = AdministrativeUnitResolver.TraTheoMa(ds, 1, AdministrativeLevel.Xa, "00004");

        Assert.Equal("Phường Ba Đình", oV2.Name);
        Assert.Equal("Phường Trúc Bạch", oV1.Name);
        Assert.NotEqual(oV2.Name, oV1.Name);      // ⬅️ nếu bỏ phiên bản khỏi phép tra thì đây là lỗi im lặng
        Assert.NotEqual(oV2.Id, oV1.Id);
    }

    [Fact]
    public void SoSanh_ma_xa_giua_cac_phien_ban_thay_ro_su_khac_nhau()
    {
        var kq = AdministrativeUnitResolver.SoSanhMaXaGiuaCacPhienBan(DuLieu(), "00004");

        Assert.Equal("Phường Trúc Bạch", kq[1]!.Name);
        Assert.Equal("Phường Ba Đình", kq[2]!.Name);
    }

    [Fact]
    public void Tra_khong_thay_thi_NEM_LOI_chu_khong_tra_null()
    {
        // 🚨 Trả null thì nơi gọi có thể vô tình bỏ qua và ghi đơn với địa chỉ rỗng.
        var ex = Assert.Throws<BusinessException>(
            () => AdministrativeUnitResolver.TraTheoMa(DuLieu(), 2, AdministrativeLevel.Xa, "99999"));

        Assert.Equal(AddressErrorCodes.KhongTimThayDonVi, ex.Code);
    }

    [Fact]
    public void Phien_ban_khong_ho_tro_thi_NEM_LOI()
    {
        Assert.Equal(AddressErrorCodes.PhienBanKhongHopLe,
            Assert.Throws<BusinessException>(() => AdministrativeSchemaVersion.TheoSo(3)).Code);
    }

    // ── 3. Cấp huyện: bị BỎ từ 01/7/2025 ────────────────────────────────────

    [Fact]
    public void KHONG_tao_duoc_don_vi_cap_huyen_o_schema_2_cap()
    {
        var ex = Assert.Throws<BusinessException>(() => AdministrativeUnit.TaoMoi(
            AdministrativeSchemaVersion.V2, AdministrativeLevel.QuanHuyen, "001", "Quận Ba Đình", parentCode: "01"));

        Assert.Equal(AddressErrorCodes.HuyenKhongDuocPhepOCap2, ex.Code);
    }

    [Fact]
    public void KiemTraBaCap_o_schema_2_cap_bi_TU_CHOI_theo_luat()
    {
        var kq = AdministrativeUnitResolver.KiemTraBaCap(DuLieu(), 2, "01", "001", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.HuyenKhongDuocPhepOCap2, kq.ErrorCode);
    }

    [Fact]
    public void Cap_duoi_bat_buoc_co_cap_tren()
    {
        var ex = Assert.Throws<BusinessException>(() => AdministrativeUnit.TaoMoi(
            AdministrativeSchemaVersion.V2, AdministrativeLevel.Xa, "00004", "Phường Ba Đình"));

        Assert.Equal(AddressErrorCodes.MaKhongHopLe, ex.Code);
    }

    // ── 4. Xác minh 2 cấp (địa chỉ MỚI) ─────────────────────────────────────

    [Fact]
    public void Dia_chi_2_cap_hop_le()
    {
        var kq = AdministrativeUnitResolver.KiemTraHaiCap(DuLieu(), 2, "01", "00004");

        Assert.True(kq.HopLe);
        Assert.Null(kq.ErrorCode);
        Assert.Equal("Thành phố Hà Nội", kq.Tinh!.Name);
        Assert.Equal("Phường Ba Đình", kq.Xa!.Name);
        Assert.Null(kq.Huyen);
    }

    [Fact]
    public void Xa_KHONG_thuoc_tinh_thi_FAIL_CLOSED()
    {
        // Xã 00004 thuộc tỉnh 01, nhưng khai báo với tỉnh 79 ⇒ phải bị từ chối.
        var kq = AdministrativeUnitResolver.KiemTraHaiCap(DuLieu(), 2, "79", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.XaKhongThuocTinh, kq.ErrorCode);
    }

    [Fact]
    public void Khong_tra_duoc_tinh_thi_FAIL_CLOSED()
    {
        var kq = AdministrativeUnitResolver.KiemTraHaiCap(DuLieu(), 2, "99", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.KhongTimThayDonVi, kq.ErrorCode);
    }

    [Fact]
    public void Khong_tra_duoc_xa_thi_FAIL_CLOSED()
    {
        var kq = AdministrativeUnitResolver.KiemTraHaiCap(DuLieu(), 2, "01", "88888");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.KhongTimThayDonVi, kq.ErrorCode);
    }

    [Fact]
    public void MA_DA_DONG_thi_khong_dung_cho_dia_chi_MOI()
    {
        var ds = DuLieu();
        var xa = ds.Single(u => u.SchemaVersion == 2 && u.Code == "00004");
        xa.Dong("Sáp nhập theo NQ 60-NQ/TW");

        var kq = AdministrativeUnitResolver.KiemTraHaiCap(ds, 2, "01", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.MaDaDong, kq.ErrorCode);
        // ⚠️ Nhưng vẫn TRA được để hiển thị dữ liệu lịch sử:
        Assert.NotNull(AdministrativeUnitResolver.ThuTra(ds, 2, AdministrativeLevel.Xa, "00004"));
    }

    [Fact]
    public void KiemTraHaiCap_tren_schema_3_cap_bi_tu_choi_de_tranh_bo_sot_huyen()
    {
        var kq = AdministrativeUnitResolver.KiemTraHaiCap(DuLieu(), 1, "01", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.PhienBanKhongHopLe, kq.ErrorCode);
    }

    // ── 5. Xác minh 3 cấp (dữ liệu LỊCH SỬ) ─────────────────────────────────

    [Fact]
    public void Dia_chi_3_cap_hop_le_o_schema_v1()
    {
        var kq = AdministrativeUnitResolver.KiemTraBaCap(DuLieu(), 1, "01", "001", "00004");

        Assert.True(kq.HopLe);
        Assert.Equal("Quận Ba Đình", kq.Huyen!.Name);
        Assert.Equal("Phường Trúc Bạch", kq.Xa!.Name);
    }

    [Fact]
    public void Huyen_khong_thuoc_tinh_thi_FAIL_CLOSED()
    {
        var ds = DuLieu();
        // Huyện 001 thuộc tỉnh 01; thêm một tỉnh khác ở v1 để thử khai sai.
        ds.Add(AdministrativeUnit.TaoMoi(AdministrativeSchemaVersion.V1, AdministrativeLevel.Tinh, "79", "Thành phố Hồ Chí Minh"));

        var kq = AdministrativeUnitResolver.KiemTraBaCap(ds, 1, "79", "001", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.HuyenKhongThuocTinh, kq.ErrorCode);
    }

    [Fact]
    public void Xa_khong_thuoc_huyen_thi_FAIL_CLOSED()
    {
        var ds = DuLieu();
        // Ở v1, thêm xã 00005 thuộc huyện 001 rồi khai nó với huyện khác.
        ds.Add(AdministrativeUnit.TaoMoi(AdministrativeSchemaVersion.V1, AdministrativeLevel.QuanHuyen, "002", "Quận Hoàn Kiếm", parentCode: "01"));

        var kq = AdministrativeUnitResolver.KiemTraBaCap(ds, 1, "01", "002", "00004");

        Assert.False(kq.HopLe);
        Assert.Equal(AddressErrorCodes.XaKhongThuocTinh, kq.ErrorCode);
    }
}
