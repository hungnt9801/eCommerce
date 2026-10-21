using System.Net;
using System.Text;
using eCommerce.Catalog;
using eCommerce.Web.Storefront;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **TRANG HÀNG ĐANG BÁN + CHI TIẾT SẢN PHẨM** (`WEB-002` · `S8` — trước đó là `WEB-001` bước 2).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔓 **CÔNG KHAI TỪ `S8` (quyết định `D10` / `Q6` = A, 17/10/2026):** khách **chưa đăng nhập** xem được
/// `/SanPham` và `/SanPham/ChiTiet/{slugOrSku}`. 🔑 **Vì sao đây không phải "nới lỏng bảo mật":**
/// storefront là **mặt tiền** — máy tìm kiếm **không đăng nhập** được, nên một trang chỉ-đăng-nhập thì
/// slug + `sitemap.xml` (**đã xong** ở `CAT-003`) **không phát huy tác dụng gì**.
/// 📌 Trước `S8`, cả 9 controller của Web đều `[Authorize]` cấp class ⇒ khách nhận `302` về `/Account/Login`.
///
/// 🔒 **BỐN RÀNG BUỘC KHI MỞ (mỗi cái có cơ chế, không chỉ là lời hứa):**
/// <list type="number">
///   <item>**Chỉ hàng ĐANG BÁN** — lọc `Status == Published` **trong truy vấn** ở `DocSanPhamCongKhai`
///     (không lọc ở view). ✅ Ca E2E `S8-W4` đo **chiều ngược**: sản phẩm `Unpublished` ⇒ **`404`**.</item>
///   <item>**KHÔNG lộ `CostPrice`** — DTO công khai **không có** trường đó, và tầng đọc **không** đọc cột đó.
///     ✅ Ca E2E `S8-W3` gieo một giá vốn **có giá trị lạ** rồi khẳng định nó **KHÔNG** có trong HTML.</item>
///   <item>**KHÔNG lộ tồn chi tiết** — tầng đọc **không** đọc `InvItems`; trang **không** hiển thị số tồn.
///     📌 Mức *"còn/hết"* chỉ được thêm khi có đường đọc tồn công khai (`INV-003`) — hôm nay nói **không có**. </item>
///   <item>**Allowlist phải có test** — `AllowlistCongKhaiTests` (đỏ nếu thêm `[AllowAnonymous]` mà không khai;
///     đỏ nếu khai một dòng chết). Mỗi dòng allowlist là **một cửa ra Internet**.</item>
/// </list>
///
/// 🚨 **BẪY ĐÃ TRẢ GIÁ (đo được 16/10/2026 — ghi lại vì `S8` suýt lặp lại):** gọi **app service có `[Authorize]`**
/// (`IPriceListAppService` / `IProductAppService`) từ một hành động **công khai** ⇒ người gọi ẩn danh nhận
/// `AbpAuthorizationException` ⇒ **`HTTP 500`** ở đúng trang cần công khai (xem `SitemapController`).
/// 🔒 Vì vậy controller này **chỉ** dùng `DocSanPhamCongKhai` (đọc repository, không quyền).
/// ✅ Có test quét nguồn khoá lại điều đó (`StorefrontCongKhaiTests`).
///
/// 📌 **Một nguồn sự thật:** giá lấy từ **dòng giá của bảng giá đang hiệu lực** (`PrcPriceListItems`) —
/// **KHÔNG** tự tính ở tầng UI (giá **phải** đến từ `Pricing`, `PRC-001` Q1). Ghi rõ nhãn nguồn theo
/// `BR-008` (chưa có pipeline giá kênh ⇒ hiển thị **giá niêm yết**, **không** trộn hai nguồn).
/// ⚠️ **Trang CHỈ ĐỌC** ⇒ không form ⇒ không cần token chống giả mạo.
/// </summary>
[AllowAnonymous]
public class SanPhamController : Controller
{
    private readonly DocSanPhamCongKhai _doc;

    public SanPhamController(DocSanPhamCongKhai doc)
    {
        _doc = doc;
    }

    /// <summary>`GET /SanPham` — danh sách **sản phẩm đang bán** kèm giá niêm yết thấp nhất mỗi SKU.
    /// 📌 Trả **view Razor** (không nối chuỗi HTML): mọi giá trị do người dùng nhập (tên sản phẩm, mã bảng giá)
    /// được Razor **tự escape** ⇒ không có đường XSS do quên `HtmlEncode` ở từng dòng.</summary>
    public async Task<IActionResult> Index()
    {
        var danhSach = await _doc.GetDangBanAsync();

        return View(danhSach);
    }

