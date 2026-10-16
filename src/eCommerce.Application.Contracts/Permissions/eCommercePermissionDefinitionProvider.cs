using Volo.Abp.Authorization.Permissions;

namespace eCommerce.Permissions;

/// <summary>
/// Định nghĩa permission của ứng dụng.
///
/// 🚨 VÌ SAO BẮT BUỘC PHẢI CÓ FILE NÀY:
/// Các app service đã dùng <c>[Authorize("eCommerce.Channels.Manage")]</c>… Nếu permission
/// KHÔNG được định nghĩa thì ABP luôn coi là bị từ chối ⇒ API trả 403 dù user có role admin.
/// Đây là lỗi rất dễ mắc khi thêm auth vào một solution đã có sẵn [Authorize].
///
/// ABP tự phát hiện class kế thừa <see cref="PermissionDefinitionProvider"/> nên không cần
/// đăng ký thủ công.
///
/// 📌 Ghi chú: tham số displayName là <c>ILocalizableString?</c> nên đang để <c>null</c>
/// ⇒ UI sẽ hiển thị tên permission dạng thô. Khi làm UI admin (P1), tạo một localization
/// resource (ví dụ <c>eCommerceResource</c> + file en/vi.json) rồi truyền
/// <c>LocalizableString.Create&lt;eCommerceResource&gt;("Permission:Products")</c> vào đây.
/// </summary>
public class eCommercePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(eCommerceConsts.Permissions.GroupName, null);

        // ---- Sản phẩm / danh mục ----
        var products = group.AddPermission(eCommerceConsts.Permissions.Products.Default);
        // 🚨 `View` và `Publish` **BẮT BUỘC phải có mặt ở ĐÂY** (`CAT-001` D10 / F9):
        // hằng số khai trong `eCommerceConsts` mà **không** định nghĩa ở provider ⇒ ABP coi là
        // **luôn bị từ chối** ⇒ `403` cho MỌI người, **kể cả admin** — đúng lỗi Phase 0 (`XC-004` D2/E3).
        // 🛡️ Lưới an toàn: `BatBienPhanQuyenTests.TC02b` khoá bất biến "hằng số ↔ định nghĩa khớp 2 CHIỀU".
        products.AddChild(eCommerceConsts.Permissions.Products.View);
        products.AddChild(eCommerceConsts.Permissions.Products.Create);
        products.AddChild(eCommerceConsts.Permissions.Products.Edit);
        products.AddChild(eCommerceConsts.Permissions.Products.Delete);
        products.AddChild(eCommerceConsts.Permissions.Products.Publish);

        // 🚨 `CAT-003` — quyền **TẢI TỆP LÊN**, tách khỏi `Edit` (xem chú thích ở `eCommerceConsts`):
        // tải tệp là **ĐƯỜNG VÀO TỪ INTERNET** (phải kiểm **nội dung nhị phân** theo `ADR-13`, và là
        // bề mặt tấn công: XSS lưu trữ / DoS dung lượng) — **khác hẳn** sửa mô tả sản phẩm.
        // 🔒 BẮT BUỘC khai ở **CẢ HAI NƠI** (hằng số + provider): thiếu một nơi ⇒ `403` cho MỌI người kể cả admin.
        products.AddChild(eCommerceConsts.Permissions.Products.ManageMedia);

        // ---- Cây danh mục + thuộc tính (`CAT-002` §9.2) ----
        // 🚨 BẮT BUỘC thêm ở **CẢ HAI NƠI** (hằng số + provider) — thiếu một nơi ⇒ 403 cho MỌI người kể cả admin.
        var categories = group.AddPermission(eCommerceConsts.Permissions.Categories.Default);
        categories.AddChild(eCommerceConsts.Permissions.Categories.Create);
        categories.AddChild(eCommerceConsts.Permissions.Categories.Edit);
        categories.AddChild(eCommerceConsts.Permissions.Categories.Delete);

        var catalogAttributes = group.AddPermission(eCommerceConsts.Permissions.CatalogAttributes.Default);
        catalogAttributes.AddChild(eCommerceConsts.Permissions.CatalogAttributes.Manage);

        // ---- Pricing (`PRC-001` §9) ----
        // 🚨 BẮT BUỘC khai ở CẢ HAI NƠI (hằng số + provider) — thiếu một nơi ⇒ 403 cho MỌI người kể cả admin.
        // 📌 Hằng số do **MODULE** Pricing sở hữu (`PricingPermissions`) — `app → module` ĐƯỢC PHÉP.
        var pricing = group.AddPermission(eCommerce.Pricing.PricingPermissions.Default);
        pricing.AddChild(eCommerce.Pricing.PricingPermissions.Manage);

        // ---- Kho ----
        var inventory = group.AddPermission(eCommerceConsts.Permissions.Inventory.Default);
        inventory.AddChild(eCommerceConsts.Permissions.Inventory.Adjust);

        // ---- Kênh bán (ChannelHub) ----
        var channels = group.AddPermission(eCommerceConsts.Permissions.Channels.Default);
        channels.AddChild(eCommerceConsts.Permissions.Channels.Manage);
        channels.AddChild(eCommerceConsts.Permissions.Channels.Sync);

        // ---- Đơn hàng ----
        var orders = group.AddPermission(eCommerceConsts.Permissions.Orders.Default);
        orders.AddChild(eCommerceConsts.Permissions.Orders.Manage);

        // ---- Thanh toán ----
        var payments = group.AddPermission(eCommerceConsts.Permissions.Payments.Default);
        payments.AddChild(eCommerceConsts.Permissions.Payments.Refund);

        // ---- Thuế (XC-006) ----
        var tax = group.AddPermission(eCommerceConsts.Permissions.TaxClassification.Default);
        tax.AddChild(eCommerceConsts.Permissions.TaxClassification.View);
        tax.AddChild(eCommerceConsts.Permissions.TaxClassification.Manage);
        tax.AddChild(eCommerceConsts.Permissions.TaxClassification.Approve);
        tax.AddChild(eCommerceConsts.Permissions.TaxClassification.Import);
        tax.AddChild(eCommerceConsts.Permissions.TaxClassification.ExportMau01);

        var rateSchedule = group.AddPermission(eCommerceConsts.Permissions.TaxRateSchedule.Default);
        rateSchedule.AddChild(eCommerceConsts.Permissions.TaxRateSchedule.Manage);

        // ---- Hạn mức theo kênh + chính sách "cho phép chọn" (`INV-003` / `REC-001`) ----
        // 🚨 BẮT BUỘC khai ở CẢ HAI NƠI (hằng số `eCommerceConsts.Permissions.ChannelLimits` + ở đây):
        // thiếu một nơi ⇒ ABP coi là **luôn bị từ chối** ⇒ `403` cho MỌI người kể cả `admin` (lỗi `F-403`).
        // 🛡️ Lưới an toàn: `BatBienPhanQuyenTests` khoá bất biến "hằng số ↔ định nghĩa khớp 2 CHIỀU".
        var channelLimits = group.AddPermission(eCommerceConsts.Permissions.ChannelLimits.Default);
        channelLimits.AddChild(eCommerceConsts.Permissions.ChannelLimits.Manage);

        // ---- Hồ sơ khách hàng (`CUS-001`) ----
        // 🚨 BẮT BUỘC khai ở CẢ HAI NƠI (hằng số + ở đây): thiếu một nơi ⇒ `403` cho MỌI người kể cả `admin`.
        // 🔒 Tách `View` (`Customers.Default`) khỏi `Manage`: hồ sơ khách là **dữ liệu cá nhân** (Luật 91/2025/QH15)
        // ⇒ người **đọc** để chăm sóc khách **không** đương nhiên được **chặn/gỡ chặn/nâng cấp**.
        // 🔐 `XC-002` — vận hành hàng đợi thư chết. 📌 Tách khỏi `Inventory` để giữ **đặc quyền tối thiểu**
        // (DLQ chứa payload sự kiện ⇒ đọc nó là một quyền **riêng**).
        var operations = group.AddPermission(eCommerceConsts.Permissions.Operations.Default);
        operations.AddChild(eCommerceConsts.Permissions.Operations.DeadLetters);
        var customers = group.AddPermission(eCommerceConsts.Permissions.Customers.Default);
        customers.AddChild(eCommerceConsts.Permissions.Customers.Manage);
    }
}
