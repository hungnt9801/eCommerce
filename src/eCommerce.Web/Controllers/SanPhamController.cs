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

    public SanPhamController(IPriceListAppService bangGia)
    {
        _bangGia = bangGia;
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

    /// <summary>`GET /SanPham/ChiTiet/{sku}` — **CHI TIẾT SẢN PHẨM + SEO** (`CAT-003` · nợ của `WEB-002`).
    ///
    /// 📌 **Một nguồn sự thật:** dữ liệu lấy từ **cùng `IPriceListAppService`** như `/SanPham` — **KHÔNG** tự tính giá ở tầng UI
    /// (giá **phải** đến từ `Pricing`, `PRC-001` Q1).
    /// 🔎 **SEO:** `<title>` · `<meta name="description">` · `<link rel="canonical">`.
    ///    📌 Slug thay SKU là **bước sau của `CAT-003`** — hiện dùng SKU làm định danh URL (đã **duy nhất** theo nghiệp vụ).
    /// ⚠️ Trang **CHỈ ĐỌC** ⇒ **không** có form ⇒ **không** cần token chống giả mạo (bài học vòng 73 · 86).
    /// 🚨 **Không có giá niêm yết ⇒ `404`** — **KHÔNG** trả trang rỗng (tránh máy tìm kiếm **đánh chỉ mục trang rỗng**).
    /// 🔒 Mọi giá trị đưa vào HTML đều qua `HtmlEncode` (SKU do người dùng nhập ⇒ **không** nối chuỗi thô).</summary>
    /// ⚠️ **BẮT BUỘC có route TƯỜNG MINH:** route mặc định là `{controller}/{action}/{id?}` ⇒ đoạn thứ ba gán cho **`id`**,
    /// nên tham số tên `sku` **KHÔNG** được bind từ URL ⇒ `sku = null` ⇒ **`404` dù SKU có giá** (đã ĐO được ở vòng 108).
    /// 📌 Đây là lỗi **chỉ lộ ra khi chạy**, và **ca DƯƠNG** mới phát hiện được — ca âm (`404`) đã "đạt" vì **lý do sai**.
    [HttpGet("SanPham/ChiTiet/{sku}")]
    public async Task<IActionResult> ChiTiet(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) { return NotFound(); }

        var bangGiaList = await _bangGia.GetListAsync();

        var dong = new List<(string MaBangGia, string TenBangGia, decimal DonGia)>();

        foreach (var bg in bangGiaList.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            var items = await _bangGia.GetItemsAsync(bg.Id);

            dong.AddRange(items
                .Where(x => string.Equals(x.Sku, sku, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Amount)
                .Select(x => (bg.Code, bg.Name, x.Amount)));
        }

        if (dong.Count == 0) { return NotFound(); }

        var giaThapNhat = dong.Min(x => x.DonGia);
        var tieuDe = $"{sku} — {giaThapNhat:N0} VND";
        var moTa = $"Giá niêm yết cho {sku}: từ {giaThapNhat:N0} VND ({dong.Count} danh sách giá).";

        var than = new System.Text.StringBuilder();
        than.Append($"<h1 id=\"ten-san-pham\">{System.Net.WebUtility.HtmlEncode(sku)}</h1>");
        than.Append($"<p id=\"gia-thap-nhat\">Giá từ <strong>{giaThapNhat:N0}</strong> VND</p>");
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
                 + "\"><link rel=\"canonical\" href=\"/SanPham/ChiTiet/"
                 + System.Net.WebUtility.HtmlEncode(sku)
                 + "\"></head><body>"
                 + than
                 + "</body></html>";

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
