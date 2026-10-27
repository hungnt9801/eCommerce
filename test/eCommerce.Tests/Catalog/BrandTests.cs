using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// **THƯƠNG HIỆU** (`CatBrands`, 27/10/2026 — `CAT-002` Q3). Kiểm bất biến **domain**:
/// mã bất biến, slug sinh/chuẩn hoá, trạng thái ngừng dùng không xoá dữ liệu.
/// </summary>
public class BrandTests
{
    [Fact]
    public void Tao_hop_le__chuan_hoa_code_ten_va_sinh_slug()
    {
        var b = new Brand(Guid.NewGuid(), "  SAMSUNG  ", "  Samsung Electronics  ");

        Assert.Equal("SAMSUNG", b.Code);
        Assert.Equal("Samsung Electronics", b.Name);
        Assert.Equal("samsung-electronics", b.Slug);
        Assert.True(b.IsActive);
        Assert.Equal(0, b.SortOrder);
    }

    [Fact]
    public void Slug_tu_chuoi_duoc_truyen__duoc_chuan_hoa()
    {
        var b = new Brand(Guid.NewGuid(), "SS", "Samsung", "  Điện Thoại Samsung  ");

        Assert.Equal("dien-thoai-samsung", b.Slug);
    }

    [Theory]
    [InlineData("", "Samsung")]
    [InlineData("   ", "Samsung")]
    [InlineData("SS", "")]
    [InlineData("SS", "   ")]
    public void Thieu_code_hoac_ten__nem_ma_thieu_thong_tin(string code, string name)
    {
        var loi = Assert.Throws<BusinessException>(() => new Brand(Guid.NewGuid(), code, name));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    [Fact]
    public void Ten_toan_ky_tu_dac_biet__KHONG_bia_slug__nem_ma_rieng()
    {
        // 🔒 Cùng lý do CAT-003 ADR-04: `brand-{guid}` không có giá trị SEO và che dữ liệu.
        var loi = Assert.Throws<BusinessException>(() => new Brand(Guid.NewGuid(), "XX", "!!! ???"));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongSinhDuocSlug, loi.Code);
    }

    [Fact]
    public void CapNhat__doi_ten_slug_mo_ta_logo_thuTu__KHONG_doi_Code()
    {
        var b = new Brand(Guid.NewGuid(), "SS", "Samsung", "samsung");

        b.CapNhat("Samsung Việt Nam", "samsung-vn", "Mô tả", "https://cdn/logo.png", 5);

        Assert.Equal("SS", b.Code);
        Assert.Equal("Samsung Việt Nam", b.Name);
        Assert.Equal("samsung-vn", b.Slug);
        Assert.Equal("Mô tả", b.Description);
        Assert.Equal("https://cdn/logo.png", b.LogoUrl);
        Assert.Equal(5, b.SortOrder);
    }

    [Fact]
    public void Ngung_va_kich_hoat__chi_doi_co__khong_xoa_du_lieu()
    {
        var b = new Brand(Guid.NewGuid(), "SS", "Samsung");

        b.NgungHoatDong();
        Assert.False(b.IsActive);
        Assert.Equal("SS", b.Code);

        b.KichHoat();
        Assert.True(b.IsActive);
    }

    [Fact]
    public void MoTa_qua_dai__nem_ma_thuong_hieu_khong_hop_le()
    {
        var quaDai = new string('x', Brand.DoDaiMoTaToiDa + 1);

        var loi = Assert.Throws<BusinessException>(() => new Brand(Guid.NewGuid(), "SS", "Samsung", null, quaDai));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThuongHieuKhongHopLe, loi.Code);
    }
}
