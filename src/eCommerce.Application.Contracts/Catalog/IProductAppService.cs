using eCommerce.Catalog.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Catalog;

/// <summary>
/// Hợp đồng app service của **Catalog** (`CAT-001` §6.1).
///
/// 🔐 **`[Authorize]` ở CẤP CLASS** là bắt buộc (`XC-004` **D3/BR-001**): đặt ở từng method ⇒
/// **quên một method là hở endpoint** (đã xảy ra thật ở Phase 0 với `GetShopsAsync`).
/// Class dùng permission **`Products`** (quyền "xem" cha); method cần quyền cụ thể thì ghi thêm.
/// 🛡️ `BatBienPhanQuyenTests.TC-01` khoá bất biến này trong CI.
/// </summary>
public interface IProductAppService : IApplicationService
{
    Task<PagedResultDto<ProductDto>> GetListAsync(GetProductsInput input);

    Task<ProductDto> GetAsync(Guid id);

    /// <summary>Tra cứu **nội bộ** theo mã sản phẩm (`CAT-001` §6.1).</summary>
    Task<ProductDto> GetByCodeAsync(string code);

    Task<ProductDto> CreateAsync(CreateProductDto input);

    /// <summary>⚠️ **Không** cho sửa `Code` (`CAT-001` D2) — DTO cập nhật không có trường đó.</summary>
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input);

    /// <summary>Thêm biến thể. Kiểm tra trùng SKU **toàn tenant** trong khoá phân tán (`CAT-001` D6).</summary>
    Task<ProductVariantDto> AddVariantAsync(Guid productId, CreateVariantDto input);

    /// <summary>Đổi giá niêm yết của biến thể (trả về `true` nếu giá **thực sự** đổi).</summary>
    Task<bool> UpdateVariantPriceAsync(Guid productId, Guid variantId, UpdateVariantPriceDto input);

    Task PublishAsync(Guid id);

    Task UnpublishAsync(Guid id);

    /// <summary>Lưu trữ — trạng thái **cuối**, không publish lại được (`CAT-001` D5).</summary>
    Task ArchiveAsync(Guid id);

    /// <summary>🚨 **Xoá MỀM** (`CAT-001` **D4**): unpublish + `IsDeleted`. **Cấm** xoá cứng vì phá lịch sử.</summary>
    Task DeleteAsync(Guid id);
}
