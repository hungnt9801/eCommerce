using eCommerce.Catalog;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🚨 **SINH SLUG SEO** (`CAT-003` **ADR-04**) — hàm **THUẦN**, chỗ **dễ sai nhất** của lát cắt này.
///
/// **VÌ SAO CẦN TEST RIÊNG CHO TIẾNG VIỆT:** slug là **khoá định danh công khai trên URL**. Sai một ký tự
/// ⇒ `404` với khách, hoặc **hai sản phẩm tranh một URL**, hoặc `canonical`/`sitemap` trỏ một dạng còn khách
/// vào dạng khác. Và **không có lỗi nào nổi lên**: URL vẫn "hợp lệ", chỉ là **sai chữ**.
///
/// 🔒 **Luật kiểm:**
///   (1) **bỏ dấu tiếng Việt** kể cả `đ`/`Đ` — ⚠️ `đ` là **một ký tự** (`U+0111`), NFD **không** tách được;
///   (2) chỉ giữ `[a-z0-9-]`; mọi thứ khác → `-`;
///   (3) **gộp** `-` liên tiếp + **cắt** `-` hai đầu;
///   (4) **tất định**: cùng tên ⇒ cùng slug (**không** phụ thuộc culture của máy);
///   (5) tên không còn ký tự dùng được ⇒ **chuỗi rỗng** (chỗ gọi **quyết định**; hàm **không** bịa `guid`);
///   (6) hậu tố số: lần 1 **không** hậu tố, lần 2 ⇒ `-2`, lần 3 ⇒ `-3`; **không** vượt trần độ dài.
/// </summary>
public class SinhSlugTests
{
    // ── (1) BỎ DẤU TIẾNG VIỆT — phần quan trọng nhất ───────────────────────────────────────────────

    [Theory]
    [InlineData("Áo thun nam", "ao-thun-nam")]
    [InlineData("Quần jeans nữ", "quan-jeans-nu")]
    [InlineData("Giày thể thao", "giay-the-thao")]
    [InlineData("Điện thoại thông minh", "dien-thoai-thong-minh")]
    [InlineData("Sản phẩm mới", "san-pham-moi")]
    [InlineData("Tủ lạnh", "tu-lanh")]
    [InlineData("Nồi cơm điện", "noi-com-dien")]
    public void Bo_dau_tieng_Viet(string ten, string mongDoi)
    {
        Assert.Equal(mongDoi, SinhSlug.Tu(ten));
    }

    [Theory]
    [InlineData("Đường", "duong")]        // 🚨 `Đ` = U+0110 — NFD KHÔNG tách được
    [InlineData("đường", "duong")]        // 🚨 `đ` = U+0111 — đây là CÁI BẪY: quên nó ⇒ "ng" (mất chữ)
    [InlineData("Đà Nẵng", "da-nang")]
    [InlineData("ĐỒNG HỒ", "dong-ho")]
    public void Chu_d_gach_ngang__CAI_BAY_chinh(string ten, string mongDoi)
    {
        // Nếu chỉ lọc `NonSpacingMark` mà quên thay `đ`/`Đ` thì ca này cho `"ng"` / `"ng"` — **vẫn hợp lệ**
        // as a slug ⇒ KHÔNG có lỗi nào nổi lên. Test này là lưới duy nhất bắt được.
        Assert.Equal(mongDoi, SinhSlug.Tu(ten));
    }

    // ── (2) KÝ TỰ LẠ → `-` ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Áo & Quần", "ao-quan")]
    [InlineData("Giày (size 42)", "giay-size-42")]
    [InlineData("SP #1 / Loại A", "sp-1-loai-a")]
    [InlineData("100% cotton", "100-cotton")]
    public void Ky_tu_dac_biet_thanh_gach_ngang(string ten, string mongDoi)
    {
        Assert.Equal(mongDoi, SinhSlug.Tu(ten));
    }

