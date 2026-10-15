using eCommerce.Catalog.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Catalog;

/// <summary>
/// Hợp đồng app service **cây danh mục** (`CAT-002` §6.1 + §9.2).
///
/// 🔐 `[Authorize]` ở **cấp CLASS** với quyền `Categories` (`XC-004` D3/BR-001) — đặt ở từng method thì
/// **quên một method là hở endpoint**.
///
/// 🚨 **BẤT BIẾN KHÔNG ĐƯỢC DB BẢO VỆ** (`CAT-002` §8.1 #1 — *"cây không có chu trình"*):
/// PostgreSQL **không** có ràng buộc "cây không chu trình" ⇒ chu trình chỉ bị chặn bởi
/// `CategoryTreeService` (khoá toàn tenant + kiểm tra `Path`). Vì vậy **`MoveAsync` là method duy nhất**
/// được đổi `ParentId`, và nó **phải** đi qua dịch vụ miền đó.
/// </summary>
public interface ICategoryAppService : IApplicationService
{
    /// <summary>Trả **cả cây** trong **một** truy vấn (lợi ích của `Path` vật chất hoá — `CAT-002` D1).</summary>
    Task<List<CategoryDto>> GetTreeAsync();

    Task<CategoryDto> GetAsync(Guid id);

    /// <summary>Danh sách **phẳng** (phân trang) — để tra cứu/quản trị, không dựng cây.</summary>
    Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoriesInput input);

    Task<CategoryDto> CreateAsync(CreateCategoryDto input);

    /// <summary>Đổi tên/slug/thứ tự/ngưỡng tổ hợp. ⚠️ **Không** đổi `Code` hay `ParentId`.</summary>
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input);

    /// <summary>
    /// 🚨 **Di chuyển nhánh** — thao tác nguy hiểm nhất của `CAT-002`: khoá toàn tenant + chống chu trình
    /// (`eCommerce:Catalog:0020`) + kiểm độ sâu (`0024`) + dựng lại `Path` cho **cả cây con**.
    /// </summary>
    Task MoveAsync(Guid id, MoveCategoryDto input);

    /// <summary>Ngừng dùng nút (thay cho xoá — `CAT-002` D9).</summary>
    Task DeactivateAsync(Guid id);

    Task ActivateAsync(Guid id);

    /// <summary>🚨 **Xoá MỀM**; **NÉM** `eCommerce:Catalog:0021` khi nút còn **nút con** hoặc còn **sản phẩm** (`CAT-002` D9).</summary>
    Task DeleteAsync(Guid id);

    // ── Liên kết sản phẩm ↔ danh mục (`CAT-002` BR-005/006/007) ────────────────────────────────

    /// <summary>
    /// Gán sản phẩm vào danh mục. **NÉM** `0021` khi danh mục đã **ngừng dùng** (bất biến **chỉ tầng domain**
    /// bảo vệ được — xem ghi chú ở `ProductCategoryService`). Gán lại là **idempotent**.
    /// </summary>
    Task AssignProductAsync(Guid categoryId, AssignProductDto input);

    /// <summary>Bỏ gán (idempotent — không ném lỗi khi chưa có liên kết).</summary>
    Task UnassignProductAsync(Guid categoryId, Guid productId);

    /// <summary>Đặt danh mục **chính** cho sản phẩm; trả về `CategoryId` **cũ** (nếu có) để client biết đã đổi cái nào.</summary>
    Task<Guid?> SetPrimaryAsync(Guid productId, SetPrimaryCategoryDto input);

    /// <summary>Danh sách sản phẩm trong một danh mục (storefront dựng trang danh mục).</summary>
    Task<List<Guid>> GetProductIdsAsync(Guid categoryId);
}
