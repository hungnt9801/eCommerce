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
        /// <summary>Khoá khi refresh token của một shop trên một kênh (token có rotate → phải tuần tự).</summary>
        public static string ChannelTokenRefresh(string channelCode, string shopId)
            => $"ecommerce:channel:{channelCode}:{shopId}:token-refresh";

        /// <summary>Khoá khi đẩy tồn kho/giá của một shop lên sàn.</summary>
        public static string ChannelStockPush(string channelCode, string shopId)
            => $"ecommerce:channel:{channelCode}:{shopId}:stock-push";

        /// <summary>Khoá khi trừ tồn kho của một SKU trong một kho.</summary>
        public static string InventorySku(string warehouseId, string sku)
            => $"ecommerce:inventory:{warehouseId}:{sku}";

        // 📌 Khoá liên quan tới kênh bán (refresh token, đẩy tồn kho) do CHÍNH module ChannelHub
        // sở hữu — xem modules/ChannelHub/ChannelHub.Domain/ChannelHubConsts.cs.
        // Không khai báo lại ở đây để tránh hai nguồn sự thật cho cùng một khoá lock.
    }
}
