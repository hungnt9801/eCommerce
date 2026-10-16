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

    /// <summary>
    /// 🚨 **TRA SẢN PHẨM THEO SLUG** (`CAT-003`) — đường đọc cho **storefront** khi URL dùng slug.
    ///
    /// 📌 **VÌ SAO CẦN (không để `WEB-002` tự tra DB):** slug là **khoá URL công khai**; nếu tầng web tự truy vấn
    /// bảng `CatProducts` thì thành **hai nguồn sự thật** cho *"slug này là sản phẩm nào"* (`XC-003` F1) —
    /// và `CAT-003` ADR-06 đã chốt: *"slug hiện tại là gì"* thuộc **Catalog**.
    /// 🔒 Trả `null` khi **không** có slug đó (chỗ gọi tự quyết `404` hay thử đường dự phòng) —
    /// **không ném**, vì "slug không tồn tại" là ca **bình thường** của người dùng gõ URL.
    /// </summary>
    Task<ProductDto?> GetBySlugAsync(string slug);

    /// <summary>
    /// 🚨 **TRA SẢN PHẨM THEO **SKU BIẾN THỂ** (`CAT-003`) — đường **TƯƠNG THÍCH NGƯỢC** cho storefront.
    ///
    /// 📌 **VÌ SAO CẦN (không bỏ ngay được):** trước `CAT-003`, URL chi tiết sản phẩm là
    /// `/SanPham/ChiTiet/{sku}` và **đã ra ngoài** (link đã chia sẻ, đã được máy tìm kiếm index, đã in trên
    /// chứng từ). Bỏ nó đột ngột ⇒ **mọi link cũ 404** và **mất SEO đã tích luỹ** — đúng thứ `ADR-05` cảnh báo.
    /// ⇒ Giữ SKU làm **đường dự phòng**, còn **canonical** trỏ slug (để dần dồn tín hiệu SEO về URL mới).
    /// 🔒 Trả `null` khi SKU không thuộc sản phẩm nào (**không** ném) — chỗ gọi tự quyết `404`.
    /// </summary>
    Task<ProductDto?> GetByVariantSkuAsync(string sku);

    Task<ProductDto> CreateAsync(CreateProductDto input);

    /// <summary>⚠️ **Không** cho sửa `Code` (`CAT-001` D2) — DTO cập nhật không có trường đó.</summary>
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input);

    /// <summary>
    /// 🚨 **SINH / ĐỔI SLUG SEO** (`CAT-003` **ADR-04/ADR-05**).
    ///
    /// 🔑 **Sinh TẤT ĐỊNH từ `Name`** (bỏ dấu tiếng Việt) khi `Slug` để trống; trùng thì thêm hậu tố **`-2`, `-3`…**
    /// — kiểm trùng trong **cùng transaction** với lúc ghi.
    /// 📣 **Phát `ProductSlugChangedEto` kèm `OldSlug`** khi slug **thực sự** đổi ⇒ trả lời **Q3 của `WEB-002`**
    /// (có `OldSlug` mới dựng được **301**, `WEB-002` BR-005).
    /// 🔒 **KHÔNG tự sinh lại khi đổi `Name`** (ADR-05): làm vậy sẽ **phá URL đã index** ⇒ mất SEO **âm thầm**.
    /// ⚠️ **NÉM `Catalog:0026`** nếu slug trùng; **NÉM `Catalog:0027`** nếu tên không sinh được slug mà người
    /// dùng cũng không nhập tay.
    /// </summary>
    /// <param name="id">Sản phẩm.</param>
    /// <param name="slugTuNhap">Slug người dùng tự nhập; để trống ⇒ **sinh tự động** từ `Name`.</param>
    /// <returns>`true` nếu slug **thực sự đổi** (để UI biết có cần thông báo đổi URL công khai hay không).</returns>
    Task<bool> DatSlugAsync(Guid id, string? slugTuNhap);

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
