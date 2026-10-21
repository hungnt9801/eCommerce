using eCommerce.Catalog;
using eCommerce.Web.Storefront;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **`/sitemap.xml`** (`CAT-003`) — danh sách URL công khai cho **máy tìm kiếm**.
///
/// **VÌ SAO CẦN VÀ VÌ SAO PHẢI CÔNG KHAI:** `sitemap.xml` là **giao kèo với máy tìm kiếm**: nó giúp Google
/// biết sản phẩm nào **đang bán**, tránh việc phải **dò mò** và — quan trọng hơn — **không** đánh chỉ mục
/// trang **404/ngừng bán**. Máy tìm kiếm **không** đăng nhập được ⇒ endpoint **buộc phải** `[AllowAnonymous]`.
///
/// 🔒 **PHẠM VI RÒ RỈ (điều phải kiểm trước khi mở cửa — `XC-004` BR-004):**
/// endpoint **chỉ** trả: `loc` (URL công khai) + `lastmod` (ngày sửa) của sản phẩm **ĐANG bán**.
/// Nó **KHÔNG** trả: giá, tồn kho, SKU nội bộ, mã sản phẩm, `Id`, tên khách, số điện thoại.
/// ⇒ Mức rò rỉ = **đúng những gì storefront đã công khai**. Không nhận tham số ⇒ không có đường dò.
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT (ghi rõ, không hứa quá):**
/// <list type="bullet">
///   <item>✅ **`S8` — ĐÃ ĐÓNG NỢ `Q9`/`CAT-003`:** danh sách nay lấy từ **đường đọc công khai**
///     `DocSanPhamCongKhai` — tức *"sản phẩm **ĐANG BÁN** (Published) có giá đang hiệu lực"*, **không** còn
///     phụ thuộc app service giá `[Authorize]`. 📌 Trước `S8`, endpoint này **rơi** về *"chỉ còn URL trang chủ"*
///     (đã đo: `AbpAuthorizationException` khi khách ẩn danh gọi) ⇒ sitemap **thiếu** đúng thứ nó sinh ra để có.</item>
///   <item>✅ **`S8` — URL dùng SLUG** (`/SanPham/ChiTiet/{slug}`), đúng `WEB-002` ADR-03. ⚠️ Sản phẩm **chưa đặt
///     slug** vẫn dùng slug **sinh tại chỗ** từ tên (cùng hàm `SinhSlug.Tu` với đường ghi và trang chi tiết) —
///     📌 nếu ba chỗ lệch nhau thì sitemap chỉ đường cho bot tới URL `404`, tệ hơn là không có sitemap.</item>
///   <item>Chưa có `<priority>`/`<changefreq>`/`hreflang` — chưa cần ở GĐ1.</item>
/// </list>
/// </summary>
[AllowAnonymous]
[Route("sitemap.xml")]
public class SitemapController : Controller
{
    private readonly DocSanPhamCongKhai _doc;

    public SitemapController(DocSanPhamCongKhai doc)
    {
        _doc = doc;
    }

    /// <summary>`GET /sitemap.xml` ⇒ `200` + `application/xml`. Danh sách URL sản phẩm **đang bán** (theo slug).</summary>
    /// <remarks>
    /// 🚨 **BÀI HỌC ĐÃ TRẢ GIÁ (đo được 16/10/2026 — KHÔNG đoán):** phiên bản đầu gọi
    /// `IPriceListAppService.GetListAsync()` **trực tiếp** ⇒ app service đó có `[Authorize(...)]`
    /// (`Pricing.Default`) ⇒ khách **chưa đăng nhập** bị `AbpAuthorizationException`
    /// ⇒ endpoint **công khai** trả **`HTTP 500`** (kèm cả stack trace vì đang ở Development).
    /// 📌 **Vì sao đây là lỗi THIẾT KẾ, không phải lỗi nhỏ:** một endpoint *"công khai"* mà **gọi vào** một
    /// app service **cần quyền** thì **không bao giờ** phục vụ được đúng đối tượng của nó — mà nó vẫn
    /// **nằm trong allowlist công khai** ⇒ vừa **hỏng** vừa **mở cửa**. Đúng loại "trông như đã xong".
    /// ✅ **`S8` sửa tận gốc** (không chỉ bắt lỗi): dùng `DocSanPhamCongKhai` — lớp **chỉ đọc repository**,
    /// không quyền ⇒ **không còn** nhánh `catch AbpAuthorizationException` nào (nhánh đó là bằng chứng
    /// *"endpoint công khai đang gọi chỗ cần quyền"*, không phải một tính năng).
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var goc = $"{Request.Scheme}://{Request.Host}";
        var url = new List<(string DuongDan, DateTime? Sua)>();

