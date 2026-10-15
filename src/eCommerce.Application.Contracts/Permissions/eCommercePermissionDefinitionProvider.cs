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
    }
}
