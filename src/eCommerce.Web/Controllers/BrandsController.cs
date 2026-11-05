using eCommerce.Catalog;
using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Web.Controllers;

/// <summary>
/// **MÀN HÌNH THƯƠNG HIỆU** (`/Brands`, 27/10/2026) — `CAT-004` · `P0 #1` ①.
///
/// 🔐 `[Authorize]` ở cấp class = **xác thực**; quyền thật do `IBrandAppService` (`Catalog.Products`) cưỡng chế.
/// 📌 Đọc + **tạo/sửa/ngừng**; mọi form POST có `@Html.AntiForgeryToken()` (bài học `web-001`: thiếu token ⇒ 400).
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

    /// <summary>`GET /Brands/Tao` — form tạo.</summary>
    [HttpGet]
    public IActionResult Tao() => View(new CreateBrandDto());

    /// <summary>`POST /Brands/Tao` — tạo; lỗi nghiệp vụ (trùng mã/slug) hiển thị lại form, KHÔNG 500.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tao(CreateBrandDto input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        try
        {
            await _brands.CreateAsync(input);
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(input);
        }
    }

    /// <summary>`GET /Brands/Sua/{id}` — form sửa (không đổi `Code`).</summary>
    [HttpGet]
    public async Task<IActionResult> Sua(Guid id)
    {
        var b = await _brands.GetAsync(id);

        ViewData["id"] = id;

        return View(new UpdateBrandDto
        {
            Name = b.Name,
            Slug = b.Slug,
            Description = b.Description,
            LogoUrl = b.LogoUrl,
            SortOrder = b.SortOrder
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sua(Guid id, UpdateBrandDto input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        try
        {
            await _brands.UpdateAsync(id, input);
            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(input);
        }
    }

    /// <summary>`POST /Brands/Ngung/{id}` — ngừng dùng (không xoá cứng).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ngung(Guid id)
    {
        await _brands.DeactivateAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KichHoat(Guid id)
    {
        await _brands.ActivateAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
