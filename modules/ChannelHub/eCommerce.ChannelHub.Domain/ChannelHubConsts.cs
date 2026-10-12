using Volo.Abp;

namespace eCommerce.ChannelHub;

/// <summary>
/// Hằng số RIÊNG của ChannelHub.
///
/// 🎯 Vì sao không dùng chung <c>eCommerceConsts</c> của app:
/// module không được tham chiếu app (app → module là hợp lệ, ngược lại là vi phạm).
/// Khi tách ChannelHub thành microservice, file này đi theo module.
/// </summary>
public static class ChannelHubConsts
{
    public const string DbTablePrefix = "Chn";

    /// <summary>
    /// Khoá distributed lock của miền kênh. Mọi nơi phải dùng qua hàm ở đây để không lệch khoá.
    /// </summary>
    public static class LockKeys
    {
        /// <summary>Khoá khi refresh token của một shop (Shopee rotate refresh token → phải tuần tự).</summary>
        public static string ChannelTokenRefresh(string channelCode, string shopId)
            => $"channel:{channelCode}:{shopId}:token-refresh";

        /// <summary>Khoá khi đẩy tồn kho/giá của một shop lên sàn.</summary>
        public static string ChannelStockPush(string channelCode, string shopId)
            => $"channel:{channelCode}:{shopId}:stock-push";

        /// <summary>Khoá khi kéo đơn của một shop (tránh hai worker cùng kéo một con trỏ).</summary>
        public static string ChannelOrderPull(string channelCode, string shopId)
            => $"channel:{channelCode}:{shopId}:order-pull";
    }
}

/// <summary>Mã lỗi của miền kênh.</summary>
public static class ChannelHubErrorCodes
{
    public const string ChannelShopNotFound = "ChannelHub:0001";
    public const string ChannelTokenExpired = "ChannelHub:0002";
    public const string ChannelRateLimited = "ChannelHub:0003";
    public const string ChannelNotSupported = "ChannelHub:0004";
}
