using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// Test **vòng đời và bất biến của sản phẩm/biến thể** — `CAT-001` **D2/D3/D5/D9/D11**.
///
/// 🚨 VÌ SAO QUAN TRỌNG: các lỗi ở đây **không có lỗi biên dịch** và **không exception** nếu thiếu guard —
/// chúng chỉ thể hiện thành **dữ liệu sai** (sản phẩm rỗng được publish, SKU trùng, giá âm, mã đổi sau
/// khi đã bán ⇒ báo cáo cũ trỏ sai). `<c>CAT-001</c>` §15.2 ghi **F3** (không có guard nào cho vòng đời)
/// và **F6** (chống trùng SKU yếu hơn DB) — hai phát hiện đó được khoá bằng test dưới đây.
/// </summary>
public class ProductLifecycleTests
{
    private static Product SanPhamMoi(string code = "SP-1", string name = "Sản phẩm 1")
        => new(Guid.NewGuid(), code, name);

    private static Product SanPhamCoBienThe()
    {
        var sp = SanPhamMoi();
        sp.AddVariant(Guid.NewGuid(), "SKU-1", 100_000m);
        return sp;
    }

    // ── Vòng đời hiển thị (D5, đóng F3) ───────────────────────────────────────

    [Fact]
    public void D9_publish_khi_KHONG_CO_bien_the_hoat_dong_thi_NEM_LOI()
    {
        // Sản phẩm không có biến thể KHÔNG BÁN ĐƯỢC. Cho publish ⇒ đẩy trang rỗng ra storefront và
        // bắt Pricing/Inventory xử lý ca rỗng ở mọi nơi.
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.Publish());

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongCoBienTheHoatDong, loi.Code);
        Assert.Equal(ProductStatus.Draft, sp.Status);
        Assert.False(sp.IsPublished);
    }

    [Fact]
    public void D9_publish_khi_moi_bien_the_DA_NGUNG_BAN_thi_NEM_LOI()
    {
        var sp = SanPhamCoBienThe();
        sp.Variants.Single().NgungBan();

        var loi = Assert.Throws<BusinessException>(() => sp.Publish());

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongCoBienTheHoatDong, loi.Code);
    }

    [Fact]
    public void Publish_thanh_cong_khi_co_bien_the_dang_hoat_dong()
    {
        var sp = SanPhamCoBienThe();

        sp.Publish();

        Assert.Equal(ProductStatus.Published, sp.Status);
        Assert.True(sp.IsPublished);   // property dẫn xuất, KHÔNG có setter
    }

    [Fact]
    public void Publish_lan_hai_la_IDEMPOTENT_khong_nem_loi()
    {
        // Worker/retry gọi lặp là bình thường ⇒ "đã ở đúng trạng thái" KHÔNG phải "chuyển sai".
        var sp = SanPhamCoBienThe();
        sp.Publish();

        sp.Publish();

        Assert.Equal(ProductStatus.Published, sp.Status);
    }

    [Fact]
    public void Unpublish_tu_Draft_thi_NEM_LOI()
    {
        var sp = SanPhamCoBienThe();

        var loi = Assert.Throws<BusinessException>(() => sp.Unpublish());

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiKhongHopLe, loi.Code);
    }

    [Fact]
    public void Unpublish_roi_PUBLISH_LAI_duoc()
    {
        var sp = SanPhamCoBienThe();
        sp.Publish();

        sp.Unpublish();
        Assert.Equal(ProductStatus.Unpublished, sp.Status);

        sp.Publish();
        Assert.Equal(ProductStatus.Published, sp.Status);
    }

    [Fact]
    public void Archive_la_trang_thai_CUOI_khong_publish_lai_duoc()
    {
        var sp = SanPhamCoBienThe();
        sp.Publish();

        sp.Archive();
        Assert.Equal(ProductStatus.Archived, sp.Status);

        var loiPublish = Assert.Throws<BusinessException>(() => sp.Publish());
        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiKhongHopLe, loiPublish.Code);

        var loiUnpublish = Assert.Throws<BusinessException>(() => sp.Unpublish());
        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiKhongHopLe, loiUnpublish.Code);
    }

    // ── D2: mã sản phẩm bất biến sau khi publish ──────────────────────────────

    [Fact]
    public void D2_doi_ma_khi_con_DRAFT_thi_duoc()
    {
        var sp = SanPhamMoi();
        sp.DoiMa("SP-MOI");
        Assert.Equal("SP-MOI", sp.Code);
    }

    [Fact]
    public void D2_doi_ma_SAU_KHI_PUBLISH_thi_NEM_LOI()
    {
        // `Code` là khoá đối chiếu đã ra ngoài (báo cáo/đối soát/listing). Đổi sau khi bán ⇒ báo cáo cũ
        // trỏ sai, và KHÔNG có lỗi nào.
        var sp = SanPhamCoBienThe();
        sp.Publish();

        var loi = Assert.Throws<BusinessException>(() => sp.DoiMa("SP-DOI"));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.DaPhatSinhGiaoDich, loi.Code);
        Assert.Equal("SP-1", sp.Code);   // mã KHÔNG đổi
    }

    // ── SKU: chuẩn hoá + chống trùng (D11/BR-002, D3/F6) ──────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SKU CO KHOANG TRANG")]
    [InlineData("SKU/CO/GACH")]
    [InlineData("SKU@LA")]
    public void SKU_khong_hop_le_thi_NEM_LOI(string sku)
    {
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.AddVariant(Guid.NewGuid(), sku, 1m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe, loi.Code);
    }

    [Fact]
    public void SKU_qua_64_ky_tu_thi_NEM_LOI()
    {
        var sp = SanPhamMoi();
        var dai = new string('A', Product.DoDaiSkuToiDa + 1);

        var loi = Assert.Throws<BusinessException>(() => sp.AddVariant(Guid.NewGuid(), dai, 1m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe, loi.Code);
    }

    [Fact]
    public void SKU_duoc_TRIM_khi_them()
    {
        var sp = SanPhamMoi();

        var bt = sp.AddVariant(Guid.NewGuid(), "  SKU-A  ", 1m);

        Assert.Equal("SKU-A", bt.Sku);
    }

    [Fact]
    public void SKU_trung_trong_CUNG_san_pham_thi_NEM_LOI_khong_phan_biet_hoa_thuong()
    {
        var sp = SanPhamMoi();
        sp.AddVariant(Guid.NewGuid(), "SKU-A", 1m);

        var loi = Assert.Throws<BusinessException>(() => sp.AddVariant(Guid.NewGuid(), "sku-a", 1m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SkuDaTonTai, loi.Code);
        Assert.Single(sp.Variants);
    }

    [Fact]
    public void Ma_san_pham_rong_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(() => SanPhamMoi(code: "   "));
        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe, loi.Code);
    }

    [Fact]
    public void Ten_san_pham_rong_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(() => SanPhamMoi(name: "  "));
        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    // ── Tiền (BR-011, quy ước #1/#2) ─────────────────────────────────────────

    [Fact]
    public void Gia_AM_thi_NEM_LOI()
    {
        var sp = SanPhamMoi();

        var loi = Assert.Throws<BusinessException>(() => sp.AddVariant(Guid.NewGuid(), "SKU-X", -1m));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.GiaKhongHopLe, loi.Code);
    }

    [Fact]
    public void DoiGia_tra_ve_TRUE_chi_khi_gia_THUC_SU_doi()
    {
        var sp = SanPhamCoBienThe();
        var bt = sp.Variants.Single();

        Assert.True(bt.DoiGia(120_000m));         // đổi thật
        Assert.Equal(120_000m, bt.Price);
        Assert.False(bt.DoiGia(120_000m));        // không đổi ⇒ KHÔNG phát sự kiện rác
        // ⚠️ `120_000.0000m` = 120000 (KHÁC `120.0000m` = 120!). Đây là ca "cùng giá trị, khác SCALE":
        // `decimal` giữ số chữ số thập phân, nhưng so sánh theo GIÁ TRỊ ⇒ vẫn là "không đổi".
        Assert.False(bt.DoiGia(120_000.0000m));
    }

    [Fact]
    public void DoiGia_am_thi_NEM_LOI_va_giu_nguyen_gia_cu()
    {
        var sp = SanPhamCoBienThe();
        var bt = sp.Variants.Single();

        Assert.Throws<BusinessException>(() => bt.DoiGia(-5m));
        Assert.Equal(100_000m, bt.Price);
    }

    [Fact]
    public void NgungBan_va_MoBanLai_bien_the()
    {
        var sp = SanPhamCoBienThe();
        var bt = sp.Variants.Single();

        bt.NgungBan();
        Assert.False(bt.IsActive);

        bt.MoBanLai();
        Assert.True(bt.IsActive);
    }
}
