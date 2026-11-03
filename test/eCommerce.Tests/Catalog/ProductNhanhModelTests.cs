using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// **MÔ HÌNH HÀNG HÓA THEO NHANH** — WP-03 nửa đầu (`03 §3.1`, D-07).
///
/// 🚨 VÌ SAO PHẢI KHOÁ BẰNG TEST: mọi bất biến ở đây **không có lỗi biên dịch** và **không ném lỗi** nếu thiếu
/// guard — chúng chỉ thể hiện thành **dữ liệu sai**: hai ĐVT gốc (quy đổi tồn kho theo hai hệ số), hai dòng
/// giá cho cùng "thùng", barcode trùng (POS bán sai món), VAT ngoài khoảng (hoá đơn sai thuế). DB có
/// `CHECK`/unique index làm hàng rào cuối, nhưng **test này chạy không cần DB** ⇒ bắt lỗi ngay trong CI.
/// </summary>
public class ProductNhanhModelTests
{
    private static Product SanPhamMoi(string code = "SP-1") => new(Guid.NewGuid(), code, "Sản phẩm 1");

    // ── Giá trị mặc định: additive, dữ liệu cũ không phải backfill ─────────────

    [Fact]
    public void SanPham_moi_mac_dinh_la_Standard_va_VAT_chua_gom()
    {
        var sp = SanPhamMoi();

        Assert.Equal(ProductType.Standard, sp.Type);
        Assert.Equal(VatMode.Excluded, sp.VatMode);
        Assert.Null(sp.VatPercent);
        Assert.Null(sp.ParentId);
        Assert.False(sp.IsHot);
        Assert.False(sp.IsNew);
        Assert.Empty(sp.Units);
    }

    // ── Cha–con (D-07) ─────────────────────────────────────────────────────────

    [Fact]
    public void DatCha_null_la_hop_le_va_idempotent()
    {
        var sp = SanPhamMoi();

        sp.DatCha(null);
        sp.DatCha(null);

        Assert.Null(sp.ParentId);
    }