        var sanPham = await _doc.GetDangBanAsync();

        foreach (var sp in sanPham)
        {
            var slug = string.IsNullOrWhiteSpace(sp.Slug) ? SinhSlug.Tu(sp.Ten) : sp.Slug!;

            if (string.IsNullOrWhiteSpace(slug))
            {
                // ⚠️ Không sinh được slug (tên toàn ký tự đặc biệt) ⇒ **BỎ** khỏi sitemap thay vì phát URL 404.
                continue;
            }

            var duongDan = "/SanPham/ChiTiet/" + Uri.EscapeDataString(slug);

            if (!url.Any(x => string.Equals(x.DuongDan, duongDan, StringComparison.Ordinal)))
            {
                url.Add((duongDan, null));
            }
        }

        var xml = new System.Text.StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        xml.Append($"<url><loc>{System.Net.WebUtility.HtmlEncode(goc + "/SanPham")}</loc></url>");

        foreach (var u in url.OrderBy(x => x.DuongDan, StringComparer.Ordinal))
        {
            xml.Append("<url><loc>");
            xml.Append(System.Net.WebUtility.HtmlEncode(goc + u.DuongDan));
            xml.Append("</loc></url>");
        }

        xml.Append("</urlset>");

        return Content(xml.ToString(), "application/xml; charset=utf-8");
    }
}

/// <summary>
/// 🚨 **`/robots.txt`** (`CAT-003`) — chỉ dẫn cho **máy tìm kiếm**.
///
/// **VÌ SAO CẦN:** không có `robots.txt` thì máy tìm kiếm **tự đoán**; và tệ hơn, nó **có thể** đánh chỉ mục
/// các đường dẫn **quản trị** (`/DonHang`, `/PhanBo`, `/DatHang`) — chúng là trang **nội bộ** cần đăng nhập,
/// được đánh chỉ mục chỉ tạo ra **kết quả tìm kiếm trỏ vào trang lỗi đăng nhập**.
///
/// 🔒 **QUAN TRỌNG — `Disallow` KHÔNG phải cơ chế bảo mật:** nó **chỉ** là *yêu cầu lịch sự* với bot tuân thủ;
/// bot xấu **bỏ qua**. Vì vậy các đường dẫn bị `Disallow` ở đây **vẫn phải** có `[Authorize]` —
/// `robots.txt` **không** thay thế xác thực. 📌 Ghi rõ để không ai "bảo vệ" endpoint bằng cách thêm `Disallow`.
/// </summary>
[AllowAnonymous]
[Route("robots.txt")]
public class RobotsController : Controller
{
    /// <summary>`GET /robots.txt` ⇒ `200` + `text/plain`.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        var goc = $"{Request.Scheme}://{Request.Host}";

        var noiDung = string.Join("\n",
        [
            "User-agent: *",
            "Allow: /SanPham",
            "Allow: /sitemap.xml",
            "",
            "# 🚨 `Disallow` là YÊU CẦU LỊCH SỰ với bot tuân thủ — KHÔNG phải cơ chế bảo mật.",
            "# Mọi đường dẫn dưới đây VẪN phải có [Authorize]; bot xấu bỏ qua robots.txt.",
            "Disallow: /DonHang",
            "Disallow: /PhanBo",
            "Disallow: /DatHang",
            // 🆕 `S8` — TRANG GIẢ LẬP CỔNG THANH TOÁN (chỉ Development). 📌 `Disallow` chỉ là yêu cầu lịch sự:
            // lớp chặn THẬT là `[Authorize]` + `404` khi không phải Development (`PayGiaLapController`).
            "Disallow: /pay-gia-lap",
            "Disallow: /Account",
            "Disallow: /api/",
            "",
            $"Sitemap: {goc}/sitemap.xml",
            string.Empty
        ]);

        return Content(noiDung, "text/plain; charset=utf-8");
    }
}
