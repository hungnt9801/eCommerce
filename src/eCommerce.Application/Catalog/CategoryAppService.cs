using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Catalog;

/// <summary>
/// App service **cây danh mục** (`CAT-002` §6.1 — lát cắt 2).
///
/// 🔐 `[Authorize]` cấp CLASS = quyền `Categories`; method cần quyền riêng thì ghi thêm.
///
/// 🚨 **PHÂN CHIA TRÁCH NHIỆM (đọc trước khi sửa):**
///   • **`Create`/`Move`/`Delete`** ⇒ **BẮT BUỘC** đi qua `CategoryTreeService` (nơi có **khoá phân tán**
///     + chống chu trình + dựng lại `Path`). App service **KHÔNG** được tự set `ParentId`.
///   • **`Update`** (tên/slug/thứ tự) ⇒ không đụng cấu trúc cây nên đặt thẳng thuộc tính.
/// Đây là lý do `UpdateCategoryDto` **cố ý không có** `Code` và `ParentId`.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Categories.Default)]
public class CategoryAppService : ApplicationService, ICategoryAppService
{
    private readonly IRepository<Category, Guid> _categoryRepository;
    private readonly IRepository<ProductCategory, Guid> _lienKetRepository;
    private readonly CategoryTreeService _categoryTreeService;
    private readonly ProductCategoryService _productCategoryService;

    public CategoryAppService(
        IRepository<Category, Guid> categoryRepository,
        IRepository<ProductCategory, Guid> lienKetRepository,
        CategoryTreeService categoryTreeService,
        ProductCategoryService productCategoryService)
    {
        _categoryRepository = categoryRepository;
        _lienKetRepository = lienKetRepository;
        _categoryTreeService = categoryTreeService;
        _productCategoryService = productCategoryService;
    }

    /// <summary>
    /// Trả **cả cây** bằng **MỘT** truy vấn (không đệ quy, không N+1) — đây chính là lợi ích mà `Path`
    /// vật chất hoá mang lại (`CAT-002` D1). Thứ tự hiển thị do `CayDanhMucBuilder` quyết định.
    /// </summary>
    public async Task<List<CategoryDto>> GetTreeAsync()
    {
        var tatCa = await _categoryRepository.GetListAsync();

        return CayDanhMucBuilder.DungCay(tatCa);
    }

    public async Task<CategoryDto> GetAsync(Guid id)
    {
        var nut = await _categoryRepository.GetAsync(id);

        var dto = CayDanhMucBuilder.ToDto(nut);

        var con = await _categoryRepository.GetListAsync(x => x.ParentId == id);
        dto.Children = con
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CayDanhMucBuilder.ToDto)
            .ToList();

