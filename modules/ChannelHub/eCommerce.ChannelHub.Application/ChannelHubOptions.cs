namespace eCommerce.ChannelHub;

/// <summary>Cấu hình ChannelHub — đọc từ section "ChannelHub" trong appsettings.json.</summary>
public class ChannelHubOptions
{
    /// <summary>Chu kỳ supervisor quét danh sách shop (giây). Mặc định 60s.</summary>
    public int SupervisorPeriodSeconds { get; set; } = 60;

    /// <summary>Nhịp đồng bộ mặc định cho một shop (giây) khi shop không tự đặt. Mặc định 300s (5 phút).</summary>
    public int DefaultShopSyncIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Refresh access token trước khi hết hạn bao lâu (phút).
    /// Shopee access token chỉ sống 4 giờ ⇒ mặc định 30 phút là an toàn.
    /// </summary>
    public int TokenRefreshSafetyMarginMinutes { get; set; } = 30;

    /// <summary>Số đơn tối đa kéo mỗi lần để không vượt quota.</summary>
    public int MaxOrdersPerPull { get; set; } = 50;

    /// <summary>Thời gian chờ tối đa để giành distributed lock (giây).</summary>
    public int LockTimeoutSeconds { get; set; } = 10;

    /// <summary>Shopee Open Platform — điền từ môi trường, KHÔNG commit giá trị thật.</summary>
    public ShopeeOptions Shopee { get; set; } = new();
}

public class ShopeeOptions
{
    /// <summary>partner_id do Shopee cấp.</summary>
    public string PartnerId { get; set; } = string.Empty;

    /// <summary>
    /// partner_key dùng để ký HMAC-SHA256.
    /// ⚠️ Shopee: partner_key hết hạn sau 180 NGÀY và KHÔNG tự gia hạn → phải có nhắc lịch gia hạn.
    /// </summary>
    public string PartnerKey { get; set; } = string.Empty;

    /// <summary>
    /// CHỈ host, KHÔNG kèm /api/v2 (vì <c>api_path</c> trong chuỗi ký đã gồm /api/v2 và
    /// được nối thẳng vào BaseUrl — nếu để cả hai sẽ thành /api/v2/api/v2).
    /// Production: https://partner.shopeemobile.com · Sandbox: https://partner.test-stable.shopeemobile.com
    /// </summary>
    public string BaseUrl { get; set; } = "https://partner.test-stable.shopeemobile.com";

    /// <summary>Bật/tắt gọi HTTP thật. Trong PoC để false → connector chỉ mô phỏng, không cần credential.</summary>
    public bool EnableLiveCalls { get; set; }
}