    // ── (3) GỘP + CẮT `-` ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Áo   thun", "ao-thun")]              // nhiều khoảng trắng ⇒ MỘT gạch
    [InlineData("  Áo thun  ", "ao-thun")]            // cắt hai đầu
    [InlineData("Áo - - thun", "ao-thun")]            // gạch + khoảng trắng lặp
    [InlineData("---Áo---", "ao")]
    [InlineData("Áo__thun", "ao-thun")]
    public void Gop_va_cat_gach_ngang(string ten, string mongDoi)
    {
        Assert.Equal(mongDoi, SinhSlug.Tu(ten));
        Assert.DoesNotContain("--", SinhSlug.Tu(ten), StringComparison.Ordinal);
        Assert.False(SinhSlug.Tu(ten).StartsWith('-') || SinhSlug.Tu(ten).EndsWith('-'));
    }

    // ── (4) TẤT ĐỊNH + độ dài ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Tat_dinh__cung_ten_cho_cung_slug__khong_phu_thuoc_culture_may()
    {
        var lan1 = SinhSlug.Tu("Điện thoại Samsung Galaxy S24 Ultra");
        var lan2 = SinhSlug.Tu("Điện thoại Samsung Galaxy S24 Ultra");

        Assert.Equal(lan1, lan2);
        Assert.Equal("dien-thoai-samsung-galaxy-s24-ultra", lan1);
    }

    [Fact]
    public void Vuot_tran_do_dai__bi_cat_va_KHONG_de_lai_gach_o_cuoi()
    {
        var ten = new string('a', 700);
        var slug = SinhSlug.Tu(ten);

        Assert.Equal(SinhSlug.DoDaiToiDa, slug.Length);
        Assert.False(slug.EndsWith('-'));
    }

    // ── (5) KHÔNG BỊA: tên vô nghĩa ⇒ chuỗi rỗng ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("---")]
    [InlineData("🎉🎊")]
    public void Ten_khong_con_ky_tu_dung_duoc__tra_CHUOI_RONG__khong_bia_guid(string? ten)
    {
        // 🔒 Cố ý KHÔNG sinh `slug-{guid}`: `Guid` không có giá trị SEO (ADR-04) và sẽ **che** dữ liệu vào
        // vô nghĩa. Chỗ gọi phải TỰ QUYẾT (ném `0027`, hoặc để sản phẩm chưa có URL công khai).
        Assert.Equal(string.Empty, SinhSlug.Tu(ten));
    }

    // ── (6) HẬU TỐ SỐ PHÁ TRÙNG ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Hau_to__lan_1_KHONG_co_hau_to__lan_2_la_2__lan_3_la_3()
    {
        Assert.Equal("ao-thun", SinhSlug.ThemHauTo("ao-thun", 1));
        Assert.Equal("ao-thun-2", SinhSlug.ThemHauTo("ao-thun", 2));
        Assert.Equal("ao-thun-3", SinhSlug.ThemHauTo("ao-thun", 3));
        Assert.Equal("ao-thun-10", SinhSlug.ThemHauTo("ao-thun", 10));
    }

    [Fact]
    public void Hau_to__khong_lam_vuot_tran__va_van_cat_dung_cach()
    {
        var goc = new string('b', SinhSlug.DoDaiToiDa);
        var coHauTo = SinhSlug.ThemHauTo(goc, 2);

        Assert.True(coHauTo.Length <= SinhSlug.DoDaiToiDa);
        Assert.EndsWith("-2", coHauTo, StringComparison.Ordinal);
        Assert.DoesNotContain("--", coHauTo, StringComparison.Ordinal);
    }

    [Fact]
    public void CacUngVien__sinh_dung_thu_tu__va_DUNG_khi_ten_vo_nghia()
    {
        var ungVien = SinhSlug.CacUngVien("Áo thun").Take(4).ToList();

        Assert.Equal(["ao-thun", "ao-thun-2", "ao-thun-3", "ao-thun-4"], ungVien);

        // 🔒 Tên vô nghĩa ⇒ **KHÔNG** có ứng viên nào (không bịa) ⇒ chỗ gọi phải xử lý.
        Assert.Empty(SinhSlug.CacUngVien("!!!").ToList());
    }

