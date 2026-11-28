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

    /// <summary>
    /// 🆕 `S12` (`PRC-001` §6.1) — **XEM GIÁ VỐN** (`eCommerce.Pricing.CostPrice.View`).
    ///
    /// 🚨 **VÌ SAO KHÔNG DÙNG CHUNG `Default`:** xem giá vốn là xem **biên lợi nhuận của công ty**
    /// (`PRC-001` **BR-013**). Mọi nhân viên bán hàng đều cần `Default` (xem giá bán); **không** phải ai
    /// cũng được thấy giá nhập. Gộp hai quyền làm một là **cấp thừa quyền im lặng** — đúng loại lỗi
    /// `XC-004` chống, và nó chỉ lộ ra khi có người **sao lưu/CSV** dữ liệu giá vốn ra ngoài.
    ///
    /// 🔒 Tên theo **đặc tả** `PRC-001` §6.1 (`…CostPrice.View`) để client/BA đối chiếu được, không theo
    /// kiểu viết tắt của lớp này.
    /// </summary>
    public const string CostPriceView = Default + ".CostPrice.View";

    /// <summary>
    /// 🆕 **`W17-2`** — **TẠO/SỬA giá vốn** (`eCommerce.Pricing.CostPrice.Manage`) cho đường ghi thủ công
    /// (`ICostPriceAdminAppService.CreateAsync`) + chạy động cơ on-demand.
    ///
    /// 🚨 **Chỉ khai KHI có method dùng** (`PRC-001` §20.3 #4 — cấm "quyền rỗng"). Khai **hai nơi**:
    /// lớp này **và** `eCommercePermissionDefinitionProvider` (thiếu một nơi ⇒ `403` cho MỌI người).
    /// </summary>
    public const string CostPriceManage = Default + ".CostPrice.Manage";

    /// <summary>
    /// 🆕 **`W17-2`** — **DUYỆT giá vốn** (`eCommerce.Pricing.CostPrice.Approve`) cho `ApproveAsync`.
    /// 📌 Tách khỏi `.Manage` **có chủ ý**: người nhập giá vốn **không** đương nhiên được duyệt con số mình nhập
    /// (tách nhiệm vụ kế toán — `PRC-001` §3.2/Q4; vẫn giữ **2 bước**).
    /// </summary>
    public const string CostPriceApprove = Default + ".CostPrice.Approve";
}
