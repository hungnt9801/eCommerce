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
    public string? Message { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
}
