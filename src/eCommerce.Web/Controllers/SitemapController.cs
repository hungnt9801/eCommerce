using eCommerce.Catalog;
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
///   <item>Dữ liệu lấy từ **`IPriceListAppService`** — **cùng nguồn** với `/SanPham` — nên danh sách là
///     *"SKU đang được niêm yết giá"*, **không** phải *"mọi sản phẩm Published"* (Catalog chưa có đường đọc
///     công khai riêng cho storefront — nợ đã ghi ở `WEB-002` Q9).</item>
///   <item>Chưa có `<priority>`/`<changefreq>`/`hreflang` — chưa cần ở GĐ1.</item>
///   <item>Slug **có thể** trùng SKU (**chưa** có bảng slug cho từng SKU — lát cắt sau của `CAT-003`):
///     hiện dùng SKU làm định danh URL, và **ghi rõ** ra đây thay vì để người sau tưởng đã có slug.</item>
/// </list>
/// </summary>
[AllowAnonymous]
[Route("sitemap.xml")]
public class SitemapController : Controller
{
    private readonly eCommerce.Pricing.IPriceListAppService _bangGia;
    private readonly ILogger<SitemapController> _logger;

    public SitemapController(eCommerce.Pricing.IPriceListAppService bangGia, ILogger<SitemapController> logger)
    {
        _bangGia = bangGia;
        _logger = logger;
    }

    /// <summary>`GET /sitemap.xml` ⇒ `200` + `application/xml`. Danh sách URL sản phẩm đang bán.</summary>
    /// <remarks>
    /// 🚨 **BÀI HỌC ĐÃ TRẢ GIÁ (đo được 16/10/2026 — KHÔNG đoán):** phiên bản đầu gọi
    /// `IPriceListAppService.GetListAsync()` **trực tiếp** ⇒ app service đó có `[Authorize(...)]`
    /// (`Pricing.Default`) ⇒ khách **chưa đăng nhập** bị `AbpAuthorizationException`
    /// ⇒ endpoint **công khai** trả **`HTTP 500`** (kèm cả stack trace vì đang ở Development).
    /// 📌 **Vì sao đây là lỗi THIẾT KẾ, không phải lỗi nhỏ:** một endpoint *"công khai"* mà **gọi vào** một
    /// app service **cần quyền** thì **không bao giờ** phục vụ được đúng đối tượng của nó — mà nó vẫn
    /// **nằm trong allowlist công khai** ⇒ vừa **hỏng** vừa **mở cửa**. Đúng loại "trông như đã xong".
    /// 🔒 Cách xử lý ở đây: coi **thiếu quyền** là ca **CÓ THỂ XẢY RA** ⇒ trả sitemap **tối thiểu** (chỉ trang chủ)
    /// và ghi `Warning` nói rõ **vì sao** — **không** để lộ `500` ra ngoài, cũng **không** im lặng.
    /// ⏳ **Nợ (ghi ở `cat-003` §16.8):** muốn liệt kê **đủ** sản phẩm cho máy tìm kiếm thì phải có **đường đọc
    /// công khai** (projection) — hiện `Pricing`/`Catalog` **không** có ⇒ cùng nợ `Q9` của `WEB-002`.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var goc = $"{Request.Scheme}://{Request.Host}";
        var url = new List<(string DuongDan, DateTime? Sua)>();

        List<eCommerce.Pricing.Dtos.PriceListDto> cacBangGia;

        try
        {
            cacBangGia = await _bangGia.GetListAsync();
        }
        catch (Volo.Abp.Authorization.AbpAuthorizationException ex)
        {
            // 🔒 KHÔNG nuốt lỗi: nói rõ nguyên nhân + hệ quả, để người vận hành biết sitemap **đang thiếu**.
            _logger.LogWarning(ex,
                "SITEMAP: KHÔNG đọc được danh sách bảng giá vì người gọi CHƯA đăng nhập mà app service giá cần quyền " +
                "⇒ sitemap chỉ còn URL trang chủ. Đây là NỢ ĐÃ GHI (chưa có đường đọc công khai) — KHÔNG phải sự cố. [SITEMAP-THIEU-DOC-CONG-KHAI]");

            cacBangGia = [];
        }

        foreach (var bg in cacBangGia.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            foreach (var item in await _bangGia.GetItemsAsync(bg.Id))
            {
                // ⚠️ Ghi rõ: **SKU** đang là định danh URL (chưa có slug cho từng biến thể).
                var duongDan = "/SanPham/ChiTiet/" + Uri.EscapeDataString(item.Sku);

                if (!url.Any(x => string.Equals(x.DuongDan, duongDan, StringComparison.Ordinal)))
                {
                    url.Add((duongDan, null));
                }
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
            "Disallow: /Account",
            "Disallow: /api/",
            "",
            $"Sitemap: {goc}/sitemap.xml",
            string.Empty
        ]);

        return Content(noiDung, "text/plain; charset=utf-8");
    }
}