    [Fact]
    public void DatCha_chinh_minh_thi_NEM_LOI_0043()
    {
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.DatCha(sp.Id));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SanPhamChaTrungChinhNo, loi.Code);
    }

    // ── % VAT: -100..100, có 3 giá trị đặc biệt của Nhanh ──────────────────────

    [Theory]
    [InlineData(-100)]   // thuế 0%
    [InlineData(-2)]     // không kê khai
    [InlineData(-1)]     // không chịu thuế
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(100)]
    public void DatVatPercent_trong_khoang_la_hop_le(decimal phanTram)
    {
        var sp = SanPhamMoi();

        sp.DatVatPercent(phanTram);

        Assert.Equal(phanTram, sp.VatPercent);
    }

    [Theory]
    [InlineData(-101)]
    [InlineData(101)]
    [InlineData(-1000)]
    public void DatVatPercent_ngoai_khoang_thi_NEM_LOI_0045(decimal phanTram)
    {
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.DatVatPercent(phanTram));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.VatPercentKhongHopLe, loi.Code);
        Assert.Null(sp.VatPercent);   // KHÔNG ghi giá trị sai vào bộ nhớ
    }

    [Fact]
    public void VatPercent_null_duoc_phep__nghia_la_CHUA_KHAI()
    {
        var sp = SanPhamMoi();

        sp.DatVatPercent(null);

        Assert.Null(sp.VatPercent);
    }

    // ── ĐVT: bất biến "đúng MỘT gốc" + hệ số > 0 + tên/barcode không trùng ─────

    [Fact]
    public void DonVi_dau_tien_TU_thanh_goc()
    {
        var sp = SanPhamMoi();

        var donVi = sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m);

        Assert.True(donVi.IsBase);
        Assert.Single(sp.Units);
    }

    [Fact]
    public void Ba_DVT_thi_chi_CO_MOT_goc_va_he_so_giu_nguyen()
    {
        var sp = SanPhamMoi();

        sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m);
        sp.AddUnit(Guid.NewGuid(), "thùng", 24m, 220_000m);
        sp.AddUnit(Guid.NewGuid(), "kiện", 24m * 6m, 1_250_000m);

        Assert.Equal(3, sp.Units.Count);
        Assert.Single(sp.Units, x => x.IsBase);
        Assert.Equal("lon", sp.Units.Single(x => x.IsBase).Name);
        Assert.Equal(24m, sp.Units.Single(x => x.Name == "thùng").ConversionFactor);
    }

    [Fact]
    public void Danh_dau_goc_lan_hai_thi_NEM_LOI_0042()
    {
        var sp = SanPhamMoi();
        sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m);

        var loi = Assert.Throws<BusinessException>(
            () => sp.AddUnit(Guid.NewGuid(), "thùng", 24m, 220_000m, isBase: true));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.NhieuHonMotDonViGoc, loi.Code);
        Assert.Single(sp.Units);   // KHÔNG thêm dòng sai
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-24)]
    public void He_so_quy_doi_khong_duong_thi_NEM_LOI_0040(decimal heSo)
    {
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.AddUnit(Guid.NewGuid(), "thùng", heSo, 220_000m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe, loi.Code);
        Assert.Empty(sp.Units);
    }

    [Fact]
    public void Ten_DVT_trung_khong_phan_biet_hoa_thuong_va_khoang_trang_thi_NEM_LOI_0040()
    {
        var sp = SanPhamMoi();
        sp.AddUnit(Guid.NewGuid(), "Thùng", 24m, 220_000m);

        var loi = Assert.Throws<BusinessException>(
            () => sp.AddUnit(Guid.NewGuid(), "  thùng  ", 24m, 220_000m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.DonViTinhKhongHopLe, loi.Code);
    }

    [Fact]
    public void Barcode_trung_trong_cung_san_pham_thi_NEM_LOI_0041()
    {
        var sp = SanPhamMoi();
        sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m, barcode: "8935000000011");

        var loi = Assert.Throws<BusinessException>(
            () => sp.AddUnit(Guid.NewGuid(), "thùng", 24m, 220_000m, barcode: "8935000000011"));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.BarcodeDaTonTai, loi.Code);
    }

    [Fact]
    public void Nhieu_DVT_KHONG_co_barcode_van_hop_le__rong_duoc_chuan_hoa_thanh_null()
    {
        var sp = SanPhamMoi();

        sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m, barcode: "   ");
        sp.AddUnit(Guid.NewGuid(), "thùng", 24m, 220_000m);

        Assert.All(sp.Units, x => Assert.Null(x.Barcode));
    }

    [Fact]
    public void Gia_ban_am_thi_NEM_LOI_0007()
    {
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.AddUnit(Guid.NewGuid(), "lon", 1m, -1m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.GiaKhongHopLe, loi.Code);
    }

    [Fact]
    public void DoiGiaBan_am_thi_NEM_LOI_va_giu_gia_cu()
    {
        var sp = SanPhamMoi();
        var donVi = sp.AddUnit(Guid.NewGuid(), "lon", 1m, 10_000m);

        var loi = Assert.Throws<BusinessException>(() => donVi.DoiGiaBan(-5m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.GiaKhongHopLe, loi.Code);
        Assert.Equal(10_000m, donVi.SalePrice);
    }

    // ── IMEI: enum có KHOẢNG TRỐNG số 4 (đúng dữ liệu Nhanh) ───────────────────

    [Fact]
    public void Imei_moi_mac_dinh_la_New_va_serial_duoc_trim()
    {
        var imei = new ProductImei(Guid.NewGuid(), Guid.NewGuid(), "  356789012345678  ");

        Assert.Equal(ProductImeiStatus.New, imei.Status);
        Assert.Equal("356789012345678", imei.Serial);
    }

    [Fact]
    public void ImeiStatus_KHONG_co_gia_tri_so_4__giu_khoang_trong_du_lieu_Nhanh()
    {
        // 🚨 Nếu ai "lấp" số 4 bằng một giá trị tự chế thì dữ liệu Nhanh nhập vào sẽ mang nghĩa khác —
        // không lỗi biên dịch, không lỗi runtime, chỉ là **hai hệ thống hiểu một con số theo hai kiểu**.
        Assert.False(Enum.IsDefined(typeof(ProductImeiStatus), 4));
        Assert.Equal(10, Enum.GetValues<ProductImeiStatus>().Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Imei_serial_rong_thi_NEM_LOI(string serial)
    {
        var loi = Assert.Throws<BusinessException>(
            () => new ProductImei(Guid.NewGuid(), Guid.NewGuid(), serial));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    // ── Lô ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Lo_rong_ma_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(
            () => new ProductBatch(Guid.NewGuid(), Guid.NewGuid(), "  "));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    [Fact]
    public void Lo_ngung_dung_la_xoa_mem_khong_xoa_cung()
    {
        var lo = new ProductBatch(Guid.NewGuid(), Guid.NewGuid(), "LO-01");

        lo.NgungDung();

        Assert.False(lo.IsActive);
    }

    // ── Combo / gói: số lượng > 0, không chứa chính mình ──────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Thanh_phan_combo_so_luong_khong_duong_thi_NEM_LOI_0044(decimal soLuong)
    {
        var loi = Assert.Throws<BusinessException>(
            () => new ComboComponent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), soLuong));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SoLuongKhongHopLe, loi.Code);
    }

    [Fact]
    public void Thanh_phan_goi_so_luong_khong_duong_thi_NEM_LOI_0044()
    {
        var loi = Assert.Throws<BusinessException>(
            () => new PackageComponent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SoLuongKhongHopLe, loi.Code);
    }

    [Fact]
    public void Combo_chua_chinh_minh_thi_NEM_LOI_0046()
    {
        var id = Guid.NewGuid();

        var loi = Assert.Throws<BusinessException>(
            () => new ComboComponent(Guid.NewGuid(), id, id, 1m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThanhPhanKhongHopLe, loi.Code);
    }

    // ── Nhãn ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Nhan_rong_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(
            () => new ProductTag(Guid.NewGuid(), Guid.NewGuid(), "  "));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    [Fact]
    public void Nhan_duoc_trim()
    {
        var tag = new ProductTag(Guid.NewGuid(), Guid.NewGuid(), "  hàng mới  ");

        Assert.Equal("hàng mới", tag.Name);
    }
}
