namespace eCommerce;

/// <summary>
/// Tên permission, tên policy và mã lỗi dùng chung.
/// Permission của ABP cần khai báo tập trung để UI/role đọc được.
/// </summary>
public static class eCommerceConsts
{
    public const string DbTablePrefix = "App";
    public const string? DbSchema = null;

    /// <summary>Tên connection string mặc định (xem appsettings.json).</summary>
    public const string DefaultConnectionStringName = "Default";

    public static class Permissions
    {
        public const string GroupName = "eCommerce";

        public static class Products
        {
            public const string Default = GroupName + ".Products";
            public const string Create = Default + ".Create";
            public const string Edit = Default + ".Edit";
            public const string Delete = Default + ".Delete";
        }

        public static class Inventory
        {
            public const string Default = GroupName + ".Inventory";
            public const string Adjust = Default + ".Adjust";
        }

        public static class Channels
        {
            public const string Default = GroupName + ".Channels";
            /// <summary>Kết nối / ngắt kết nối một shop trên sàn (OAuth).</summary>
            public const string Manage = Default + ".Manage";
            /// <summary>Kích hoạt đồng bộ thủ công.</summary>
            public const string Sync = Default + ".Sync";
        }

        public static class Orders
        {
            public const string Default = GroupName + ".Orders";
            public const string Manage = Default + ".Manage";
        }

        public static class Payments
        {
            public const string Default = GroupName + ".Payments";
            public const string Refund = Default + ".Refund";
        }

        /// <summary>
        /// Quyền của module <b>Tax</b> (XC-006). Ba việc tách biệt có chủ ý:
        /// kế toán nhập liệu — kế toán trưởng duyệt — kế toán trưởng xuất Mẫu 01.
        /// </summary>
        public static class TaxClassification
        {
            public const string Default = GroupName + ".Tax.Classification";
            public const string View = Default + ".View";
            public const string Manage = Default + ".Manage";
            public const string Approve = Default + ".Approve";
            public const string Import = Default + ".Import";
            public const string ExportMau01 = Default + ".ExportMau01";
        }

        /// <summary>Đổi khung hiệu lực mức thuế suất — theo Q6 đã chốt, thuộc KẾ TOÁN TRƯỞNG, có duyệt + audit.</summary>
        public static class TaxRateSchedule
        {
            public const string Default = GroupName + ".Tax.RateSchedule";
            public const string Manage = Default + ".Manage";
        }
    }

    /// <summary>Mã lỗi nghiệp vụ (dùng với BusinessException).</summary>
    public static class ErrorCodes
    {
        public const string InsufficientStock = "eCommerce:Inventory:0001";
        public const string ChannelShopNotFound = "eCommerce:Channel:0001";
        public const string ChannelTokenExpired = "eCommerce:Channel:0002";
        public const string ChannelRateLimited = "eCommerce:Channel:0003";
        public const string PaymentSignatureInvalid = "eCommerce:Payment:0001";
    }

    /// <summary>Tên khoá distributed lock — mọi nơi phải dùng chung hàm này để không lệch khoá.</summary>
    public static class LockKeys
    {
        // 🚨 F1 — XC-003 §15.2: khoá của miền KÊNH đã được XOÁ khỏi đây (13/10/2026).
        // Trước đó chúng tồn tại ở CẢ HAI nơi với CHUỖI KHÁC NHAU:
        //   • ở đây: `ecommerce:channel:{channelCode}:{shopId}:token-refresh`
        //   • ở module: `channel:{channelCode}:{shopId}:token-refresh`  (thiếu tiền tố `ecommerce:`)
        // ⇒ Nếu hai đoạn code dùng hai bản khác nhau thì khoá KHÔNG bảo vệ gì, và KHÔNG có lỗi biên dịch nào.
        // Khoá của miền kênh do CHÍNH module ChannelHub sở hữu — xem ChannelHubConsts.LockKeys.

        /// <summary>Khoá khi trừ tồn kho của một SKU trong một kho.</summary>
        /// <remarks>
        /// 📌 F4 — XC-003 §15.2: khoá này còn nằm ở app vì entity Inventory hiện ở <c>src/</c>.
        /// Khi tách module Inventory (GĐ1) thì chuyển khoá này về module đó (quy ước D3 của XC-003).
        /// </remarks>
        public static string InventorySku(string warehouseId, string sku)
            => $"ecommerce:inventory:{warehouseId}:{sku}";

        // 📌 Khoá liên quan tới kênh bán (refresh token, đẩy tồn kho) do CHÍNH module ChannelHub
        // sở hữu — xem modules/ChannelHub/ChannelHub.Domain/ChannelHubConsts.cs.
        // Không khai báo lại ở đây để tránh hai nguồn sự thật cho cùng một khoá lock.
    }
}
