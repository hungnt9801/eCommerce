using eCommerce.Catalog;
using eCommerce.Catalog.Dtos;
using eCommerce.HttpApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// Hàng hóa — bề mặt `/api/v1/products` cho Web Admin (`04 §5` màn #4–#6).
///
/// 🔐 Quyền: class dùng `Products.Default` (quyền **xem**); method ghi khai `Products.Manage*`/`Create`
/// riêng ở cấp method — theo đúng mẫu `[Authorize]` cấp class đã chốt ở `XC-004`.
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Authorize(eCommerceConsts.Permissions.Products.Default)]
public class ProductsController : AbpControllerBase
{
    private const int PageSizeToiDa = 200;

    private readonly IProductAppService _products;

    public ProductsController(IProductAppService products)
    {
        _products = products;
    }

    /// <summary>Danh sách có phân trang — dùng cho bảng 20 cột (server-side sort/filter).</summary>
    [HttpGet]
    public async Task<ApiPage<ProductListItemDto>> GetListAsync([FromQuery] ProductListQuery input)
    {
        var page = input.Page < 1 ? 1 : input.Page;
        var pageSize = input.PageSize is < 1 or > PageSizeToiDa ? 50 : input.PageSize;

        var result = await _products.GetListAsync(new GetProductsInput
        {
            Keyword = string.IsNullOrWhiteSpace(input.Q) ? null : input.Q.Trim(),
            Status = ParseStatus(input.Status),
            SkipCount = (page - 1) * pageSize,
            MaxResultCount = pageSize,
            Sorting = string.IsNullOrWhiteSpace(input.Sort) ? "CreationTime desc" : input.Sort
        });

        return new ApiPage<ProductListItemDto>(
            result.Items.Select(ProductApiMapper.ToListItem).ToList(),
            result.TotalCount,
            page,
            pageSize);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailDto>> GetAsync(Guid id)
    {
        var dto = await _products.GetAsync(id);
        return ProductApiMapper.ToDetail(dto);
    }

    /// <summary>
    /// Tạo sản phẩm theo mô hình Nhanh (WP-03): loại, VAT, cha–con và **các ĐVT tạo kèm**.
    /// 🔐 Quyền: `Products.Default` (cấp class) **VÀ** `Products.Create` (cấp method).
    /// 📌 Trả **201** + `Location` tới `/api/v1/products/{id}` để client mở form chi tiết ngay.
    /// </summary>
    [HttpPost]
    [Authorize(eCommerceConsts.Permissions.Products.Create)]
    public async Task<ActionResult<ProductDetailDto>> CreateAsync([FromBody] CreateProductDto input)
    {
        var dto = await _products.CreateAsync(input);
        return CreatedAtAction(nameof(GetAsync), new { id = dto.Id }, ProductApiMapper.ToDetail(dto));
    }

    /// <summary>
    /// Tra cứu 1 sản phẩm theo **SKU/biến thể** hoặc **mã sản phẩm** — POS dùng khi quét mã vạch.
    /// Trả 404 kèm mã lỗi ổn định thay vì ném exception thô.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<ProductLookupResultDto>> LookupAsync([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { code = "catalog.product.q_required", message = "Thiếu tham số q (mã vạch / SKU / mã sản phẩm)." });
        }

        var key = q.Trim();
        var dto = await _products.GetByVariantSkuAsync(key);

        if (dto is null)
        {
            try
            {
                dto = await _products.GetByCodeAsync(key);
            }
            catch (BusinessException)
            {
                // không tìm thấy theo mã — rơi xuống 404 bên dưới
            }
        }

        if (dto is null)
        {
            return NotFound(new { code = "catalog.product.not_found", message = $"Không tìm thấy sản phẩm '{key}'." });
        }

        return ProductApiMapper.ToLookup(dto);
    }

    private static ProductStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return Enum.TryParse<ProductStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;
    }
}
