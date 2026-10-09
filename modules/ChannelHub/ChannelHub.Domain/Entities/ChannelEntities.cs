using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.ChannelHub.Channels;

/// <summary>
/// Một kênh bán (Shopee, Lazada, TikTok Shop, website riêng...). Dữ liệu cấu hình toàn hệ thống.
/// </summary>
public class Channel : FullAuditedAggregateRoot<Guid>
{
    /// <summary>Mã kênh, dùng trong mọi khoá lock/đối soát: "shopee", "lazada", "tiktok", "web".</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>0=OwnStorefront, 1=Marketplace, 2=Offline, 99=Other (khớp <c>eCommerce.ChannelType</c>).</summary>
    public int Type { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Kênh này có chiếm tồn kho không (sàn có; kênh chỉ hiển thị thì không).</summary>
    public bool HoldsInventory { get; set; } = true;

    protected Channel() { }

    public Channel(Guid id, string code, string name, int type) : base(id)
    {
        Code = code;
        Name = name;
        Type = type;
    }
}

/// <summary>
/// Một shop cụ thể của người bán trên một kênh (một kênh có thể có nhiều shop).
/// Là thực thể trung tâm của ChannelHub: token, con trỏ đồng bộ và trạng thái nằm ở đây.
/// </summary>
public class ChannelShop : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Trùng với <see cref="Channel.Code"/>.</summary>
    public string ChannelCode { get; protected set; } = default!;

    /// <summary>ID shop do sàn cấp (ví dụ Shopee <c>shop_id</c>) — khoá đối chiếu quan trọng nhất.</summary>
    public string ExternalShopId { get; set; } = default!;

    public string ShopName { get; set; } = default!;

    public string? Region { get; set; }

    /// <summary>
    /// Access token ĐÃ MÃ HOÁ (<c>IStringEncryptionService</c> của ABP, gọi ở tầng application).
    /// ⚠️ Không bao giờ lưu plaintext; không log giá trị này.
    /// </summary>
    public string? AccessTokenEncrypted { get; set; }

    /// <summary>Refresh token đã mã hoá. ⚠️ Shopee refresh token CÓ ROTATE → bắt buộc lock khi refresh.</summary>
    public string? RefreshTokenEncrypted { get; set; }

    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    public DateTime? RefreshTokenExpiresAtUtc { get; set; }

    /// <summary>Hạn uỷ quyền của người bán (Shopee cho chọn 7/30/90/180/365 ngày).</summary>
    public DateTime? AuthorizationExpiresAtUtc { get; set; }

    /// <summary>Con trỏ đồng bộ: mốc <c>update_time</c> đã đồng bộ gần nhất.</summary>
    public DateTime? LastSyncedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Trạng thái push/webhook do sàn báo: Normal / Warning / Suspended.</summary>
    public string? PushStatus { get; set; }

    public string? LastError { get; set; }

    /// <summary>Nhịp đồng bộ riêng cho shop (giây). null = dùng mặc định của hệ thống.</summary>
    public int? SyncIntervalSeconds { get; set; }

    protected ChannelShop() { }

    public ChannelShop(Guid id, string channelCode, string externalShopId, string shopName) : base(id)
    {
        ChannelCode = channelCode;
        ExternalShopId = externalShopId;
        ShopName = shopName;
    }

    /// <summary>
    /// Ghi token mới. CHỈ được gọi khi đang giữ distributed lock
    /// <c>eCommerceConsts.LockKeys.ChannelTokenRefresh(ChannelCode, ExternalShopId)</c>:
    /// refresh token có rotate nên hai worker refresh song song sẽ làm mất token tốt.
    /// </summary>
    public void SetTokens(
        string accessTokenEncrypted,
        string? refreshTokenEncrypted,
        DateTime? accessExpiresAtUtc,
        DateTime? refreshExpiresAtUtc)
    {
        AccessTokenEncrypted = accessTokenEncrypted;
        if (refreshTokenEncrypted is not null)
        {
            RefreshTokenEncrypted = refreshTokenEncrypted;
        }

        AccessTokenExpiresAtUtc = accessExpiresAtUtc;
        RefreshTokenExpiresAtUtc = refreshExpiresAtUtc;
        LastError = null;
    }

    public bool NeedsTokenRefresh(DateTime nowUtc, TimeSpan safetyMargin)
        => AccessTokenExpiresAtUtc is null || AccessTokenExpiresAtUtc.Value <= nowUtc.Add(safetyMargin);
}

/// <summary>Ánh xạ một biến thể nội bộ lên một item trên kênh — bảng mapping trung tâm khi bán đa kênh.</summary>
public class ChannelListing : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ChannelShopId { get; protected set; }

    public string ChannelCode { get; protected set; } = default!;

    public Guid ProductVariantId { get; set; }

    /// <summary>SKU nội bộ tại thời điểm map (giữ lại để đối soát khi SKU đổi).</summary>
    public string InternalSku { get; set; } = default!;

    /// <summary>ID item do sàn cấp (Shopee/Lazada <c>item_id</c>).</summary>
    public string? ExternalItemId { get; set; }

    public string? ExternalSku { get; set; }

    /// <summary>Giá riêng cho kênh (null = dùng giá niêm yết của biến thể).</summary>
    public decimal? ChannelPrice { get; set; }

    /// <summary>
    /// Tồn kho dành riêng cho kênh (channel allotment) — giải bài toán bán vượt tồn kho vì
    /// sàn cập nhật tồn kho có độ trễ. null = dùng chung tồn kho khả dụng.
    /// </summary>
    public int? AllottedQuantity { get; set; }

    /// <summary>Khớp <c>eCommerce.ChannelListingStatus</c>.</summary>
    public int Status { get; set; }

    public string? LastSyncError { get; set; }

    public DateTime? LastSyncedAtUtc { get; set; }

    protected ChannelListing() { }

    public ChannelListing(Guid id, Guid channelShopId, string channelCode, Guid productVariantId, string internalSku)
        : base(id)
    {
        ChannelShopId = channelShopId;
        ChannelCode = channelCode;
        ProductVariantId = productVariantId;
        InternalSku = internalSku;
    }

    public void MarkSynced(string? externalItemId, string? externalSku)
    {
        ExternalItemId = externalItemId;
        ExternalSku = externalSku;
        Status = 2; // Synced
        LastSyncError = null;
        LastSyncedAtUtc = DateTime.UtcNow;
    }

    public void MarkError(string error)
    {
        Status = 3; // Error
        LastSyncError = error;
    }
}
