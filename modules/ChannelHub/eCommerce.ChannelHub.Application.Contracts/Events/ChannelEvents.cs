using eCommerce.ChannelHub.Channels;

namespace eCommerce.ChannelHub.Events;

/// <summary>
/// Sự kiện: đã kéo được đơn từ sàn.
///
/// 🎯 Đây là điểm nối giữa ChannelHub và nghiệp vụ đơn hàng của app, và cũng là lý do
/// ChannelHub KHÔNG cần tham chiếu domain của app:
///   ChannelHub chỉ phát sự kiện → app subscribe và tạo Order của mình.
/// Nhờ vậy ChannelHub tách thành microservice được mà không phải sửa app.
///
/// ⚠️ Sự kiện này đi qua distributed event bus ⇒ handler phải IDEMPOTENT
/// (sàn gửi trùng là bình thường; xem <see cref="ChannelOrderPulledEto.ExternalOrderId"/>).
/// </summary>
[Serializable]
public class ChannelOrderPulledEto
{
    public Guid? TenantId { get; set; }
    public Guid ChannelShopId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public string ExternalShopId { get; set; } = default!;
    public string ExternalOrderId { get; set; } = default!;
    public DateTime? PlacedAtUtc { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal ShippingFee { get; set; }
    public decimal ChannelDiscount { get; set; }
    public string? BuyerNote { get; set; }
    public List<ChannelOrderPulledLineEto> Lines { get; set; } = new();
}

[Serializable]
public class ChannelOrderPulledLineEto
{
    public string? ExternalLineId { get; set; }
    public string Sku { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}

/// <summary>Sự kiện: token của shop đã được refresh (để audit + cảnh báo vận hành).</summary>
[Serializable]
public class ChannelShopTokenRefreshedEto
{
    public Guid? TenantId { get; set; }
    public Guid ChannelShopId { get; set; }
    public string ChannelCode { get; set; } = default!;
    public string ExternalShopId { get; set; } = default!;
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
