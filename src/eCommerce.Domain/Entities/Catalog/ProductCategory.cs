using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// Liên kết **sản phẩm ↔ danh mục** (`CatProductCategories`) — `CAT-002` §5.2.
///
/// 🚨 **BẤT BIẾN QUAN TRỌNG NHẤT: mỗi sản phẩm có TỐI ĐA MỘT danh mục chính** (`CAT-002` **BR-006**).
/// Bất biến này **KHÔNG** diễn đạt được bằng unique index thường (vì cần điều kiện "chỉ khi `IsPrimary`")
/// ⇒ phải dùng **unique index CÓ FILTER**: `UNIQUE (TenantId, ProductId) WHERE "IsPrimary"`. Đây là
/// **hàng rào cuối**: hai request đồng thời cùng đặt "danh mục chính" thì DB **chặn** request thua.
/// (Không có khoá phân tán cho thao tác này theo `CAT-002` §8.2 — xem ghi chú "điểm chưa hoàn thiện".)
///
/// 📌 Vì sao "danh mục chính" quan trọng: nó quyết định **breadcrumb** và **URL SEO** của sản phẩm
/// (`CAT-003`). Hai danh mục chính ⇒ hai breadcrumb ⇒ SEO tự cạnh tranh chính nó.
/// </summary>
public class ProductCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    public Guid CategoryId { get; protected set; }

    /// <summary>Danh mục chính (dùng cho breadcrumb/SEO). **Tối đa một** `true` cho mỗi sản phẩm.</summary>
    public bool IsPrimary { get; protected set; }

    public int SortOrder { get; set; }

    protected ProductCategory() { }

    private ProductCategory(Guid id, Guid productId, Guid categoryId, bool isPrimary) : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(productId));
        }

        if (categoryId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(categoryId));
        }

        ProductId = productId;
        CategoryId = categoryId;
        IsPrimary = isPrimary;
    }

    /// <summary>Tạo liên kết. ⚠️ Việc kiểm tra *"danh mục còn hoạt động"* (`CAT-002` §12 ⇒ `0021`) và
    /// *"chưa gán trùng"* thuộc **app service** (cần đọc DB); DB là hàng rào cuối cho cả hai.</summary>
    public static ProductCategory Tao(Guid id, Guid productId, Guid categoryId, bool isPrimary = false)
        => new(id, productId, categoryId, isPrimary);

    /// <summary>Đặt làm danh mục **chính**. ⚠️ Người gọi **phải** hạ `IsPrimary` của liên kết cũ trước
    /// (hoặc để DB chặn — unique index có filter là hàng rào cuối).</summary>
    public void DatLamChinh() => IsPrimary = true;

    /// <summary>Bỏ khỏi vai trò danh mục chính (vẫn giữ liên kết).</summary>
    public void BoChinh() => IsPrimary = false;
}
