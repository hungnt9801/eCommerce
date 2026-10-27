using eCommerce.Catalog;
using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Web.Controllers;

/// <summary>
/// **MÀN HÌNH THƯƠNG HIỆU** (`/Brands`, 27/10/2026) — `CAT-004` · `P0 #1` ①.
///
/// 🔐 `[Authorize]` ở cấp class = **xác thực**; quyền thật do `IBrandAppService` (`Catalog.Products`) cưỡng chế.
/// 📌 Slice này **CHỈ ĐỌC** (danh sách + lọc). Form tạo/sửa là slice UI kế tiếp — 🔒 không dựng form nửa vời
/// khi chưa có test hành vi (bài học “nút bấm được nhưng không lưu” ở `web-001`).
/// </summary>
[Authorize]
public class BrandsController : Controller
{
    private readonly IBrandAppService _brands;

    public BrandsController(IBrandAppService brands)
    {
        _brands = brands;
    }

    /// <summary>`GET /Brands?filter=&isActive=&page=` — danh sách thương hiệu có phân trang.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? filter = null, bool? isActive = null, int page = 1)
    {
        const int soDongMotTrang = 50;
        var trang = page < 1 ? 1 : page;

        var ketQua = await _brands.GetListAsync(new GetBrandsInput
        {
            Filter = filter,
            IsActive = isActive,
            SkipCount = (trang - 1) * soDongMotTrang,
            MaxResultCount = soDongMotTrang
        });

        ViewData["filter"] = filter;
        ViewData["isActive"] = isActive;
        ViewData["page"] = trang;
        ViewData["soDongMotTrang"] = soDongMotTrang;

        return View(ketQua);
    }
}
