using eCommerce.Pricing;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

/// <summary>
/// **MÀN HÌNH BẢNG GIÁ** (`/Pricing`, 28/10/2026) — xem bảng giá và dòng giá (read-only).
///
/// 🔐 `[Authorize]` cấp class; quyền `Pricing.Default` do `IPriceListAppService` cưỡng chế.
/// 🔒 **Không** sửa giá ở đây: bảng giá có vòng đời `Draft → Approved → Active` và audit; nút sửa nhanh sẽ
/// phá vòng đời đó (`PRC-001`). Tạo/sửa là slice quản trị riêng.
/// </summary>
[Authorize]
public class PricingController : Controller
{
    private readonly IPriceListAppService _bangGia;

    public PricingController(IPriceListAppService bangGia)
    {
        _bangGia = bangGia;
    }

    /// <summary>`GET /Pricing?id=` — danh sách bảng giá; nếu chọn `id` thì kèm dòng giá của bảng đó.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(Guid? id = null)
    {
        var danhSach = await _bangGia.GetListAsync();

        var dong = id is { } bangGiaId
            ? await _bangGia.GetItemsAsync(bangGiaId)
            : new List<PriceListItemDto>();

        ViewData["id"] = id;

        return View((danhSach, dong));
    }
}
