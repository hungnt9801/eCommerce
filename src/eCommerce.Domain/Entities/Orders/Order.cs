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

    /// <summary>
    /// **TRỤC TÀI CHÍNH CỦA ĐƠN** (`SAL-003` trục 2) — *"khách đã trả tiền chưa"*.
    ///
    /// 🚨 **`S4` (18/10/2026) — KHÔNG GÁN TRỰC TIẾP ĐƯỢC NỮA.** Trước đó trường này có **setter công khai** và
    /// **không một dòng nào** trong toàn repo gán nó (grep: chỉ đọc để hiển thị) ⇒ *"khách trả tiền xong thì đơn
    /// vẫn `Unpaid`"* (`PAY-001` §3.5 — nợ #1 của tài liệu đó). Nay đường ghi **duy nhất** là
    /// <see cref="DatTrangThaiThanhToan"/> — hàm có **luật hấp thụ**, gọi từ cầu nối
    /// `OrderPaymentStatusHandler` (nhận `PaymentSucceededEto`/`PaymentFailedEto`).
    /// 🔒 Giữ setter **kín** là có chủ ý: `SAL-003` **BR-013** nói trục tài chính **chỉ** `PaymentHub`/IPN ghi —
    /// một setter công khai cho phép *"ai cũng ghi"*, và đó là cách hai nguồn sự thật sinh ra.
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Unpaid;

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

    /// <summary>
    /// 🚨 **Khoá idempotency của lần checkout đã sinh ra đơn này** (`SAL-001`).
    /// 📌 **VÌ SAO CẦN CỘT NÀY (không thể suy ra từ chỗ khác):**
    ///   • **Xác nhận đơn** phải biết **chốt lượt giữ chỗ nào** ⇒ cần khoá để tra `Checkout:{khoá}`;
    ///   • **Chống tạo HAI đơn cho MỘT checkout** (khách bấm hai lần / client retry) ⇒ cần **unique index**;
    ///   • **Truy vết ngược** từ đơn về **bằng chứng giá** (`PrcPriceCalculationRecords`) để trả lời khiếu nại.
    /// ⚠️ Trước vòng 33 khoá này **chỉ có trong log** ⇒ cả ba việc trên **không làm được** (xem `inv-001` §24.5 #2).
    /// </summary>
    public string? CheckoutIdempotencyKey { get; set; }

    private readonly List<OrderLine> _lines = new();

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    protected Order() { }

    public Order(Guid id, string number, string channelCode) : base(id)
    {
        Number = number;
        ChannelCode = channelCode;
    }

    /// <summary>
    /// Tạo đơn **kèm chiều tenant** — dùng khi nguồn đơn là **hệ thống ngoài** (sàn TMĐT) và tenant
    /// đến từ dữ liệu nguồn chứ không từ `ICurrentTenant` của tiến trình đang xử lý.
    ///
    /// 🎯 Vì sao cần ctor riêng: `TenantId` là `protected set`, mà tầng app (nơi nhận đơn sàn) **không**
    /// được phép gán trực tiếp. Nếu bỏ qua, tenant của đơn nguồn sẽ **rơi im lặng** và chỉ còn tenant
    /// của tiến trình — đúng loại mất mát không ai phát hiện (`XC-004` F2 ghi nhận multi-tenancy chưa bật).
    /// </summary>
    public Order(Guid id, string number, string channelCode, Guid? tenantId) : this(id, number, channelCode)
    {
        TenantId = tenantId;
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

    /// <summary>
    /// 🚨 **GHI TRỤC TÀI CHÍNH — ĐƯỜNG DUY NHẤT, CÓ LUẬT HẤP THỤ** (`S4`).
    ///
    /// **VÌ SAO PHẢI LÀ MỘT HÀM (không phải setter):** `PaymentStatus` là *"sự thật về tiền"*; nếu chỗ gọi được
    /// gán tự do thì một IPN thất bại đến **sau** IPN thành công (at-least-once, hoặc hai luồng song song) sẽ
    /// **hạ** đơn từ `Paid` về `Failed` ⇒ hệ thống **tưởng khách chưa trả** ⇒ **thu tiền lần hai** hoặc **không
    /// giao hàng** — đúng họ lỗi mà `PaymentTransaction.MarkSucceeded`/`MarkFailed` đã phải sửa ngày 16/10/2026,
    /// chỉ là ở phía **đơn**.
    ///
    /// 🔒 **Luật (giống bên giao dịch, hai phía phải NHẤT QUÁN — lệch nhau là mầm sổ sách tự mâu thuẫn):**
    /// <list type="bullet">
    ///   <item>`Refunded` là trạng thái **HẤP THỤ** — không đi đâu được nữa (kể cả về `Paid`);</item>
    ///   <item>Đã có tiền (`Paid`/`PartiallyRefunded`) ⇒ **cấm** hạ về `Failed`/`Pending`/`Unpaid`; chỉ được
    ///     **tiến** sang `PartiallyRefunded`/`Refunded`;</item>
    ///   <item>Trước khi có tiền (`Unpaid`/`Pending`/`Failed`) ⇒ đi đâu cũng được — 🔑 kể cả `Failed → Paid`
    ///     (`querydr` xác nhận tiền **đã** vào, lưới an toàn khi IPN mất).</item>
    /// </list>
    /// </summary>
    /// <returns>`true` = trạng thái **thực sự** đổi (đã ghi); `false` = **không đổi** (đã ở đúng trạng thái đó,
    /// hoặc bị luật hấp thụ chặn). 📌 Giá trị này là **bắt buộc** đọc: nếu chỗ gọi bỏ qua thì không phân biệt
    /// được *"đã ghi"* với *"bị chặn"* — đúng lỗi `F-PAY-9` mà `MarkFailed` vừa sửa bên phía giao dịch.
    /// </returns>
    public bool DatTrangThaiThanhToan(PaymentStatus moi)
    {
        if (PaymentStatus == moi)
        {
            return false;
        }

        if (LaDaThuTien(PaymentStatus))
        {
            // Hấp thụ: đã hoàn đủ thì **không** có đường ra (kể cả "hồi sinh" về Paid).
            if (PaymentStatus == PaymentStatus.Refunded)
            {
                return false;
            }

            // Đang có tiền ⇒ chỉ được TIẾN sang trạng thái vẫn phản ánh "đã thu / đã hoàn".
            if (moi is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded))
            {
                return false;
            }
        }

        PaymentStatus = moi;
        return true;
    }

    /// <summary>Đơn **đã từng** thu được tiền (`Paid`/`PartiallyRefunded`/`Refunded`).</summary>
    private static bool LaDaThuTien(PaymentStatus trangThai)
        => trangThai is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded;
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
