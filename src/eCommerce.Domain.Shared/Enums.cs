namespace eCommerce;

/// <summary>
/// Kênh bán. Giá trị lưu dạng chuỗi trong DB (xem <c>Channel.Code</c>), enum này chỉ để
/// phân loại hành vi đồng bộ.
/// </summary>
public enum ChannelType
{
    /// <summary>Website bán hàng của chính mình (nguồn sự thật về tồn kho).</summary>
    OwnStorefront = 0,

    /// <summary>Sàn TMĐT (Shopee, Lazada, TikTok Shop...).</summary>
    Marketplace = 1,

    /// <summary>Kênh bán offline / POS.</summary>
    Offline = 2,

    /// <summary>Kênh khác (Zalo, Facebook...).</summary>
    Other = 99
}

/// <summary>
/// Loại biến động kho. <c>StockLedgerEntry</c> là append-only: mọi thay đổi tồn kho
/// đều phải sinh một bản ghi ở đây, không được UPDATE tồn kho trực tiếp.
/// </summary>
public enum StockMovementType
{
    /// <summary>Nhập kho.</summary>
    Receipt = 0,

    /// <summary>Giữ chỗ cho đơn (chưa xuất kho).</summary>
    Reservation = 1,

    /// <summary>Nhả giữ chỗ (đơn hủy / hết TTL).</summary>
    ReservationRelease = 2,

    /// <summary>Xuất kho bán hàng.</summary>
    Sale = 3,

    /// <summary>Khách trả hàng, nhập lại kho.</summary>
    CustomerReturn = 4,

    /// <summary>Điều chỉnh kiểm kê.</summary>
    Adjustment = 5,

    /// <summary>Chuyển kho.</summary>
    Transfer = 6
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Completed = 4,
    Cancelled = 5,
    Returned = 6
}

public enum PaymentStatus
{
    Unpaid = 0,
    Pending = 1,
    Paid = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
    Failed = 5
}

/// <summary>Trạng thái đồng bộ của một listing trên sàn.</summary>
public enum ChannelListingStatus
{
    Draft = 0,
    PendingSync = 1,
    Synced = 2,
    Error = 3,
    Delisted = 4
}
