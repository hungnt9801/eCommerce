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
/// Sự kiện: **slug (khoá định danh công khai trên URL) của sản phẩm đã đổi** — `CAT-003` **ADR-05**.
///
/// 🎯 **ĐÂY LÀ CÂU TRẢ LỜI CHO `Q3` CỦA `WEB-002`** (*"ETO `ProductSlugChangedEto` có mang `OldSlug` không? Ai phát?"*):
/// <list type="bullet">
///   <item><b>CÓ mang <see cref="OldSlug"/></b> — không có nó thì `WEB-002` **không** dựng được bảng chuyển hướng
///     **301** (`WEB-002` **BR-005**) ⇒ khách vào **link cũ** nhận **`404`** thay vì được chuyển hướng,
///     và **không có lỗi nào** để lần ra (SEO mất âm thầm).</item>
///   <item><b>Ai phát:</b> `ProductAppService` (tầng application) — **KHÔNG** phải entity `Product`, vì ETO nằm ở
///     `Application.Contracts` mà tầng `Domain` **không được** tham chiếu (bất biến #2/#3). Đúng khuôn đang có
///     của repo: `Product.Publish()` không phát `ProductPublishedEto`, `ProductAppService.PublishAsync` phát.</item>
/// </list>
///
/// 📌 **PHÂN LOẠI: sự kiện *MANG DỮ LIỆU* hay *THÔNG BÁO*?**
/// Slug **hiện tại** đã được ghi vào `CatProducts.Slug` **trước** khi phát (nguồn sự thật vẫn ở DB).
/// Nhưng **`OldSlug`** thì **chỉ tồn tại trong sự kiện này** — DB **không** giữ lịch sử slug (bảng `WebSlugRoutes`
/// là của `WEB-002`, `CAT-003` ADR-06 **cố ý** không sở hữu).
/// ⚠️ Vì vậy khi có message broker, sự kiện này **phải** đi qua **outbox** (`XC-002`): mất nó ⇒ mất **khả năng 301**
/// cho **đúng cặp slug đó**, và không có cách nào dựng lại (`inv-001` §22 — bài học *"ETO mang dữ liệu"*).
/// 📌 Hiện chưa có broker ⇒ in-process; đây là **nợ đã ghi**, không phải "đã an toàn".
/// </summary>
[Serializable]
public class ProductSlugChangedEto
{
    public Guid? TenantId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    /// <summary>Slug **CŨ** (`null` = trước đó sản phẩm **chưa có** URL công khai). 🔑 Đây là lý do ETO tồn tại.</summary>
    public string? OldSlug { get; set; }

    /// <summary>Slug **MỚI** (`null` = sản phẩm **không còn** URL công khai — ví dụ ngừng hiển thị).</summary>
    public string? NewSlug { get; set; }

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
