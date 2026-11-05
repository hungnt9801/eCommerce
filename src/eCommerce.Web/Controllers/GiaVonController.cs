using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🆕 **`S12` — MÀN HÌNH GIÁ VỐN** (`/GiaVon`) — **chỉ đọc**.
///
/// 🚨 **BR-013: ĐÂY LÀ DỮ LIỆU NỘI BỘ.** Quyền thật do `ICostPriceAppService` cưỡng chế
/// (`[Authorize(PricingPermissions.CostPriceView)]` cấp class) — `[Authorize]` ở đây chỉ là **xác thực**.
/// 📌 Nếu chỉ có `[Authorize]` mà thiếu quyền ở app service thì **mọi nhân viên đăng nhập** đều xem được
/// biên lợi nhuận — xem chú thích ở `PricingPermissions.CostPriceView`.
///
/// 🔒 **Không** có nút thêm/sửa: nguồn giá vốn là **câu hỏi mở `Q2`** (nhập tay / import NCC / bình quân
/// gia quyền). Một form "sửa nhanh" ở đây sẽ trở thành **nguồn sự thật thứ hai** cho cùng con số, và lãi gộp
/// sẽ phụ thuộc *"lần cuối ai sửa"*.
/// </summary>
[Authorize]
public class GiaVonController : Controller
{
    private readonly ICostPriceAppService _giaVon;

    public GiaVonController(ICostPriceAppService giaVon)
    {
        _giaVon = giaVon;
    }

    /// <summary>
    /// `GET /GiaVon?sku=&amp;at=` — danh sách giá vốn.
    /// 📌 `at` (ngày) **tuỳ chọn trên UI** nhưng khi có thì lọc **đúng ngữ nghĩa hiệu lực** (`BR-003`); trang
    /// **không** tự chọn "bản mới nhất" thay người dùng.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? sku = null, DateOnly? at = null)
    {
        var danhSach = await _giaVon.GetListAsync(sku, at);

        ViewData["sku"] = sku;
        ViewData["at"] = at;

        return View(danhSach);
    }
}