        return dto;
    }

    public async Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoriesInput input)
    {
        var query = await _categoryRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var tuKhoa = input.Keyword.Trim();
            query = query.Where(x => x.Code.Contains(tuKhoa) || x.Name.Contains(tuKhoa));
        }

        if (input.IsActive is not null)
        {
            query = query.Where(x => x.IsActive == input.IsActive);
        }

        if (input.ParentId is { } chaId)
        {
            // `Guid.Empty` = chỉ lấy NÚT GỐC (quy ước rõ ràng thay vì "null nghĩa là gì" mơ hồ).
            query = chaId == Guid.Empty
                ? query.Where(x => x.ParentId == null)
                : query.Where(x => x.ParentId == chaId);
        }

        var tong = await AsyncExecuter.CountAsync(query);

        var danhSach = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Level)
                 .ThenBy(x => x.SortOrder)
                 .ThenBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        return new PagedResultDto<CategoryDto>(
            tong, danhSach.Select(CayDanhMucBuilder.ToDto).ToList());
    }

    [Authorize(eCommerceConsts.Permissions.Categories.Create)]
    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input)
    {
        var nut = await _categoryTreeService.TaoNutAsync(
            GuidGenerator.Create(), input.ParentId, input.Code, input.Name);

        nut.Slug = ChuanHoaSlug(input.Slug);
        nut.SortOrder = input.SortOrder;
        nut.MaxVariantCombinations = input.MaxVariantCombinations;

        await _categoryRepository.UpdateAsync(nut, autoSave: true);

        Logger.LogInformation(
            "Đã tạo danh mục {Code} (cấp {Cap}).", nut.Code, nut.Level);

        return CayDanhMucBuilder.ToDto(nut);
    }

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input)
    {
        var nut = await _categoryRepository.GetAsync(id);

        // ⚠️ KHÔNG chạm `Code`/`ParentId` — DTO cố ý không có; đổi cấu trúc phải qua `MoveAsync`.
        nut.DoiTen(input.Name);
        nut.Slug = ChuanHoaSlug(input.Slug);
        nut.SortOrder = input.SortOrder;
        nut.MaxVariantCombinations = input.MaxVariantCombinations;

        await _categoryRepository.UpdateAsync(nut, autoSave: true);

        var dto = CayDanhMucBuilder.ToDto(nut);

        var con = await _categoryRepository.GetListAsync(x => x.ParentId == id);
        dto.Children = con
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CayDanhMucBuilder.ToDto)
            .ToList();

        return dto;
    }

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task MoveAsync(Guid id, MoveCategoryDto input)
        // 🚨 Toàn bộ phần nguy hiểm (khoá + chống chu trình + dựng lại Path cả cây con) nằm trong dịch vụ miền.
        => await _categoryTreeService.DiChuyenNhanhAsync(id, input.ParentId);

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task DeactivateAsync(Guid id)
    {
        var nut = await _categoryRepository.GetAsync(id);

        nut.NgungDung();

        await _categoryRepository.UpdateAsync(nut, autoSave: true);

        Logger.LogInformation("Đã NGỪNG DÙNG danh mục {Code}.", nut.Code);
    }

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task ActivateAsync(Guid id)
    {
        var nut = await _categoryRepository.GetAsync(id);

        nut.KichHoat();

        await _categoryRepository.UpdateAsync(nut, autoSave: true);
    }

    [Authorize(eCommerceConsts.Permissions.Categories.Delete)]
    public async Task DeleteAsync(Guid id)
        // 🚨 XOÁ MỀM + chặn khi còn nút con (`CAT-002` D9). Không xoá cứng: FK `Restrict` + guard.
        => await _categoryTreeService.XoaMemAsync(id);

    // ── Liên kết sản phẩm ↔ danh mục (`CAT-002` BR-005/006/007) ────────────────────────────────

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task AssignProductAsync(Guid categoryId, AssignProductDto input)
    {
        await _productCategoryService.GanAsync(input.ProductId, categoryId, input.IsPrimary);

        if (input.SortOrder != 0)
        {
            var lienKet = await _lienKetRepository.FirstOrDefaultAsync(
                x => x.ProductId == input.ProductId && x.CategoryId == categoryId);

            if (lienKet is not null)
            {
                lienKet.SortOrder = input.SortOrder;
                await _lienKetRepository.UpdateAsync(lienKet, autoSave: true);
            }
        }
    }

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task UnassignProductAsync(Guid categoryId, Guid productId)
        => await _productCategoryService.BoGanAsync(productId, categoryId);

    [Authorize(eCommerceConsts.Permissions.Categories.Edit)]
    public async Task<Guid?> SetPrimaryAsync(Guid productId, SetPrimaryCategoryDto input)
        => await _productCategoryService.DatDanhMucChinhAsync(productId, input.CategoryId);

    public async Task<List<Guid>> GetProductIdsAsync(Guid categoryId)
    {
        // ⚠️ Kiểm tra danh mục TỒN TẠI để phân biệt "danh mục rỗng" với "danh mục không có"
        // (trả `[]` cho cả hai sẽ khiến client tưởng danh mục tồn tại mà rỗng).
        _ = await _categoryRepository.GetAsync(categoryId);

        var lienKet = await _lienKetRepository.GetListAsync(x => x.CategoryId == categoryId);

        return lienKet
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .Select(x => x.ProductId)
            .ToList();
    }

    private static string? ChuanHoaSlug(string? slug)
        => string.IsNullOrWhiteSpace(slug) ? null : slug.Trim().ToLowerInvariant();
}