    [Fact]
    public void CacUngVien__so_luong_phai_duong()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => SinhSlug.CacUngVien("Áo", toiDa: 0).ToList());
    }
}

/// <summary>Test **đặt slug trên entity** `Product` (`CAT-003` **ADR-05**) — hợp đồng "trả slug CŨ".</summary>
public class ProductSlugTests
{
    private static Product Tao() => new(Guid.NewGuid(), "SP-001", "Áo thun nam");

    [Fact]
    public void DatSlug__lan_dau__tra_NULL_vi_slug_cu_KHONG_CO()
    {
        var sp = Tao();

        // 🔒 `null` ở đây có HAI nghĩa: *"không đổi"* và *"trước đó chưa có slug"*. app service phân biệt bằng
        // cách so `sanPham.Slug` sau khi gọi (xem `DatSlugAsync`). 📌 Ghi rõ để không ai đọc `null` thành "lỗi".
        var cu = sp.DatSlug("ao-thun-nam");

        Assert.Null(cu);
        Assert.Equal("ao-thun-nam", sp.Slug);
    }

    [Fact]
    public void DatSlug__doi_lan_hai__tra_SLUG_CU__de_dung_duoc_301()
    {
        var sp = Tao();
        sp.DatSlug("ao-thun-nam");

        var cu = sp.DatSlug("ao-thun-nam-moi");

        // 🔑 Đây là điều kiện để `ProductSlugChangedEto.OldSlug` có giá trị ⇒ `WEB-002` dựng được 301.
        Assert.Equal("ao-thun-nam", cu);
        Assert.Equal("ao-thun-nam-moi", sp.Slug);
    }

    [Fact]
    public void DatSlug__CUNG_gia_tri__tra_NULL__idempotent__khong_phat_su_kien()
    {
        var sp = Tao();
        sp.DatSlug("ao-thun-nam");

        Assert.Null(sp.DatSlug("ao-thun-nam"));
        Assert.Null(sp.DatSlug("  ao-thun-nam  "));   // khoảng trắng thừa KHÔNG tính là đổi
    }

    [Fact]
    public void DatSlug__khac_HOA_thuong__LA_doi__vi_slug_la_khoa_URL()
    {
        var sp = Tao();
        sp.DatSlug("ao-thun-nam");

        // 🔒 `Ordinal`: `"AO-THUN-NAM"` và `"ao-thun-nam"` là HAI khoá khác nhau ở tầng DB
        // (index `NULLS NOT DISTINCT` không gộp hoa/thường) ⇒ coi là "không đổi" sẽ làm bộ nhớ lệch DB.
        Assert.Equal("ao-thun-nam", sp.DatSlug("AO-THUN-NAM"));
        Assert.Equal("AO-THUN-NAM", sp.Slug);
    }

    [Fact]
    public void DatSlug__rong_hoac_null__GO_slug__san_pham_khong_con_URL_cong_khai()
    {
        var sp = Tao();
        sp.DatSlug("ao-thun-nam");

        Assert.Equal("ao-thun-nam", sp.DatSlug(null));
        Assert.Null(sp.Slug);

        sp.DatSlug("ao-thun-nam");
        Assert.Equal("ao-thun-nam", sp.DatSlug("   "));
        Assert.Null(sp.Slug);
    }

    [Fact]
    public void DatSlug__vuot_do_dai__NEM__khong_de_DB_cat_am_tham()
    {
        var sp = Tao();
        var quaDai = new string('c', SinhSlug.DoDaiToiDa + 1);

        var loi = Assert.Throws<Volo.Abp.BusinessException>(() => sp.DatSlug(quaDai));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
        Assert.Null(sp.Slug);   // 🔒 ném mà KHÔNG để lại thay đổi (bài học "ném mà để lại rác là gieo bug")
    }
}
