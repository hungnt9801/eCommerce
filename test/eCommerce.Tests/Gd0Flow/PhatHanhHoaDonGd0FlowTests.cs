using eCommerce.Address;
using eCommerce.EInvoice;
using eCommerce.EInvoice.Application;
using eCommerce.Signing;
using Volo.Abp;

namespace eCommerce.Tests.Gd0Flow;

/// <summary>
/// Test cho **luồng GĐ0 end-to-end**: *xác minh địa chỉ → cấp số → tạo hoá đơn → tạo yêu cầu ký*.
///
/// 🎯 BẤT BIẾN QUAN TRỌNG NHẤT ĐƯỢC KHOÁ Ở ĐÂY: **địa chỉ nằm trong hash được ký**.
///   Nếu không, người ta có thể **sửa địa chỉ trên hoá đơn SAU KHI ĐÃ KÝ** mà chữ ký **vẫn "hợp lệ"**
///   — hoá đơn sai địa chỉ nhưng **không có cảnh báo nào**. Test đổi xã và khẳng định hash **đổi**.
/// </summary>
public class PhatHanhHoaDonGd0FlowTests
{
    private static readonly DateTime NgayLap = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>Danh mục ĐVHC tối thiểu: schema v2 (2 cấp) — 2 tỉnh, 2 xã thuộc tỉnh 01.</summary>
    private static List<AdministrativeUnit> DonVi()
    {
        var v2 = AdministrativeSchemaVersion.V2;

        return
        [
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Tinh, "79", "Thành phố Hồ Chí Minh"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00004", "Phường Ba Đình", parentCode: "01"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00005", "Phường Trúc Bạch", parentCode: "01"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "26734", "Phường Bến Thành", parentCode: "79")
        ];
    }

    private static PhatHanhHoaDonGd0Input DauVao(string xaCode = "00004", string tinhCode = "01")
        => new(2, tinhCode, xaCode, "1C26TAA", "00000042", NgayLap);

    private static KeHoachPhatHanh Lap(string xaCode = "00004", string tinhCode = "01")
        => PhatHanhHoaDonGd0Planner.Lap(DonVi(), DauVao(xaCode, tinhCode));

    // ── Luồng thành công ────────────────────────────────────────────────────

    [Fact]
    public void Luong_thanh_cong_tao_hoa_don_nhap_va_yeu_cau_ky()
    {
        var keHoach = Lap();

        Assert.Equal(EInvoiceStatus.Nhap, keHoach.HoaDon.Status);
        Assert.Equal("00000042", keHoach.HoaDon.SoHoaDon);
        Assert.Equal(SigningRequestStatus.ChoKy, keHoach.YeuCauKy.TrangThai);
        Assert.Equal("Phường Ba Đình, Thành phố Hà Nội", keHoach.DiaChiDaXacMinh);
    }

    [Fact]
    public void Hash_tai_lieu_dung_dinh_dang_SHA256_va_khop_voi_yeu_cau_ky()
    {
        var keHoach = Lap();

        // 64 ký tự hex = SHA-256 (đúng yêu cầu của SigningRequest.TaoYeuCau).
        Assert.Equal(64, keHoach.HashTaiLieu.Length);
        Assert.All(keHoach.HashTaiLieu, c => Assert.True(char.IsAsciiHexDigit(c)));

        // 🔒 Yêu cầu ký PHẢI mang đúng hash đó — nếu lệch thì bước 2 không bao giờ khớp được.
        Assert.Equal(keHoach.HashTaiLieu, keHoach.YeuCauKy.HashTaiLieu);
        Assert.Equal("SHA256", keHoach.YeuCauKy.ThuatToanBam);
    }

    [Fact]
    public void Ma_tai_lieu_cua_yeu_cau_ky_truy_vet_duoc_ve_hoa_don()
    {
        var keHoach = Lap();

        Assert.Equal("HD:1C26TAA/00000042", keHoach.YeuCauKy.MaTaiLieu);
    }

    // ── 🚨 BẤT BIẾN: ĐỊA CHỈ NẰM TRONG HASH ────────────────────────────────

    [Fact]
    public void DOI_XA_thi_HASH_DOI_nho_vay_sua_dia_chi_sau_khi_ky_se_bi_tu_choi()
    {
        var baDinh = Lap("00004");
        var trucBach = Lap("00005");

        Assert.NotEqual(baDinh.HashTaiLieu, trucBach.HashTaiLieu);
    }

    [Fact]
    public void CUNG_XA_nhung_KHAC_TINH_thi_khong_the_tao_duoc_vi_xa_khong_thuoc_tinh()
    {
        // Xã 00004 thuộc tỉnh 01; khai với tỉnh 79 ⇒ FAIL-CLOSED.
        var ex = Assert.Throws<BusinessException>(() => Lap("00004", "79"));

        Assert.Equal(AddressErrorCodes.XaKhongThuocTinh, ex.Code);
    }

    [Fact]
    public void Doi_SO_HOA_DON_thi_hash_doi()
    {
        var a = PhatHanhHoaDonGd0Planner.Lap(DonVi(), DauVao() with { SoHoaDon = "00000042" });
        var b = PhatHanhHoaDonGd0Planner.Lap(DonVi(), DauVao() with { SoHoaDon = "00000043" });

        Assert.NotEqual(a.HashTaiLieu, b.HashTaiLieu);
    }

    [Fact]
    public void Doi_NGAY_LAP_thi_hash_doi()
    {
        var a = PhatHanhHoaDonGd0Planner.Lap(DonVi(), DauVao());
        var b = PhatHanhHoaDonGd0Planner.Lap(DonVi(), DauVao() with { NgayLap = NgayLap.AddDays(1) });

        Assert.NotEqual(a.HashTaiLieu, b.HashTaiLieu);
    }

    [Fact]
    public void Cung_dau_vao_thi_hash_ON_DINH()
    {
        Assert.Equal(Lap().HashTaiLieu, Lap().HashTaiLieu);
    }

    // ── FAIL-CLOSED: địa chỉ sai ⇒ KHÔNG tạo hoá đơn ──────────────────────

    [Fact]
    public void Xa_KHONG_ton_tai_thi_KHONG_tao_hoa_don()
    {
        var ex = Assert.Throws<BusinessException>(() => Lap("99999"));

        Assert.Equal(AddressErrorCodes.KhongTimThayDonVi, ex.Code);
        // ⬅️ Ném lỗi TRƯỚC khi tạo hoá đơn ⇒ không có hoá đơn rác nào được sinh ra.
    }

    [Fact]
    public void Tinh_KHONG_ton_tai_thi_KHONG_tao_hoa_don()
    {
        var ex = Assert.Throws<BusinessException>(() => Lap("00004", "99"));

        Assert.Equal(AddressErrorCodes.KhongTimThayDonVi, ex.Code);
    }

    [Fact]
    public void Ma_DA_DONG_thi_KHONG_tao_duoc_hoa_don_MOI()
    {
        var donVi = DonVi();
        donVi.Single(u => u.Code == "00004").Dong("Sáp nhập");

        var ex = Assert.Throws<BusinessException>(
            () => PhatHanhHoaDonGd0Planner.Lap(donVi, DauVao()));

        Assert.Equal(AddressErrorCodes.MaDaDong, ex.Code);
    }

    [Fact]
    public void Schema_3_cap_bi_tu_choi_cho_dia_chi_MOI()
    {
        // Luồng GĐ0 chỉ dùng địa chỉ 2 cấp (cấp huyện đã bị bỏ từ 01/7/2025).
        var ex = Assert.Throws<BusinessException>(
            () => PhatHanhHoaDonGd0Planner.Lap(DonVi(), DauVao() with { SchemaVersion = 1 }));

        Assert.Equal(AddressErrorCodes.PhienBanKhongHopLe, ex.Code);
    }

    [Fact]
    public void Thong_diep_loi_co_du_ma_tinh_va_ma_xa_de_go_loi()
    {
        var ex = Assert.Throws<BusinessException>(() => Lap("00004", "79"));

        Assert.Equal("79", ex.Data["tinhCode"]);
        Assert.Equal("00004", ex.Data["xaCode"]);
        Assert.NotNull(ex.Data["thongDiep"]);
    }
}
