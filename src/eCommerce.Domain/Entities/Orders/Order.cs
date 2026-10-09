using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>
/// Đơn hàng — dùng chung cho cả đơn phát sinh trên website và đơn kéo về từ sàn.
/// Nguồn gốc đơn được phân biệt bằng <see cref="ChannelCode"/> + <see cref="ExternalOrderId"/>.
/// </summary>
public class Order : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Số đơn hiển thị cho khách (unique theo tenant).</summary>
    public string Number { get; protected set; } = default!;

    /// <summary>Mã kênh phát sinh đơn: "web", "shopee", "lazada"...</summary>
    public string ChannelCode { get; protected set; } = default!;

    /// <summary>Shop trên kênh (null nếu đơn từ website riêng).</summary>
    public Guid? ChannelShopId { get; set; }

    /// <summary>
    /// ID đơn do sàn cấp (<c>order_sn</c> / <c>trade_order_id</c>).
    /// ⚠️ Phải có unique index (TenantId, ChannelCode, ExternalOrderId) để idempotent khi webhook gửi trùng.
    /// </summary>
    public string? ExternalOrderId { get; set; }

    public Guid? CustomerId { get; set; }

    public OrderStatus Status { get; protected set; } = OrderStatus.Pending;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    /// <summary>Mã tiền tệ ISO-4217 (VND, USD...).</summary>
    public string Currency { get; set; } = "VND";

    // ⚠️ Tất cả trường tiền tệ là decimal — không dùng double (bài học từ GrandNode 2).
    public decimal Subtotal { get; private set; }

    public decimal DiscountTotal { get; private set; }

    public decimal TaxTotal { get; private set; }

    public decimal ShippingTotal { get; private set; }

    public decimal GrandTotal { get; private set; }

    /// <summary>Phí sàn (nếu là đơn sàn) — cần cho đối soát settlement.</summary>
    public decimal? ChannelFeeAmount { get; set; }

    public string? Note { get; set; }

    private readonly List<OrderLine> _lines = new();

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    protected Order() { }

    public Order(Guid id, string number, string channelCode) : base(id)
    {
        Number = number;
        ChannelCode = channelCode;
    }

    /// <summary>
    /// Thêm một dòng đơn. Idempotent theo (ExternalLineId / Sku): webhook gửi lại cùng đơn
    /// sẽ không tạo dòng trùng.
    /// </summary>
    public OrderLine AddLine(Guid id, string sku, int quantity, decimal unitPrice, string? externalLineId = null)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Số lượng phải > 0.");
        }

        var existing = externalLineId is null
            ? _lines.FirstOrDefault(x => x.Sku == sku)
            : _lines.FirstOrDefault(x => x.ExternalLineId == externalLineId);

        if (existing is not null)
        {
            return existing;
        }

        var line = new OrderLine(id, sku, quantity, unitPrice, externalLineId);
        _lines.Add(line);
        Recalculate();
        return line;
    }

    public void Recalculate()
    {
        Subtotal = _lines.Sum(x => x.LineTotal);
        GrandTotal = Subtotal - DiscountTotal + TaxTotal + ShippingTotal;
    }

    public void SetDiscount(decimal discountTotal)
    {
        DiscountTotal = discountTotal;
        Recalculate();
    }

    public void SetTotals(decimal taxTotal, decimal shippingTotal)
    {
        TaxTotal = taxTotal;
        ShippingTotal = shippingTotal;
        Recalculate();
    }

    public void SetStatus(OrderStatus status) => Status = status;
}

/// <summary>
/// Dòng đơn hàng. Lưu <see cref="Sku"/> và giá tại thời điểm mua (snapshot) —
/// không tham chiếu giá hiện tại của sản phẩm.
/// </summary>
public class OrderLine : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid OrderId { get; protected set; }

    public string Sku { get; protected set; } = default!;

    public Guid? ProductVariantId { get; set; }

    /// <summary>ID dòng đơn do sàn cấp (<c>trade_order_line_id</c>) — dùng để chống trùng webhook.</summary>
    public string? ExternalLineId { get; set; }

    public int Quantity { get; protected set; }

    public decimal UnitPrice { get; protected set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal => (UnitPrice * Quantity) - DiscountAmount + TaxAmount;

    protected OrderLine() { }

    public OrderLine(Guid id, string sku, int quantity, decimal unitPrice, string? externalLineId = null) : base(id)
    {
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
        ExternalLineId = externalLineId;
    }
}
