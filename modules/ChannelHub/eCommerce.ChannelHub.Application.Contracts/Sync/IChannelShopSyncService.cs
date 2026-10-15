namespace eCommerce.ChannelHub.Sync;

/// <summary>
/// Cổng **đồng bộ một shop** — để tầng app gọi được mà **không phải biết lớp cài đặt**
/// (<c>ChannelShopSyncer</c> nằm ở assembly Application của module).
///
/// 🚨 VÌ SAO CẦN CỔNG NÀY (bịt **biến thể thứ hai** của lỗi #22):
/// `ChannelAppService.SyncNowAsync` (nút *"Đồng bộ ngay"* trên admin) trước đây **tự kéo đơn rồi tự
/// tiến con trỏ** — mà **không hề phát sự kiện** cho ai. Tức là **cùng một lỗi** với `ChannelShopSyncer`,
/// nhưng ở đường **do người dùng bấm**: đúng cái nút mà người vận hành sẽ bấm khi thấy đồng bộ có vẻ sai.
///
/// Hai đường đồng bộ song song là **hai cơ hội để lệch nhau**. Nay tầng app **bắt buộc** đi qua cổng này
/// ⇒ chỉ còn **MỘT** đường đồng bộ, và **một** chỗ quyết định fail-closed.
/// </summary>
public interface IChannelShopSyncService
{
    /// <summary>Đồng bộ một shop (kéo đơn → bàn giao, hoặc **chặn** nếu chưa có nơi nhận đơn).</summary>
    Task<ChannelSyncResult> SyncAsync(Guid channelShopId, CancellationToken cancellationToken = default);
}