    /// <summary>`GET /SanPham/ChiTiet/{slugOrSku}` — **CHI TIẾT SẢN PHẨM + SEO** (`CAT-003` · `WEB-002`).
    ///
    /// 🚨 **HAI ĐƯỜNG ĐỊNH DANH, MỘT `canonical`:**
    /// <list type="number">
    ///   <item><b>slug</b> (`/SanPham/ChiTiet/ao-thun-nam`) — URL **chuẩn**, `canonical` trỏ vào đây;</item>
    ///   <item><b>SKU</b> (`/SanPham/ChiTiet/SKU-001`) — URL **CŨ**, giữ để **không phá link đã chia sẻ/index**
    ///     (`ADR-05`). Vẫn trả `200`, nhưng `canonical` trỏ **slug** ⇒ máy tìm kiếm dồn tín hiệu về URL chuẩn.</item>
    /// </list>
    /// 🚨 **Không tìm thấy / CHƯA publish / CHƯA có giá ⇒ `404`** — **KHÔNG** trả trang rỗng (máy tìm kiếm
    /// sẽ **đánh chỉ mục trang rỗng**); và **không** trả `403`: khách không cần biết *"có sản phẩm nhưng bị ẩn"*.
    /// 🔒 Mọi giá trị đưa vào HTML đều qua `HtmlEncode`; mô tả **KHÔNG** render (ADR-12 — cột `text` tự do
    /// ⇒ render thô là **stored XSS**; muốn render phải có bộ lọc HTML được duyệt trước).
    /// ⚠️ **BẮT BUỘC route TƯỜNG MINH:** route mặc định là `{controller}/{action}/{id?}` ⇒ đoạn thứ ba gán cho
    /// **`id`**, nên tham số tên khác **KHÔNG** được bind từ URL ⇒ **`404` dù dữ liệu có** (đã ĐO được ở vòng 108).
    /// 📌 Đây là lỗi **chỉ lộ ra khi chạy**, và **ca DƯƠNG** mới phát hiện được — ca âm (`404`) đã "đạt" vì lý do sai.
    /// </summary>
    [HttpGet("SanPham/ChiTiet/{slugOrSku}")]
    public async Task<IActionResult> ChiTiet(string slugOrSku)
    {
        if (string.IsNullOrWhiteSpace(slugOrSku))
        {
            return NotFound();
        }

        var sanPham = await _doc.GetTheoSlugHoacSkuAsync(slugOrSku);

        if (sanPham is null)
        {
            return NotFound();
        }

        var giaThapNhat = sanPham.Gia.Min(x => x.DonGia);
        var duongDanChuan = "/SanPham/ChiTiet/" + SlugCongKhai(sanPham);

        var tieuDe = $"{sanPham.Ten} — {giaThapNhat:N0} VND";
        var moTa = $"Giá niêm yết cho {sanPham.Ten}: từ {giaThapNhat:N0} VND ({sanPham.Gia.Count} dòng giá, "
                   + $"{sanPham.Gia.Select(x => x.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count()} biến thể).";

        var than = new StringBuilder();
        than.Append("<h1 id=\"ten-san-pham\">").Append(WebUtility.HtmlEncode(sanPham.Ten)).Append("</h1>");
        than.Append("<p id=\"gia-thap-nhat\">Giá từ <strong>").Append(giaThapNhat.ToString("N0"))
            .Append("</strong> VND</p>");
        than.Append("<p id=\"slug-san-pham\">Slug: ")
            .Append(WebUtility.HtmlEncode(sanPham.Slug ?? "(chưa có)"))
            .Append("</p>");

        than.Append("<table id=\"bang-gia-chi-tiet\"><tr><th>Danh sách giá</th><th>Tên</th><th>SKU</th><th>Đơn giá</th></tr>");

        foreach (var d in sanPham.Gia)
        {
            than.Append("<tr class=\"dong-gia\"><td>").Append(WebUtility.HtmlEncode(d.MaBangGia))
                .Append("</td><td>").Append(WebUtility.HtmlEncode(d.TenBangGia))
                .Append("</td><td>").Append(WebUtility.HtmlEncode(d.Sku))
                .Append("</td><td class=\"so\">").Append(d.DonGia.ToString("N0")).Append("</td></tr>");
        }

        than.Append("</table>");
        than.Append("<p id=\"nguon-gia\">Giá hiển thị là <strong>giá niêm yết</strong> từ bảng giá đang hiệu lực.</p>");
        than.Append("<p><a id=\"quay-lai\" href=\"/SanPham\">← Danh sách hàng đang bán</a></p>");

        var html = "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\">"
                   + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
                   + "<title>" + WebUtility.HtmlEncode(tieuDe) + "</title>"
                   + "<meta name=\"description\" content=\"" + WebUtility.HtmlEncode(moTa) + "\">"
                   + "<link rel=\"canonical\" href=\"" + WebUtility.HtmlEncode(duongDanChuan) + "\">"
                   + "</head><body>" + than + "</body></html>";

        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// 🔑 **SLUG DÙNG LÀM URL** — ưu tiên `Product.Slug` (đã đăng ký, là URL chuẩn); nếu **cột `Slug` là NULL**
    /// (trạng thái **bình thường**: migration không backfill) thì **sinh tại chỗ** từ `Name` bằng **cùng hàm**
    /// của đường ghi (`SinhSlug.Tu`).
    /// 📌 VÌ SAO KHÔNG dùng thẳng SKU làm canonical: `canonical` là **lời khai URL CHUẨN**; trỏ vào SKU nghĩa là
    /// *"URL chuẩn là dạng SKU"* ⇒ máy tìm kiếm **không bao giờ** chuyển sang URL slug, trong khi slug mới là
    /// URL mà storefront/`sitemap` sẽ dùng ⇒ **hai dạng URL tranh nhau** — đúng thứ `canonical` sinh ra để tránh.
    /// ⚠️ Slug tạo tại chỗ **có thể** trùng với sản phẩm khác (chưa kiểm ở tầng ghi) — nợ đã ghi (`CAT-003` §17.5).
    /// </summary>
    private static string SlugCongKhai(SanPhamCongKhaiDto sanPham)
    {
        if (!string.IsNullOrWhiteSpace(sanPham.Slug))
        {
            return sanPham.Slug!;
        }

        var tam = SinhSlug.Tu(sanPham.Ten);

        return tam.Length > 0 ? tam : sanPham.Gia[0].Sku;
    }
}
