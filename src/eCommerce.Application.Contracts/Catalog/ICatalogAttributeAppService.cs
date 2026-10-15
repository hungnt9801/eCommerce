using eCommerce.Catalog.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Catalog;

/// <summary>
/// Hợp đồng app service **thuộc tính + sinh tổ hợp biến thể** (`CAT-002` §6.1 + §9.2).
///
/// 🔐 Quyền được gắn ở **lớp HIỆN THỰC** (`CatalogAttributeAppService`): `[Authorize]` cấp CLASS =
/// quyền `CatalogAttributes`, method ghi thêm `CatalogAttributes.Manage`
/// (`XC-004` D3/BR-001 — đặt ở từng method thì **quên một method là hở endpoint**).
/// 📌 **Interface KHÔNG gắn `[Authorize]`** vì tầng `Application.Contracts` **không** tham chiếu
/// `Microsoft.AspNetCore.Authorization` (giữ layering); `AbpAppServiceConvention` đọc attribute từ **lớp hiện thực**.
///
/// 🚨 **`GenerateVariantsAsync` là thao tác nặng và nguy hiểm nhất của `CAT-002`**: nó **ghi nhiều biến thể
/// trong một transaction**, có **khoá theo sản phẩm**, **chặn ngưỡng TRƯỚC khi ghi** (D6) và **giữ nguyên**
/// biến thể đã có (BR-013). Xem `VariantGenerationService`.
/// </summary>
public interface ICatalogAttributeAppService : IApplicationService
{
    /// <summary>Danh sách định nghĩa thuộc tính **kèm giá trị** (để UI dựng bộ chọn).</summary>
    Task<List<CatalogAttributeDto>> GetListAsync();

    Task<CatalogAttributeDto> CreateAsync(CreateCatalogAttributeDto input);

    Task<CatalogAttributeValueDto> AddValueAsync(Guid attributeId, AddAttributeValueDto input);

    /// <summary>
    /// Chọn (**THÊM**) một giá trị thuộc tính cho sản phẩm. 🚨 **Nhiều giá trị cho một thuộc tính là HỢP LỆ** —
    /// đó chính là các **trục** của tích Descartes (chọn Đỏ **và** Xanh + M **và** L ⇒ sinh 4 tổ hợp).
    /// Idempotent: chọn lại giá trị đã có thì không tạo trùng.
    /// </summary>
    Task SelectValueAsync(Guid productId, SelectAttributeValueDto input);

    /// <summary>Bỏ **một giá trị cụ thể** khỏi lựa chọn của sản phẩm.</summary>
    Task UnselectValueAsync(Guid productId, Guid attributeValueId);

    /// <summary>Bỏ **toàn bộ** giá trị đã chọn của một thuộc tính trên sản phẩm.</summary>
    Task UnselectAsync(Guid productId, Guid attributeId);

    Task<List<ProductAttributeSelectionDto>> GetSelectionsAsync(Guid productId);

    /// <summary>
    /// 🚨 Sinh tổ hợp biến thể. **NÉM** `Catalog:0009` khi vượt ngưỡng (**trước khi ghi**),
    /// `eCommerce:Lock:0001` khi không giành được khoá, `Catalog:0007` khi chưa có giá mà sản phẩm chưa có
    /// biến thể nào.
    /// </summary>
    Task<GenerateVariantsResultDto> GenerateVariantsAsync(Guid productId, GenerateVariantsDto input);
}
