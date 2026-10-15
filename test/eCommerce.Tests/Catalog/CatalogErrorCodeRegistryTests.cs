using System.Reflection;
using System.Text.RegularExpressions;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🛡️ **BẤT BIẾN CỦA SỔ ĐĂNG KÝ MÃ LỖI CATALOG** — nguồn sự thật duy nhất.
///
/// 🚨 VÌ SAO CÓ FILE NÀY (phát hiện thật 15/10/2026): hai tài liệu `CAT-001` **§6.3** và `CAT-002` **§12**
/// gán **CÙNG dải số** `eCommerce:Catalog:0001…0009` cho **NGHĨA KHÁC NHAU**:
///   • `0001`: *"Code sản phẩm trùng"* (CAT-001) **vs** *"chu trình cây danh mục"* (CAT-002)
///   • `0002`: *"SKU trùng"* (CAT-001) **vs** *"xoá danh mục còn con"* (CAT-002)
///   • `0003`: *"Publish thiếu biến thể"* (CAT-001) **vs** *"vượt trần tổ hợp"* (CAT-002)
///
/// Nếu cứ thế triển khai thì **cùng một mã mang hai nghĩa** ⇒ client và đối soát **không thể** phân loại
/// lỗi ⇒ đúng họ lỗi *"hai nguồn sự thật cho cùng một mã"* mà `XC-003` F2/F5 đã gặp.
///
/// Test dùng **reflection** nên nó tự bảo vệ **mọi mã thêm sau này**, không cần cập nhật test.
/// </summary>
public class CatalogErrorCodeRegistryTests
{
    private static readonly FieldInfo[] CacHangSo = typeof(eCommerceConsts.ErrorCodes.Catalog)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .OrderBy(f => f.Name, StringComparer.Ordinal)
        .ToArray();

    private static string GiaTri(FieldInfo f) => (string)f.GetRawConstantValue()!;

    [Fact]
    public void So_dang_ky_phai_CO_ma__neu_khong_thi_test_nay_vo_nghia()
    {
        Assert.NotEmpty(CacHangSo);
    }

    [Fact]
    public void Moi_ma_phai_dung_dinh_dang_eCommerce_Catalog_4_chu_so()
    {
        var khuon = new Regex(@"^eCommerce:Catalog:\d{4}$", RegexOptions.Compiled);

        foreach (var f in CacHangSo)
        {
            Assert.True(khuon.IsMatch(GiaTri(f)),
                $"Mã `{f.Name}` = `{GiaTri(f)}` sai định dạng `eCommerce:Catalog:nnnn`.");
        }
    }

    [Fact]
    public void KHONG_co_hai_hang_so_nao_TRUNG_GIA_TRI()
    {
        // 🚨 Đây là lưới an toàn chính: thêm một mã mới "tiện tay" dùng lại số cũ ⇒ CI ĐỎ ngay,
        // thay vì để hai nghĩa chung một mã lọt vào production rồi mới phát hiện lúc đối soát.
        var trung = CacHangSo
            .GroupBy(GiaTri, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ← dùng bởi: {string.Join(", ", g.Select(f => f.Name))}")
            .ToList();

        Assert.True(trung.Count == 0,
            "🚨 Có mã lỗi Catalog bị TRÙNG GIÁ TRỊ (hai nguồn sự thật cho cùng một mã):\n  " +
            string.Join("\n  ", trung));
    }

    [Fact]
    public void KHONG_co_hang_so_nao_rong()
    {
        foreach (var f in CacHangSo)
        {
            Assert.False(string.IsNullOrWhiteSpace(GiaTri(f)), $"Mã `{f.Name}` rỗng.");
        }
    }

    [Theory]
    // Vị trí đã CHỐT theo `CAT-001` §6.3 — đây là **hợp đồng**, đổi số là thay đổi hợp đồng với client.
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.MaSanPhamDaTonTai), "eCommerce:Catalog:0001")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.SkuDaTonTai), "eCommerce:Catalog:0002")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.KhongCoBienTheHoatDong), "eCommerce:Catalog:0003")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.ChuyenTrangThaiKhongHopLe), "eCommerce:Catalog:0004")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.DaPhatSinhGiaoDich), "eCommerce:Catalog:0005")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.SanPhamConThamChieu), "eCommerce:Catalog:0006")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.GiaKhongHopLe), "eCommerce:Catalog:0007")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.SkuKhongHopLe), "eCommerce:Catalog:0008")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.VuotTranToHopBienThe), "eCommerce:Catalog:0009")]
    // Dải 0020+ đánh số LẠI cho `CAT-002` (tài liệu §12 đã được cập nhật).
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.ChuTrinhDanhMuc), "eCommerce:Catalog:0020")]
    [InlineData(nameof(eCommerceConsts.ErrorCodes.Catalog.DanhMucConRangBuoc), "eCommerce:Catalog:0021")]
    public void Vi_tri_ma_theo_tai_lieu(string tenHangSo, string giaTriMongDoi)
    {
        var f = CacHangSo.SingleOrDefault(x => x.Name == tenHangSo);

        Assert.True(f is not null, $"Không tìm thấy hằng số `{tenHangSo}` trong sổ đăng ký.");
        Assert.Equal(giaTriMongDoi, GiaTri(f!));
    }

    [Fact]
    public void Ma_KHOA_PHAN_TAN_dung_CHUNG__khong_duoc_nam_trong_dai_Catalog()
    {
        // 🚨 `XC-003` D10: quá timeout giành khoá là lỗi **hạ tầng dùng chung**, phải có **MỘT** mã cho mọi
        // module. Lúc đầu tôi tự chế `eCommerce:Catalog:0011` ⇒ vi phạm + hai nguồn sự thật cho cùng
        // một nguyên nhân. Test này khoá lại đúng quy ước.
        Assert.Equal("eCommerce:Lock:0001", eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa);

        var trongDaiCatalog = CacHangSo.Any(f =>
            string.Equals(GiaTri(f), eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa, StringComparison.Ordinal));

        Assert.False(trongDaiCatalog,
            "Mã khoá phân tán KHÔNG được nằm trong dải `eCommerce:Catalog:*` — nó là lỗi dùng chung (`XC-003` D10).");
    }

    [Fact]
    public void Dai_0020_phai_danh_cho_CAT002()
    {
        var dai0020 = CacHangSo
            .Where(f => GiaTri(f).StartsWith("eCommerce:Catalog:002", StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.NotEmpty(dai0020);
    }
}
