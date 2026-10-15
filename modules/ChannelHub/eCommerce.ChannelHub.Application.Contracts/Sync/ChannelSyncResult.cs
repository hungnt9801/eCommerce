namespace eCommerce.ChannelHub.Sync;

/// <summary>
/// Kết quả một lần đồng bộ — do ChannelHub trả về.
/// App sẽ map sang DTO của mình (<c>eCommerce.Channels.Dtos.ChannelSyncResultDto</c>);
/// ChannelHub KHÔNG được biết DTO của app.
/// </summary>
public class ChannelSyncResult
{
    public Guid ChannelShopId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public int OrdersPulled { get; set; }
    public int ListingsPushed { get; set; }
    public int Errors { get; set; }
    public bool RateLimited { get; set; }
    public bool TokenRefreshed { get; set; }

    /// <summary>
    /// 🚨 `true` khi lô đơn **BỊ CHẶN BÀN GIAO** vì chưa có nơi nhận đơn (lỗi #22).
    /// Khi đó con trỏ đồng bộ **KHÔNG tiến** ⇒ sàn sẽ gửi lại ⇒ **đơn không mất**.
    /// Cờ này tồn tại để admin/worker **nhìn thấy** trạng thái "kẹt" thay vì tưởng là thành công.
    /// </summary>
    public bool BiChanDoChuaCoNoiNhanDon { get; set; }

    /// <summary>Lý do chặn bàn giao (nếu có) — <c>null</c> khi lô đơn được xử lý bình thường.</summary>
    public string? LyDoChan { get; set; }

    public string? Message { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
}
