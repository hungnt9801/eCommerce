using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// Test **bất biến cây danh mục** (`CAT-002` D1/D2/D3/D8/D9).
///
/// 🚨 **VÌ SAO BỘ TEST NÀY QUAN TRỌNG HƠN BÌNH THƯỜNG:** `CAT-002` §8.1 **#1** ghi rõ bất biến
/// *"cây không có chu trình"* là bất biến **DB KHÔNG bảo vệ được** — PostgreSQL không có ràng buộc
/// "cây không chu trình". `CHECK` ở DB chỉ chặn được **định dạng** `Path` và **khoảng** `Level`;
/// phần **ngữ nghĩa** (Path khớp cây thật, không chu trình) **chỉ** có tầng domain + test này lo.
/// Mất nó ⇒ chu trình/hỏng `Path` **im lặng**: hiện ra dưới dạng "menu storefront thiếu mục" rất khó truy.
/// </summary>
public class CategoryTreeTests
{
    private static Category NutGoc(out Guid id, string code = "CAT-1", string name = "Danh mục 1")
    {
        id = Guid.NewGuid();
        return Category.Tao(id, null, code, name);
    }

    // ── D1/D2: Path + Level ───────────────────────────────────────────────────

    [Fact]
    public void Nut_goc_co_Path_bang_chinh_Id_va_Level_0()
    {
        var nut = NutGoc(out var id);

        Assert.Equal($"/{id}/", nut.Path);
        Assert.Equal(0, nut.Level);
        Assert.Null(nut.ParentId);
        Assert.True(nut.IsActive);
    }

    [Fact]
    public void Nut_con_noi_Path_cua_cha_voi_Id_cua_minh()
    {
        var cha = NutGoc(out var idCha);
        var con = Category.Tao(Guid.NewGuid(), cha, "CAT-2", "Danh mục 2");

        Assert.Equal($"/{idCha}/{con.Id}/", con.Path);
        Assert.Equal(1, con.Level);
        Assert.Equal(idCha, con.ParentId);
    }

    [Theory]
    [InlineData("/a/", 0)]
    [InlineData("/a/b/", 1)]
    [InlineData("/a/b/c/", 2)]
    [InlineData("/a/b/c/d/e/f/", 5)]
    public void TinhLevel_dem_so_to_tien(string path, int mongDoi)
    {
        Assert.Equal(mongDoi, Category.TinhLevel(path));
    }

