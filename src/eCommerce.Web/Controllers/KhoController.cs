using eCommerce.Application.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// **MÀN HÌNH KHO** (`/Kho`, 28/10/2026) — xem tồn theo kho/SKU (read-only).
///
/// 🔐 `[Authorize]` cấp class = xác thực; quyền `Inventory.Default` do `ITonKhoAppService` cưỡng chế.
/// 🔒 **Không** có nút sửa tồn: điều chỉnh tồn là chứng từ có lý do + sổ cái (append-only) — thêm nút
/// "sửa số" ở đây sẽ mở đường ghi tắt, đúng thứ dự án chặn (`INV-001`).
/// </summary>
[Authorize]
public class KhoController : Controller
{
    private readonly ITonKhoAppService _ton;

    public KhoController(ITonKhoAppService ton)
    {
        _ton = ton;
    }

    /// <summary>`GET /Kho?khoId=&sku=` — danh sách tồn (lọc theo kho/mã SKU).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(Guid? khoId = null, string? sku = null)
    {
        var danhSach = await _ton.GetAsync(khoId, sku);

        ViewData["khoId"] = khoId;
        ViewData["sku"] = sku;

        return View(danhSach);
    }
}
