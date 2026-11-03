using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// **BẢNG ĐỐI CHIẾU `VariantId → ProductId`** của lần backfill `CatProductVariants` → sản phẩm con (WP-03 mục 5).
///
/// 🚨 VÌ SAO PHẢI CÓ MỘT BẢNG, KHÔNG CHỈ "CHẠY LẠI THÌ BIẾT":
/// <list type="number">
///   <item>**Idempotent**: lần chạy thứ hai phải **không** tạo sản phẩm con thứ hai cho cùng một biến thể.
///     Nếu chỉ suy ra bằng *"đã có sản phẩm `Code = Sku` chưa"* thì một sản phẩm **do người dùng tạo tay**
///     trùng mã SKU sẽ bị coi là "đã backfill" ⇒ biến thể đó **không bao giờ** được chuyển, và **không có lỗi nào**.</item>
///   <item>**Đối soát**: câu hỏi *"biến thể nào đã chuyển, biến thể nào chưa"* phải trả lời được **bằng SQL**
///     sau khi phiên làm việc kết thúc — không dựa vào log của một lần chạy.</item>
///   <item>**Lùi được**: `Down` của migration chỉ xoá bảng; muốn hoàn tác dữ liệu thì đọc chính bảng này để biết
///     sản phẩm con nào do backfill sinh ra (không đụng sản phẩm do người dùng tạo).</item>
/// </list>
///
/// 🔒 `(TenantId, VariantId)` **duy nhất** (`NULLS NOT DISTINCT` — xem migration): một biến thể chỉ được
/// chuyển **một lần**. Thiếu ràng buộc này thì hai lần chạy song song (hoặc chạy lại sau khi lỗi giữa đường)
/// tạo **hai sản phẩm con** cho cùng một SKU ⇒ trùng hàng trong danh mục, POS quét ra hai món.
/// </summary>
public class ProductVariantMap : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Biến thể cũ (`CatProductVariants.Id`) — khoá của lần chuyển.</summary>
    public Guid VariantId { get; protected set; }

    /// <summary>Sản phẩm **con** được tạo từ biến thể này.</summary>
    public Guid ProductId { get; protected set; }

    /// <summary>Đơn vị gốc của sản phẩm con — mang SKU/barcode/giá của biến thể cũ.</summary>
    public Guid UnitId { get; protected set; }

    /// <summary>SKU của biến thể (lưu lại để đọc/đối soát bằng mắt, không phải khoá).</summary>
    public string Sku { get; protected set; } = default!;

    protected ProductVariantMap() { }

    public ProductVariantMap(Guid id, Guid variantId, Guid productId, Guid unitId, string sku) : base(id)
    {
        VariantId = variantId;
        ProductId = productId;
        UnitId = unitId;
        Sku = sku;
    }
}
