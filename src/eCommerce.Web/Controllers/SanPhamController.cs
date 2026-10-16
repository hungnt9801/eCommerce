using eCommerce.Catalog;
using eCommerce.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **TRANG HÀNG ĐANG BÁN + ĐƠN GIÁ** (`WEB-001` — bước **2** của chuỗi mặt tiền, sau `GET /PhanBo`).
///
/// **VÌ SAO TRANG NÀY:** đây là **điểm vào** của việc mua hàng — khách cần thấy *"có hàng gì, giá bao nhiêu"*
/// **trước khi** giỏ hàng/đặt hàng có nghĩa. 📌 Dữ liệu lấy từ **cùng `IPriceListAppService`** mà API dùng
/// ⇒ **một nguồn sự thật** (không tự tính giá ở tầng UI — giá **phải** đến từ `Pricing`, `PRC-001` Q1).
///
/// 🔐 `[Authorize]` **cấp class** — quy ước #4. ⚠️ **Không** phải bảo mật dữ liệu: app service vẫn tự kiểm quyền.
///
/// 📌 **`Pricing` đã được nạp ở Web** (vòng 58 — bài học *"dùng app service của module nào ⇒ phải nạp module đó ở Web"*)
/// nên `IPriceListAppService` **dựng được** ở đây. Đây là **lần đầu quy tắc đó được áp dụng có chủ đích** thay vì
/// phát hiện sau khi `HTTP 500`.
/// </summary>
[Authorize]
public class SanPhamController : Controller
{
    private readonly IPriceListAppService _bangGia;
    private readonly IProductAppService _sanPham;
    private readonly ILogger<SanPhamController> _logger;

    public SanPhamController(IPriceListAppService bangGia, IProductAppService sanPham, ILogger<SanPhamController> logger)
    {
        _bangGia = bangGia;
        _sanPham = sanPham;
        _logger = logger;
    }

    /// <summary>`GET /SanPham` — bảng **(danh sách giá × SKU × đơn giá)**.</summary>
    public async Task<IActionResult> Index()
    {
        var bangGiaList = await _bangGia.GetListAsync();

        var dong = new List<SanPhamDongDto>();

        foreach (var bg in bangGiaList.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            var items = await _bangGia.GetItemsAsync(bg.Id);

            dong.AddRange(items
                .OrderBy(x => x.Sku, StringComparer.Ordinal)
                .Select(x => new SanPhamDongDto
                {
                    MaBangGia = bg.Code,
                    TenBangGia = bg.Name,
                    Sku = x.Sku,
                    DonGia = x.Amount
                }));
        }

        return View(dong);
    }

