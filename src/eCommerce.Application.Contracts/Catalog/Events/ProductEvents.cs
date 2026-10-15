using eCommerce.Kernel.Events;

namespace eCommerce.Catalog.Events;

/// <summary>
/// Sự kiện: sản phẩm đã được **publish** (bắt đầu bán).
///
/// 📌 **ĐÂY LÀ SỰ KIỆN *THÔNG BÁO*** (gắn <see cref="INotificationEvent"/>) — trả lời câu hỏi
/// *"nếu sự kiện này biến mất, tôi có mất thông tin mà DB **không** có không?"* ⇒ **KHÔNG**:
/// trạng thái `Published` **đã được ghi** vào `CatProducts.Status` **trước** khi publish sự kiện.
///
/// 🎯 MỤC ĐÍCH: các bên cần biết để *hành động* — đẩy catalog lên sàn (`ChannelHub`),
/// xoá cache storefront (`WEB-002`), ghi chỉ mục tìm kiếm. Mất sự kiện ⇒ các bên đó **chậm** cho tới
/// lần đối chiếu/poll sau, **không** mất dữ liệu.
///
/// ⚠️ **VÌ SAO CHƯA CÓ OUTBOX (`F8` của `CAT-001`)**: chưa có message broker ⇒ sự kiện chỉ đi **in-process**.
/// Với loại **thông báo** thì chấp nhận được (mất ⇒ chậm, không mất dữ liệu). Với loại **mang dữ liệu**
/// thì **KHÔNG** chấp nhận được — đó chính là lỗi #22, và sự kiện đó **bắt buộc** fail-closed.
/// </summary>
[Serializable]
public class ProductPublishedEto : INotificationEvent
{
    public Guid? TenantId { get; set; }

    public Guid ProductId { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Slug { get; set; }

    /// <summary>Danh sách SKU đang hoạt động tại thời điểm publish (để bên nhận khỏi phải hỏi lại).</summary>
    public List<string> ActiveSkus { get; set; } = new();

    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>
/// Sự kiện: **giá niêm yết của một biến thể đã đổi**.
///
/// 📌 **SỰ KIỆN *THÔNG BÁO*** — giá mới **đã được ghi** vào `CatProductVariants.Price` trước khi publish.
/// 🚨 Chỉ phát khi giá **THỰC SỰ đổi** (`ProductVariant.DoiGia` trả `true`) ⇒ tránh sự kiện rác làm
/// nhiễu đối soát và làm bên nhận đẩy giá lên sàn không cần thiết (tiêu quota).
/// </summary>
[Serializable]
public class VariantPriceChangedEto : INotificationEvent
{
    public Guid? TenantId { get; set; }

    public Guid ProductId { get; set; }

    public Guid VariantId { get; set; }

    public string Sku { get; set; } = default!;

    /// <summary>Giá **CŨ** — cần cho đối soát ("giá nào đã áp dụng lúc nào").</summary>
    public decimal OldPrice { get; set; }

    /// <summary>Giá **MỚI**.</summary>
    public decimal NewPrice { get; set; }

    public string Currency { get; set; } = "VND";

    public DateTime OccurredAtUtc { get; set; }
}
