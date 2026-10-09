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
        products.AddChild(eCommerceConsts.Permissions.Products.Create);
        products.AddChild(eCommerceConsts.Permissions.Products.Edit);
        products.AddChild(eCommerceConsts.Permissions.Products.Delete);

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
    }
}