    /// <summary>`GET /SanPham/ChiTiet/{slugOrSku}` — **CHI TIẾT SẢN PHẨM + SEO** (`CAT-003` · nợ của `WEB-002`).
    ///
    /// 🚨 **`CAT-003` — HAI ĐƯỜNG ĐỊNH DANH, MỘT `canonical`:**
    /// <list type="number">
    ///   <item><b>slug</b> (`/SanPham/ChiTiet/ao-thun-nam`) — URL **chuẩn**, `canonical` trỏ vào đây;</item>
    ///   <item><b>SKU</b> (`/SanPham/ChiTiet/SKU-001`) — URL **CŨ**, giữ để **không phá link đã chia sẻ/index**
    ///     (`ADR-05`: bỏ đột ngột ⇒ mọi link cũ `404` + mất SEO đã tích luỹ). Vẫn trả `200`, nhưng `canonical`
    ///     trỏ **slug** ⇒ máy tìm kiếm dần dồn tín hiệu về URL chuẩn (**không** tạo hai bản trùng nội dung).</item>
    /// </list>
    /// 📌 **Một nguồn sự thật:** giá lấy từ **cùng `IPriceListAppService`** như `/SanPham` — **KHÔNG** tự tính ở tầng UI
    /// (giá **phải** đến từ `Pricing`, `PRC-001` Q1). Tra slug/SKU qua `IProductAppService` của **Catalog**
    /// (không tự truy vấn bảng `CatProducts` ⇒ không tạo nguồn sự thật thứ hai — `CAT-003` ADR-06).
    /// 🔎 **SEO:** `<title>` · `<meta name="description">` · `<link rel="canonical">`.
    /// ⚠️ Trang **CHỈ ĐỌC** ⇒ **không** có form ⇒ **không** cần token chống giả mạo (bài học vòng 73 · 86).
    /// 🚨 **Không có giá niêm yết ⇒ `404`** — **KHÔNG** trả trang rỗng (tránh máy tìm kiếm **đánh chỉ mục trang rỗng**).
    /// 🔒 Mọi giá trị đưa vào HTML đều qua `HtmlEncode` (giá trị do người dùng nhập ⇒ **không** nối chuỗi thô).</summary>
    /// ⚠️ **BẮT BUỘC có route TƯỜNG MINH:** route mặc định là `{controller}/{action}/{id?}` ⇒ đoạn thứ ba gán cho **`id`**,
    /// nên tham số tên khác **KHÔNG** được bind từ URL ⇒ **`404` dù dữ liệu có** (đã ĐO được ở vòng 108).
    /// 📌 Đây là lỗi **chỉ lộ ra khi chạy**, và **ca DƯƠNG** mới phát hiện được — ca âm (`404`) đã "đạt" vì **lý do sai**.
    [HttpGet("SanPham/ChiTiet/{slugOrSku}")]
    public async Task<IActionResult> ChiTiet(string slugOrSku)
    {
        if (string.IsNullOrWhiteSpace(slugOrSku)) { return NotFound(); }

        // ① Thử SLUG trước (URL chuẩn), ② rồi mới thử SKU (URL cũ). Tra **sản phẩm TRƯỚC** rồi mới tra giá:
        // nếu tra giá trước thì một SKU "trông hợp lệ" vẫn cho `200` dù **không** thuộc sản phẩm nào.
        var sanPham = await _sanPham.GetBySlugAsync(slugOrSku);
        var laSlug = sanPham is not null;

        sanPham ??= await _sanPham.GetByVariantSkuAsync(slugOrSku);

        // 🔑 `canonical`: ưu tiên SLUG khi sản phẩm CÓ slug (URL chuẩn đã được đăng ký).
        // 🚨 **KHI SẢN PHẨM CHƯA CÓ SLUG (cột `Slug` là NULL — trạng thái hoàn toàn bình thường vì migration
        // không backfill):** dùng **slug TẠO TẠI CHỖ** từ `Name` (`SinhSlug.Tu` — cùng hàm với đường ghi).
        // 📌 VÌ SAO KHÔNG dùng thẳng SKU làm canonical: canonical là **lời khai URL CHUẨN**; trỏ vào SKU nghĩa là
        // *"URL chuẩn là dạng SKU"* ⇒ máy tìm kiếm **không bao giờ** chuyển sang URL slug, trong khi slug mới là
        // URL mà storefront/`sitemap` sẽ dùng ⇒ **hai dạng URL tranh nhau** đúng thứ `canonical` sinh ra để tránh.
        // ⚠️ Slug tạo tại chỗ **có thể** trùng với sản phẩm khác (chưa được kiểm ở tầng ghi) — đây là **nợ đã ghi**
        // (`CAT-003` §17.5): đường đúng là **đặt slug qua API** trước khi publish.
        var slugChuan = sanPham?.Slug;

        if (sanPham is not null && string.IsNullOrWhiteSpace(slugChuan))
        {
            var slugTam = eCommerce.Catalog.SinhSlug.Tu(sanPham.Name);
            slugChuan = slugTam.Length > 0 ? slugTam : null;
        }

        var bangGiaList = await _bangGia.GetListAsync();

        var dong = new List<(string MaBangGia, string TenBangGia, decimal DonGia)>();

        foreach (var bg in bangGiaList.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            var items = await _bangGia.GetItemsAsync(bg.Id);

            // 📌 Khi đã nhận diện được SẢN PHẨM (qua slug) thì gom giá của **mọi SKU thuộc sản phẩm đó** —
            // đó là ý nghĩa của "trang chi tiết sản phẩm" (một sản phẩm, nhiều biến thể).
            // Khi **không** nhận diện được (chỉ có SKU trần) thì giữ đúng hành vi cũ: lọc theo chính SKU đó.
            var cacSku = sanPham is null
                ? [slugOrSku]
                : sanPham.Variants.Select(v => v.Sku).ToArray();

            dong.AddRange(items
                .Where(x => cacSku.Contains(x.Sku, StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => x.Amount)
                .Select(x => (bg.Code, bg.Name, x.Amount)));
        }

        if (dong.Count == 0) { return NotFound(); }

        var giaThapNhat = dong.Min(x => x.DonGia);
        var tenHienThi = sanPham?.Name ?? slugOrSku;
        var tieuDe = $"{tenHienThi} — {giaThapNhat:N0} VND";
        var moTa = sanPham is null
            ? $"Giá niêm yết cho {tenHienThi}: từ {giaThapNhat:N0} VND ({dong.Count} danh sách giá)."
            : $"Giá niêm yết cho {tenHienThi}: từ {giaThapNhat:N0} VND ({dong.Count} dòng giá, {sanPham.Variants.Count} biến thể).";

        // 🔑 `canonical`: ưu tiên SLUG (URL chuẩn) khi nhận diện được sản phẩm; ngược lại giữ chính tham số vào
        // (để trang cũ vẫn có `canonical` hợp lệ thay vì rỗng).
        var duongDanChuan = "/SanPham/ChiTiet/" + (slugChuan ?? slugOrSku);

        var than = new System.Text.StringBuilder();
        than.Append($"<h1 id=\"ten-san-pham\">{System.Net.WebUtility.HtmlEncode(tenHienThi)}</h1>");
        than.Append($"<p id=\"gia-thap-nhat\">Giá từ <strong>{giaThapNhat:N0}</strong> VND</p>");

        if (sanPham is not null)
        {
            than.Append($"<p id=\"slug-san-pham\">Slug: {System.Net.WebUtility.HtmlEncode(sanPham.Slug ?? "(chưa có)")}</p>");
        }

        than.Append("<table id=\"bang-gia-chi-tiet\"><tr><th>Danh sách giá</th><th>Tên</th><th>Đơn giá</th></tr>");

        foreach (var d in dong)
        {
            than.Append($"<tr class=\"dong-gia\"><td>{System.Net.WebUtility.HtmlEncode(d.MaBangGia)}</td><td>{System.Net.WebUtility.HtmlEncode(d.TenBangGia)}</td><td>{d.DonGia:N0}</td></tr>");
        }

        than.Append("</table>");
        than.Append("<p><a id=\"quay-lai\" href=\"/SanPham\">← Danh sách hàng đang bán</a></p>");

        var html = "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>"
                 + System.Net.WebUtility.HtmlEncode(tieuDe)
                 + "</title><meta name=\"description\" content=\""
                 + System.Net.WebUtility.HtmlEncode(moTa)
                 + "\"><link rel=\"canonical\" href=\""
                 + System.Net.WebUtility.HtmlEncode(duongDanChuan)
                 + "\"></head><body>"
                 + than
                 + "</body></html>";

        // 📌 `laSlug` hiện chỉ dùng để ghi log chẩn đoán: phân biệt "khách vào bằng URL chuẩn" với
        // "khách vào bằng URL cũ" là thông tin cần để biết **khi nào** bỏ được đường dự phòng SKU.
        _logger.LogInformation("CHI TIẾT SẢN PHẨM: vào bằng {Loai} · sản phẩm {Code} · {So} dòng giá. [SP-CHITIET]",
            laSlug ? "SLUG (URL chuẩn)" : "SKU (URL cũ)",
            sanPham?.Code ?? "(không nhận diện được)",
            dong.Count);

        return Content(html, "text/html; charset=utf-8");
    }
}

/// <summary>Một dòng hiển thị: **danh sách giá** nào đang niêm yết **SKU** này với **đơn giá** bao nhiêu.</summary>
public class SanPhamDongDto
{
    public string MaBangGia { get; set; } = default!;

    public string TenBangGia { get; set; } = default!;

    public string Sku { get; set; } = default!;

    public decimal DonGia { get; set; }
}
