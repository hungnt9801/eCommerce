namespace eCommerce.Pricing;

/// <summary>
/// 🚨 **TÊN PERMISSION CỦA MODULE PRICING — NGUỒN SỰ THẬT DUY NHẤT.**
///
/// 📌 **VÌ SAO Ở TRONG MODULE, KHÔNG Ở `eCommerceConsts`:** module **không** tham chiếu `eCommerce.Domain.Shared`
/// (quy ước dự án: *"module sở hữu hằng số của mình"* — tiền lệ `ChannelHubConsts`). Nếu khai **cả hai nơi**
/// thì đó đúng là lỗi *"hai nguồn sự thật cho cùng một mã"* đã gặp ở mien kênh (`XC-003` F2).
///
/// 🚨 **BẮT BUỘC khai ở CẢ HAI NƠI** khi thêm permission mới (D10/`XC-004` E3):
///   1. lớp này (hằng số), **và**
///   2. `eCommercePermissionDefinitionProvider` (`group.AddPermission` + `AddChild`).
/// Thiếu một trong hai ⇒ **`403` cho MỌI người kể cả admin** — đúng lỗi Phase 0.
///
/// 📌 **TÁCH `Manage` KHỎI `Default` CÓ CHỦ Ý:** xem giá là quyền của **mọi nhân viên bán hàng**;
/// **sửa/phát hành bảng giá** ảnh hưởng trực tiếp **doanh thu** ⇒ chỉ **quản trị giá**.
/// </summary>
public static class PricingPermissions
{
    public const string Nhom = "eCommerce.Pricing";

    public const string Default = Nhom;
    public const string Manage = Default + ".Manage";
}
