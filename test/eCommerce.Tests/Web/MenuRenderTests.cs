using System.Net;
using System.Text.RegularExpressions;
using eCommerce.Web.Navigation;
using Microsoft.AspNetCore.Html;

namespace eCommerce.Tests.Web;

/// <summary>
/// 🚨 **LƯỚI AN TOÀN CHO LỚP RENDER MENU** (`WEB-001`) — khoá **hai lỗi sản phẩm THẬT** đã đo được ở lượt 20.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO FILE NÀY TỒN TẠI — và vì sao trước nó KHÔNG có gì chặn hai lỗi này:**
///
/// Ở lượt 19, `MenuDieuHuong` **đã** khai **mục con**, và `BoLocDieuHuong` **đã** lọc quyền cho con.
/// 🚨 Nhưng `MenuViewComponent` **chỉ render `muc.Url`** ⇒ **mục con KHÔNG BAO GIỜ hiện**, và nhóm
/// "Vận hành" render thành **`href="#"`** — một **liên kết CHẾT**.
///
/// 🔑 **VÌ SAO NÓ LỌT QUA 1101 UNIT TEST + 11 CA PROBE THẬT:**
/// <list type="bullet">
///   <item>**Unit test** chỉ soi **DỮ LIỆU** menu (`MenuDieuHuong`, `BoLocDieuHuong`) — **không** test nào
///     soi **HTML sinh ra**, vì lớp render là `ViewComponent` (cần host/DI);</item>
///   <item>**Probe** chỉ hỏi *"chưa đăng nhập có bị chặn không"* và *"trang lỗi có render không"* — nó
///     **không** hỏi *"menu có mục con không"*.</item>
/// </list>
///
/// ⇒ 🔒 **Cách chặn tái diễn:** `MenuViewComponent.DungHtml` được tách thành **hàm THUẦN** ⇒ test gọi
/// **trực tiếp** và khẳng định **HTML THẬT**. 📌 Đúng khuôn đã trả giá ở `CAT-003` (`SinhSlug`) và
/// `SAL-005` (`LuatWatermark`): **tách lõi quyết định ra hàm thuần ⇒ có test cho MỌI nhánh.**
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class MenuRenderTests
{
    /// <summary>Dựng HTML từ danh sách mục, trả về **chuỗi** để khẳng định.</summary>
    private static string Dung(IReadOnlyList<MucDieuHuong> muc)
    {
        var html = MenuViewComponent.DungHtml(muc);

        using var writer = new StringWriter();
        html.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);

        // 🚨 **PHAI GIAI MA ENTITY TRUOC KHI KHANG DINH** — bai hoc DO DUOC o luot 20:
        // `HtmlEncoder.Default` escape ky tu tieng Viet thanh ENTITY SO (`&#x110;` cho `Đ`) ⇒
        // `Assert.Contains("Hạn mức kênh", html)` that bai **DU chuoi CO trong HTML**.
        // 🔒 Neu khong giai ma thi test nay se DO vi LY DO SAI => roi bi sua bang cach HA TIEU chuan
        // khang dinh (vi du chi kiem `href`) => mat dung ca da bat duoc loi that.
        return WebUtility.HtmlDecode(writer.ToString());
    }

    private static MucDieuHuong La(string nhan, string url, string? quyen = null) => new()
    {
        Nhan = nhan,
        Url = url,
        QuyenCanCo = quyen
    };

    private static MucDieuHuong Nhom(string nhan, params MucDieuHuong[] con) => new()
    {
        Nhan = nhan,
        Url = "#",
        MoTa = "nhóm thử",
        Con = con
    };

    // ── 🚨 LỖI #1 CỦA LƯỢT 19: MỤC CON KHÔNG BAO GIỜ RENDER ──────────────────────────────────

    [Fact]
    public void MUC_CON__phai_RENDER_ra_HTML__khong_chi_nam_trong_du_lieu()
    {
        // 🚨 **CA QUAN TRỌNG NHẤT CỦA FILE.** Đây chính xác là lỗi đã lọt: dữ liệu **có** con, nhưng
        // HTML **không** có con. 🔒 Test này khẳng định **HTML**, không khẳng định dữ liệu.
        var html = Dung([Nhom("Vận hành", La("Hạn mức kênh", "/HanMucKenh"), La("Đối soát thanh toán", "/DoiSoatThanhToan"))]);

        Assert.Contains("href=\"/HanMucKenh\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/DoiSoatThanhToan\"", html, StringComparison.Ordinal);

        // 📌 Và nhãn của con cũng phải có — nếu chỉ khớp `href` thì một `href` "mồ côi" (không nhãn)
        // vẫn qua được, mà đó là liên kết **người dùng không đọc được**.
        Assert.Contains("Hạn mức kênh", html, StringComparison.Ordinal);
        Assert.Contains("Đối soát thanh toán", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MUC_CON__nam_TRONG_long_muc_cha__khong_bi_tach_ra_ngoai()
    {
        // 🔒 Bất biến cấu trúc: con phải **nằm trong** `<li>` của nhóm. 📌 Nếu con bị render **ngang hàng**
        // với nhóm thì menu mất tính hai tầng — người dùng **không biết** mục nào thuộc nhóm nào.
        var html = Dung([Nhom("Vận hành", La("Hạn mức kênh", "/HanMucKenh"))]);

        var viTriNhom = html.IndexOf("Vận hành", StringComparison.Ordinal);
        var viTriCon = html.IndexOf("href=\"/HanMucKenh\"", StringComparison.Ordinal);

        Assert.True(viTriNhom >= 0, "Không thấy nhãn nhóm trong HTML.");
        Assert.True(viTriCon > viTriNhom, "Mục con phải xuất hiện SAU nhãn nhóm (tức nằm trong nhóm).");
    }

    // ── 🚨 LIÊN KẾT CHẾT: NHÓM KHÔNG ĐƯỢC LÀ `a href="#"` ───────────────────────────────────

    [Fact]
    public void MUC_CHA__KHONG_duoc_la_lien_ket_chet_href_hash()
    {
        // 🔒 `href="#"` bấm vào **nhảy về đầu trang** và trông như hỏng. 📌 Nhóm phải là **`span`**
        // (không phải liên kết) — vì nó **không dẫn tới đâu cả**.
        var html = Dung([Nhom("Vận hành", La("Hạn mức kênh", "/HanMucKenh"))]);

        Assert.DoesNotContain("href=\"#\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MUC_CHA__co_role_heading__de_phan_biet_voi_lien_link()
    {
        var html = Dung([Nhom("Vận hành", La("Hạn mức kênh", "/HanMucKenh"))]);

        // 📌 `role="heading"` + `disabled` là **dấu hiệu đo được** rằng đây là **nhãn nhóm**, không phải
        // liên kết. 🔑 Cần nó để một `span` trông-giống-liên-kết không bị người dùng bấm rồi tưởng hỏng.
        Assert.Contains("role=\"heading\"", html, StringComparison.Ordinal);
    }

    // ── BẤT BIẾN CHUNG ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void MOI_muc_LA__co_href__va_URL_khong_bao_gio_la_hash()
    {
        // 🔒 Quét **mọi** mục có `Url` thật (không phải nhóm) trong HTML: không mục nào được trỏ `#`.
        var html = Dung(
        [
            La("Đơn hàng", "/DonHang"),
            La("Sản phẩm", "/SanPham"),
            Nhom("Vận hành", La("Hạn mức kênh", "/HanMucKenh"), La("Đối soát thanh toán", "/DoiSoatThanhToan"))
        ]);

        var cacHref = Regex.Matches(html, "href=\"(?<u>[^\"]*)\"")
            .Select(m => m.Groups["u"].Value)
            .ToList();

        Assert.NotEmpty(cacHref);
        Assert.DoesNotContain("#", cacHref);

        // 📌 Và **mọi** URL khai trong menu phải **có mặt** trong HTML — đây là bất biến hai chiều:
        // nếu ai thêm một nhánh render mới mà **quên** in ra, ca này ĐỎ.
        foreach (var url in new[] { "/DonHang", "/SanPham", "/HanMucKenh", "/DoiSoatThanhToan" })
        {
            Assert.Contains(url, cacHref);
        }
    }

    [Fact]
    public void Menu_RONG__khong_render_gi__khong_co_KHUNG_rong()
    {
        // 🔒 Cùng bất biến với `Invoke()`: không mục nào ⇒ **rỗng thật**. 📌 Một thanh điều hướng **rỗng**
        // làm người dùng tưởng *"hệ thống thiếu menu"* và đi tìm lỗi không tồn tại.
        var html = Dung([]);

        Assert.DoesNotContain("aria-label=\"Điều hướng quản trị\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_CO_muc__co_neo_aria_label__de_probe_HTTP_do_duoc()
    {
        // 🔒 `aria-label` là **neo đo được**: nhờ nó mà phép kiểm HTTP phân biệt *"menu có render"* với
        // *"trang chủ tình cờ chứa chữ Đơn hàng ở chỗ khác"*. 📌 Neo này **đã** dùng ở probe §96/§97.
        var html = Dung([La("Đơn hàng", "/DonHang")]);

        Assert.Contains("aria-label=\"Điều hướng quản trị\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/DonHang\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Nhom_co_N_con__phan_hoi_DU_N_lien_ket(int soCon)
    {
        // 🔒 Chống lỗi **"chỉ render con ĐẦU TIÊN"** (một biến thể của cùng bug): số liên kết con trong HTML
        // phải **đúng bằng** số con khai.
        var con = Enumerable.Range(1, soCon).Select(i => La($"Mục {i}", $"/Muc{i}")).ToArray();

        var html = Dung([Nhom("Nhóm", con)]);

        var soLienKet = Regex.Matches(html, "href=\"/Muc\\d+\"").Count;

        Assert.Equal(soCon, soLienKet);
    }
}