    [Fact]
    public void TinhPath_them_dau_gach_neu_cha_thieu()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"/{id}/", Category.TinhPath(null, id));
        Assert.Equal($"/{id}/", Category.TinhPath("", id));
        Assert.Equal($"/x/{id}/", Category.TinhPath("/x", id));      // cha thiếu '/' cuối ⇒ tự thêm
        Assert.Equal($"/x/{id}/", Category.TinhPath("/x/", id));
    }

    // ── §8.1 #3: độ sâu ───────────────────────────────────────────────────────

    [Fact]
    public void Tao_nut_VUOT_do_sau_thi_NEM_LOI_0024()
    {
        // Dựng chuỗi tới cấp 5 (hợp lệ), rồi tạo cấp 6 ⇒ phải NÉM.
        var nut = NutGoc(out _);

        for (var cap = 1; cap <= Category.DoCaoToiDa; cap++)
        {
            nut = Category.Tao(Guid.NewGuid(), nut, $"CAT-{cap + 1}", $"Danh mục {cap + 1}");
        }

        Assert.Equal(Category.DoCaoToiDa, nut.Level);

        var loi = Assert.Throws<BusinessException>(
            () => Category.Tao(Guid.NewGuid(), nut, "CAT-QUA-SAU", "Quá sâu"));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.VuotDoSauDanhMuc, loi.Code);
    }

    [Fact]
    public void DoiCha_lam_VUOT_do_sau_thi_NEM_LOI_0024()
    {
        var nong = NutGoc(out _, "NONG");
        var sau = Category.Tao(Guid.NewGuid(), nong, "SAU-1", "Sâu 1");

        for (var cap = 2; cap < Category.DoCaoToiDa; cap++)
        {
            sau = Category.Tao(Guid.NewGuid(), sau, $"SAU-{cap}", $"Sâu {cap}");
        }

        // `sau` đang ở cấp 4; gắn vào `nong` (cấp 0) ⇒ cấp 1 (được); nhưng gắn vào chuỗi sâu hơn thì không.
        var sauHon = Category.Tao(Guid.NewGuid(), sau, "SAU-CUOI", "Sâu cuối");
        Assert.Equal(Category.DoCaoToiDa, sauHon.Level);

        // Gắn `sauHon` vào `nong` là HỢP LỆ (nông hơn), nhưng gắn vào chính `sau` thì không đổi.
        // Ca vượt độ sâu thật sự do `CategoryTreeService` xử lý (phải nhìn thấy CHIỀU CAO cây con).
        var loi = Assert.Throws<BusinessException>(
            () => Category.Tao(Guid.NewGuid(), sauHon, "SAU-QUA", "Quá sâu"));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.VuotDoSauDanhMuc, loi.Code);
    }

    // ── §8.1 #1: CHU TRÌNH (bất biến DB KHÔNG bảo vệ được) ───────────────────

    [Fact]
    public void LaToTienCua_nhan_dien_hau_due_theo_tien_to_Path()
    {
        var ong = NutGoc(out _);
        var cha = Category.Tao(Guid.NewGuid(), ong, "CHA", "Cha");
        var con = Category.Tao(Guid.NewGuid(), cha, "CON", "Con");

        Assert.True(Category.LaToTienCua(ong.Path, cha.Path));
        Assert.True(Category.LaToTienCua(ong.Path, con.Path));
        Assert.True(Category.LaToTienCua(cha.Path, con.Path));

        Assert.False(Category.LaToTienCua(con.Path, cha.Path));   // con KHÔNG là tổ tiên của cha
        Assert.False(Category.LaToTienCua(ong.Path, ong.Path));   // một nút KHÔNG là tổ tiên của CHÍNH NÓ
    }

    [Fact]
    public void DoiTienToPath_dung_lai_duong_dan_cho_ca_cay_con()
    {
        var pathCu = "/a/b/";
        var pathMoi = "/a/c/b/";

        Assert.Equal("/a/c/b/d/", Category.DoiTienToPath("/a/b/d/", pathCu, pathMoi));
        Assert.Equal("/a/c/b/d/e/", Category.DoiTienToPath("/a/b/d/e/", pathCu, pathMoi));

        // ⚠️ Không phải hậu duệ ⇒ trả `null` (người gọi PHẢI xử lý) — không được dựng ra đường dẫn rác.
        Assert.Null(Category.DoiTienToPath("/x/y/", pathCu, pathMoi));
    }

    // ── BR-002: Path phải khớp Level (lỗi lập trình phải LỘ RA, không tự sửa) ──

    [Fact]
    public void DatLaiPath_voi_Level_KHONG_khop_thi_NEM_InvalidOperationException()
    {
        // 🚨 Đây là ca `CAT-002` §12 dòng cuối: "Path không khớp Level ⇒ NÉM InvalidOperationException
        // (lỗi lập trình, PHẢI lộ ra CI), KHÔNG tự sửa im lặng".
        var nut = NutGoc(out var id);

        var loi = Assert.Throws<InvalidOperationException>(
            () => nut.DatLaiPath($"/{Guid.NewGuid()}/{id}/", 0));   // Path nói cấp 1, tham số nói cấp 0

        Assert.Contains("KHÔNG khớp", loi.Message);
    }

    [Fact]
    public void DatLaiPath_khong_ket_thuc_bang_Id_cua_chinh_no_thi_NEM()
    {
        var nut = NutGoc(out _);

        var loi = Assert.Throws<InvalidOperationException>(
            () => nut.DatLaiPath($"/{Guid.NewGuid()}/", 0));

        Assert.Contains("KẾT THÚC bằng chính Id", loi.Message);
    }

    [Fact]
    public void Path_khong_dung_dinh_dang_thi_NEM()
    {
        var nut = NutGoc(out _);

        var loi = Assert.Throws<InvalidOperationException>(() => nut.DatLaiPath("khong-co-gach", 0));

        Assert.Contains("bắt đầu và kết thúc bằng '/'", loi.Message);
    }

    [Fact]
    public void DatLaiPath_HOP_LE_thi_cap_nhat_ca_Path_lan_Level()
    {
        var nut = NutGoc(out var id);
        var moi = $"/{Guid.NewGuid()}/{Guid.NewGuid()}/{id}/";

        nut.DatLaiPath(moi, 2);

        Assert.Equal(moi, nut.Path);
        Assert.Equal(2, nut.Level);
    }

    // ── DoiCha: dựng lại Path/Level ───────────────────────────────────────────

    [Fact]
    public void DoiCha_dung_lai_Path_va_Level_theo_cha_moi()
    {
        var chaA = NutGoc(out _, "A");
        var chaB = NutGoc(out var idB, "B");
        var con = Category.Tao(Guid.NewGuid(), chaA, "CON", "Con");

        con.DoiCha(chaB);

        Assert.Equal(idB, con.ParentId);
        Assert.Equal($"/{idB}/{con.Id}/", con.Path);
        Assert.Equal(1, con.Level);
    }

    [Fact]
    public void DoiCha_ve_null_thi_thanh_NUT_GOC()
    {
        var cha = NutGoc(out _);
        var con = Category.Tao(Guid.NewGuid(), cha, "CON", "Con");

        con.DoiCha(null);

        Assert.Null(con.ParentId);
        Assert.Equal($"/{con.Id}/", con.Path);
        Assert.Equal(0, con.Level);
    }

    // ── Trạng thái + dữ liệu bắt buộc ─────────────────────────────────────────

    [Fact]
    public void Code_hoac_Ten_rong_thi_NEM_LOI()
    {
        Assert.Equal(
            eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => Category.Tao(Guid.NewGuid(), null, "  ", "Tên")).Code);

        Assert.Equal(
            eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => Category.Tao(Guid.NewGuid(), null, "MA", "  ")).Code);
    }

    [Fact]
    public void NgungDung_va_KichHoat_lai()
    {
        var nut = NutGoc(out _);

        nut.NgungDung();
        Assert.False(nut.IsActive);

        nut.KichHoat();
        Assert.True(nut.IsActive);
    }

    [Fact]
    public void DoiTen_va_DatThuTu()
    {
        var nut = NutGoc(out _);

        nut.DoiTen("  Tên mới  ");
        nut.DatThuTu(7);

        Assert.Equal("Tên mới", nut.Name);   // có TRIM
        Assert.Equal(7, nut.SortOrder);
    }

    [Fact]
    public void NguongToHop_mac_dinh_la_200_va_ghi_de_duoc()
    {
        var nut = NutGoc(out _);

        Assert.Null(nut.MaxVariantCombinations);       // NULL = dùng mặc định
        Assert.Equal(200, Category.NguongToHopMacDinh);

        nut.MaxVariantCombinations = 50;
        Assert.Equal(50, nut.MaxVariantCombinations);
    }

    // ── Khoá phân tán (D8) ────────────────────────────────────────────────────

    [Fact]
    public void Khoa_cay_danh_muc_KHONG_chua_tien_to_ecommerce()
    {
        // 🚨 `XC-003` §5.4: tên khoá do module sinh KHÔNG chứa tiền tố (ABP ghép `KeyPrefix`).
        // Tự thêm tiền tố ⇒ **tiền tố đôi** ⇒ khoá thật khác dự kiến ⇒ **khoá không bảo vệ gì** (lỗi C13/F3).
        var tenant = Guid.NewGuid();
        var khoa = eCommerceConsts.LockKeys.CategoryTree(tenant);

        Assert.Equal($"catalog:category-tree:{tenant}", khoa);
        Assert.DoesNotContain("ecommerce:", khoa, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Khoa_cay_danh_muc_voi_tenant_null_van_ON_DINH()
    {
        // Hệ thống hiện chạy 1 tenant ⇒ `TenantId` là NULL là ca THẬT, không phải ca biên.
        Assert.Equal("catalog:category-tree:null", eCommerceConsts.LockKeys.CategoryTree(null));
    }
}
