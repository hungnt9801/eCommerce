namespace eCommerce.ChannelHub.Connectors;

/// <summary>
/// Bối cảnh một shop khi gọi API sàn. Token ở đây là <b>plaintext</b> — chỉ tồn tại trong
/// bộ nhớ trong lúc gọi; nơi tạo context chịu trách nhiệm giải mã từ entity.
/// </summary>
public class ChannelShopContext
{
    public Guid ChannelShopId { get; set; }
    public Guid? TenantId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public string ExternalShopId { get; set; } = default!;
    public string? Region { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
}

public class ChannelOrderLineModel
{
    /// <summary>ID dòng đơn do sàn cấp (<c>trade_order_line_id</c>) — dùng để chống trùng.</summary>
    public string? ExternalLineId { get; set; }
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class ChannelOrderModel
{
    /// <summary>ID đơn do sàn cấp (<c>order_sn</c> / <c>trade_order_id</c>).</summary>
    public string ExternalOrderId { get; set; } = default!;
    public DateTime? PlacedAtUtc { get; set; }
    public string? BuyerNote { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal ShippingFee { get; set; }
    public decimal ChannelDiscount { get; set; }
    public List<ChannelOrderLineModel> Lines { get; set; } = new();
}

public class PullOrdersResult
{
    public List<ChannelOrderModel> Orders { get; set; } = new();

    /// <summary>Mốc <c>update_time</c> mới nhất đã đọc — ghi vào <c>ChannelShop.LastSyncedAtUtc</c>.</summary>
    public DateTime? NewSyncCursorUtc { get; set; }

    /// <summary>Sàn báo hết quota (HTTP 429) → worker phải backoff, không được retry dồn.</summary>
    public bool RateLimited { get; set; }

    public string? Message { get; set; }
}

public class ChannelListingPushModel
{
    public string InternalSku { get; set; } = default!;
    public string? ExternalItemId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int? WeightInGrams { get; set; }
}

public class PushListingResult
{
    public string InternalSku { get; set; } = default!;
    public bool Success { get; set; }
    public string? ExternalItemId { get; set; }
    public string? ExternalSku { get; set; }
    public bool RateLimited { get; set; }
    public string? Error { get; set; }
}

public class RefreshTokenResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Một connector cho một sàn. Mọi ràng buộc riêng của sàn (ký request, quota, webhook)
/// phải nằm SAU interface này — tầng nghiệp vụ không bao giờ biết chi tiết đó.
///
/// Ghi chú kỹ thuật đã khảo sát (xem docs/architecture.md):
///  • Shopee: sign = hex(HMAC-SHA256(partner_key, partner_id + api_path + timestamp [+ access_token + shop_id]));
///    access token 4h, refresh token 30 ngày VÀ CÓ ROTATE → bắt buộc distributed lock khi refresh.
///  • Lazada: sign = HEX HOA(HMAC-SHA256(app_secret, api_path + concat(sorted params) + body));
///    code chỉ dùng 1 lần, đổi token trong 30 phút.
///  • Cả hai: webhook là at-least-once → mọi xử lý phải IDEMPOTENT.
/// </summary>
public interface IChannelConnector
{
    /// <summary>Mã kênh, ví dụ "shopee". Phải khớp <c>Channel.Code</c>.</summary>
    string ChannelCode { get; }

    /// <summary>Kéo đơn mới/kế tiếp theo con trỏ đồng bộ. Phải chịu được gọi lặp (idempotent ở phía xử lý).</summary>
    Task<PullOrdersResult> PullOrdersAsync(ChannelShopContext shop, CancellationToken cancellationToken = default);

    /// <summary>Đẩy giá + tồn kho của một listing lên sàn.</summary>
    Task<PushListingResult> PushListingAsync(ChannelShopContext shop, ChannelListingPushModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh access token. BẮT BUỘC được gọi trong distributed lock per-shop
    /// (xem <c>eCommerceConsts.LockKeys.ChannelTokenRefresh</c>).
    /// </summary>
    Task<RefreshTokenResult> RefreshTokenAsync(ChannelShopContext shop, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra token còn hợp lệ (dùng cho health check hiển thị trên admin).</summary>
    Task<bool> PingAsync(ChannelShopContext shop, CancellationToken cancellationToken = default);
}

/// <summary>Chọn connector theo mã kênh. Đăng ký thêm sàn mới = thêm một implementation.</summary>
public interface IChannelConnectorFactory
{
    IChannelConnector Get(string channelCode);

    bool IsSupported(string channelCode);
}
