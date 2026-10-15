using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// Test bất biến của <see cref="ProductCategory"/> (liên kết sản phẩm ↔ danh mục — `CAT-002` BR-005/006).
///
/// ⚠️ Phần **"tối đa MỘT danh mục chính"** KHÔNG test được ở đây: nó là bất biến **xuyên nhiều bản ghi**
/// nên hàng rào thật là **unique index CÓ FILTER** ở DB ⇒ đã có test tích hợp
/// (`CAT002b_chan_HAI_danh_muc_CHINH_cho_cung_san_pham__BR006`) + **đối chứng** rằng nhiều danh mục
/// **không chính** thì vẫn được phép.
/// </summary>
public class ProductCategoryTests
{
    [Fact]
    public void Tao_voi_ProductId_rong_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(
            () => ProductCategory.Tao(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    [Fact]
    public void Tao_voi_CategoryId_rong_thi_NEM_LOI()
    {
        var loi = Assert.Throws<BusinessException>(
            () => ProductCategory.Tao(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
    }

    [Fact]
    public void Mac_dinh_KHONG_phai_danh_muc_chinh()
    {
        var lienKet = ProductCategory.Tao(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.False(lienKet.IsPrimary);
    }

    [Fact]
    public void DatLamChinh_va_BoChinh_doi_duoc_co()
    {
        var lienKet = ProductCategory.Tao(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isPrimary: true);
        Assert.True(lienKet.IsPrimary);

        lienKet.BoChinh();
        Assert.False(lienKet.IsPrimary);

        lienKet.DatLamChinh();
        Assert.True(lienKet.IsPrimary);
    }
}
