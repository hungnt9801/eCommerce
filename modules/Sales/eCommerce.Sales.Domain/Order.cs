using Volo.Abp;
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

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 **`SAL-003` — 4 TRỤC TRẠNG THÁI TRỰC GIAO.** `Status` ở trên nay là **giá trị SUY RA** (chỉ để
    // hiển thị/tương thích ngược) — **KHÔNG** có đường ghi công khai. Mọi chuyển dịch đi qua
    // `ChuyenThucHien`/`ChuyenDoiSoat`/`ChuyenKetThuc`/`HuyDon`/`DatTrangThaiThanhToan`.
    //
    // 🔑 VÌ SAO PHẢI TÁCH: ca phổ biến nhất ở VN — *"đã giao nhưng CHƯA thu tiền"* (COD) — **không biểu diễn
    // được** bằng một enum. Nếu gộp, mỗi lần thêm giá trị phải tự hỏi "giá trị này thuộc trục nào", và câu hỏi
    // vận hành *"đơn nào chưa thu tiền"* trở thành phép quét chuỗi.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Trục 1 — thực hiện. Mặc định `Pending`.</summary>
    public FulfillmentStatus FulfillmentStatus { get; private set; } = FulfillmentStatus.Pending;

    /// <summary>
    /// Trục 3 — đối soát. Mặc định `AwaitingReconcile` (`SAL-003` §3: trạng thái đầu có thể là
    /// `NotRequired` **hoặc** `AwaitingReconcile`). 📌 Chọn `AwaitingReconcile` vì **mọi** đơn trong hệ thống
    /// đều có tiền cần đối soát; `NotRequired` là **hấp thụ** nên nếu chọn nó làm mặc định thì trục 3
    /// **không bao giờ** bắt đầu được — đúng lỗi "trục chết" (đã tự phát hiện khi viết test).
    /// </summary>
    public ReconciliationStatus ReconciliationStatus { get; private set; } = ReconciliationStatus.AwaitingReconcile;

    /// <summary>Trục 4 — kết thúc (trả/huỷ). Mặc định `Open`.</summary>
    public ReturnStatus ReturnStatus { get; private set; } = ReturnStatus.Open;

    /// <summary>Cờ truy vấn nhanh "đơn đã huỷ" — **luôn** đồng bộ với `ReturnStatus == Cancelled`.</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>`AggregateVersion` **của từng trục** (D6) — tăng đúng 1 mỗi lần chuyển của trục đó.</summary>
    public long FulfillmentVersion { get; private set; }

    public long PaymentVersion { get; private set; }

    public long ReconciliationVersion { get; private set; }

    public long ClosureVersion { get; private set; }

    /// <summary>
    /// **CONCURRENCY TOKEN CỦA CẢ DÒNG** (`SAL-003` D5/BR-010) — EF dùng **giá trị GỐC** (đọc từ DB) trong
    /// `WHERE`, nên hai request cùng sửa một đơn thì **request sau** nhận `DbUpdateConcurrencyException`
    /// ⇒ app service map thành `eCommerce:Order:0002` (**không** ghi đè im lặng).
    ///
    /// 🔑 **VÌ SAO TĂNG TRONG `Chuyen` (không phải DB trigger):** mọi thay đổi trạng thái đi qua **một** cửa;
    /// tăng ở đó là **tất định**, kiểm được bằng unit test, và không phụ thuộc cơ chế riêng của DBMS.
    /// 📌 Khác `AggregateVersion` (tăng theo **từng trục**): `RowVersion` tăng theo **mọi** thay đổi của dòng.
    /// ⚠️ Tăng ở đây **trước** khi lưu; EF vẫn so **giá trị gốc** ⇒ không có chuyện "tự sửa rồi tự khớp".
    /// </summary>
    public long RowVersion { get; private set; }

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

    /// <summary>
    /// Lịch sử chuyển trạng thái (append-only). 📌 Nạp **cùng aggregate** để không thể "quên ghi lịch sử":
    /// bản ghi được thêm **trong chính phương thức chuyển trạng thái**, cùng transaction với đổi trạng thái.
    /// </summary>
    private readonly List<OrderStatusHistory> _statusHistory = new();

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory;

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

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 **ĐÃ XOÁ `SetStatus` (24/10/2026, `SAL-003` D4).** Trước đây là
    // `public void SetStatus(OrderStatus status) => Status = status;` — một **phép gán trần**:
    // `Completed → Pending`, `Cancelled → Shipped`… **đều được chấp nhận im lặng**, vi phạm quy ước #11.
    // 🔒 Nay **mọi** chuyển dịch đi qua 4 phương thức dưới đây; chuyển sai ⇒ **NÉM** `eCommerce:Order:0001`
    // (hoặc `0006` nếu trạng thái hấp thụ). Giữ lại ghi chú này để không ai "khôi phục cho tiện".
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// **TRỤC 1 — THỰC HIỆN.** Chuyển `FulfillmentStatus` theo bảng chuyển (`SAL-003` §3, cạnh 1–9).
    /// </summary>
    public OrderStatusHistory ChuyenThucHien(
        FulfillmentStatus moi,
        StatusActorType actor,
        Guid? actorId,
        string reasonCode,
        string? correlationId = null,
        string? reasonText = null,
        DateTime? occurredAtUtc = null)
        => Chuyen(
            OrderAxis.Fulfillment,
            (int)FulfillmentStatus,
            (int)moi,
            actor,
            actorId,
            reasonCode,
            correlationId,
            reasonText,
            occurredAtUtc);

    /// <summary>
    /// **TRỤC 3 — ĐỐI SOÁT.** Chuyển `ReconciliationStatus` (cạnh 20–24). GĐ1 chưa có caller thật
    /// (`REC-002`/`REC-003` sẽ gọi); có sẵn để **không** phải mở lại entity khi làm GĐ2.
    /// </summary>
    public OrderStatusHistory ChuyenDoiSoat(
        ReconciliationStatus moi,
        StatusActorType actor,
        Guid? actorId,
        string reasonCode,
        string? correlationId = null,
        string? reasonText = null,
        DateTime? occurredAtUtc = null)
        => Chuyen(
            OrderAxis.Reconciliation,
            (int)ReconciliationStatus,
            (int)moi,
            actor,
            actorId,
            reasonCode,
            correlationId,
            reasonText,
            occurredAtUtc);

    /// <summary>
    /// **TRỤC 4 — KẾT THÚC.** Yêu cầu trả / duyệt trả / từ chối / đã trả (`SAL-003` cạnh 25–28).
    /// ⚠️ Huỷ đơn **không** đi qua đây — dùng <see cref="HuyDon"/> (có guard "chưa xuất kho").
    /// </summary>
    public OrderStatusHistory ChuyenKetThuc(
        ReturnStatus moi,
        StatusActorType actor,
        Guid? actorId,
        string reasonCode,
        string? correlationId = null,
        string? reasonText = null,
        DateTime? occurredAtUtc = null)
        => Chuyen(
            OrderAxis.Closure,
            (int)ReturnStatus,
            (int)moi,
            actor,
            actorId,
            reasonCode,
            correlationId,
            reasonText,
            occurredAtUtc);

    /// <summary>
    /// **HUỶ ĐƠN** — trục kết thúc `Open → Cancelled` (cạnh 29).
    ///
    /// 🚨 **GUARD NGHIỆP VỤ (không chỉ là cạnh):** chỉ huỷ được khi **chưa xuất kho**
    /// (`FulfillmentStatus == Pending`). Đơn đã xác nhận (hàng đã ra khỏi kho) thì phải **TRẢ HÀNG**
    /// (chứng từ nhập) — xoá bút toán sổ cái là **cấm** (append-only). Guard sai ⇒ `0001`.
    /// </summary>
    public OrderStatusHistory HuyDon(
        StatusActorType actor,
        Guid? actorId,
        string reasonCode,
        string? correlationId = null,
        string? reasonText = null,
        DateTime? occurredAtUtc = null)
    {
        if (IsCancelled)
        {
            throw new BusinessException(OrderErrorCodes.TrangThaiHapThu)
                .WithData("axis", OrderAxis.Closure.ToString())
                .WithData("from", ReturnStatus.ToString())
                .WithData("to", ReturnStatus.Cancelled.ToString());
        }

        if (FulfillmentStatus != FulfillmentStatus.Pending)
        {
            throw new BusinessException(OrderErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("axis", OrderAxis.Closure.ToString())
                .WithData("from", ReturnStatus.ToString())
                .WithData("to", ReturnStatus.Cancelled.ToString())
                .WithData("lyDo", "Đơn đã xuất kho ⇒ phải TRẢ HÀNG (chứng từ nhập), không được huỷ.");
        }

        var lichSu = Chuyen(
            OrderAxis.Closure,
            (int)ReturnStatus,
            (int)ReturnStatus.Cancelled,
            actor,
            actorId,
            reasonCode,
            correlationId,
            reasonText,
            occurredAtUtc);

        IsCancelled = true;
        Status = TinhTrangThaiSuyRa();

        return lichSu;
    }

    /// <summary>
    /// **LÕI DUY NHẤT** của mọi chuyển trạng thái: kiểm trục → kiểm hấp thụ → kiểm cạnh → tăng version →
    /// ghi trục → **ghi lịch sử** → suy ra `Status`. Mọi phương thức công khai đều đi qua đây.
    ///
    /// 📌 Thứ tự kiểm **quan trọng**: kiểm **hấp thụ trước** để đơn đã huỷ/đã trả nhận đúng mã `0006`
    /// (không phải `0001`) — hai mã này dẫn tới **hai hành vi UI khác nhau** (gợi ý tạo đơn mới vs bấm lại).
    /// </summary>
    private OrderStatusHistory Chuyen(
        OrderAxis truc,
        int tu,
        int den,
        StatusActorType actor,
        Guid? actorId,
        string reasonCode,
        string? correlationId,
        string? reasonText,
        DateTime? occurredAtUtc)
    {
        if (!OrderStateMachine.LaTrucHopLe(truc))
        {
            throw new BusinessException(OrderErrorCodes.TrucKhongHopLe)
                .WithData("axis", truc.ToString());
        }

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            // BR-SAL-003-006: không có lý do ⇒ không có chứng từ. Không cho phép chuỗi rỗng.
            throw new BusinessException(OrderErrorCodes.ThieuThongTinChuyenTrangThai)
                .WithData("axis", truc.ToString())
                .WithData("lyDo", "Thiếu ReasonCode.");
        }

        // 🚨 **ĐƠN ĐÃ KẾT THÚC THÌ KHÔNG TRỤC NÀO ĐI ĐƯỢC NỮA — TRỪ TRỤC TÀI CHÍNH.**
        // Kiểm **trước** guard của từng trục vì `Cancelled`/`Returned` nằm ở trục 4, còn lời gọi sai thường
        // đến từ trục 1/2/3 — nếu chỉ kiểm trục hiện tại thì `Cancelled` + `ChuyenThucHien(Confirmed)` sẽ **lọt**.
        //
        // 🔑 **VÌ SAO TRỤC TÀI CHÍNH ĐƯỢC MIỄN:** tiền có thể về **sau** khi đơn đã huỷ/đã trả (IPN đến muộn,
        // khách chuyển khoản chậm, hoàn tiền sau khi trả hàng). Trục tài chính là **sự thật về tiền** — chặn nó
        // nghĩa là *"tiền đã vào mà sổ không biết"*, đúng lỗi tệ nhất của miền thanh toán. 📌 Đơn vẫn `Cancelled`;
        // việc hoàn tiền là của `SAL-006`, nhưng **bằng chứng đã thu** phải được ghi.
        if (truc != OrderAxis.Payment
            && (IsCancelled || ReturnStatus is ReturnStatus.Returned
                or ReturnStatus.Cancelled
                or ReturnStatus.ReturnRejected))
        {
            throw new BusinessException(OrderErrorCodes.TrangThaiHapThu)
                .WithData("axis", truc.ToString())
                .WithData("from", tu)
                .WithData("to", den)
                .WithData("lyDo", "Đơn đã kết thúc (huỷ/trả) — muốn bán lại phải tạo đơn mới.");
        }

        if (OrderStateMachine.LaHapThu(truc, tu))
        {
            throw new BusinessException(OrderErrorCodes.TrangThaiHapThu)
                .WithData("axis", truc.ToString())
                .WithData("from", tu)
                .WithData("to", den);
        }

        if (tu == den || !OrderStateMachine.CoCanh(truc, tu, den))
        {
            throw new BusinessException(OrderErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("axis", truc.ToString())
                .WithData("from", tu)
                .WithData("to", den);
        }

        if (actor is StatusActorType.Staff && actorId is null)
        {
            // BR-006: thao tác của nhân viên phải truy được **người** làm. Job/webhook/IPN mới được null.
            throw new BusinessException(OrderErrorCodes.ThieuThongTinChuyenTrangThai)
                .WithData("axis", truc.ToString())
                .WithData("lyDo", "Thiếu ActorId cho thao tác của nhân viên.");
        }

        var version = TangVersion(truc);
        RowVersion++;
        DatTruc(truc, den);

        Status = TinhTrangThaiSuyRa();

        var lichSu = new OrderStatusHistory(
            Guid.NewGuid(),
            Id,
            OrderStateMachine.TenTruc(truc),
            tu,
            den,
            actor,
            actorId,
            reasonCode,
            correlationId,
            version,
            occurredAtUtc ?? DateTime.UtcNow,
            reasonText);

        _statusHistory.Add(lichSu);

        // 📣 **ETO TRONG CÙNG TRANSACTION** (`SAL-003` BR-017 / `XC-002` BR-001) — đi qua **outbox**:
        // `AddDistributedEvent` của ABP chỉ **ghi nhận**; UoW ghi nó xuống `AbpEventOutbox` **cùng** lúc ghi
        // trạng thái + lịch sử, `OutboxSender` phát sau commit. 🔒 Nếu ai gọi thẳng `IDistributedEventBus`
        // trong app service thì *"đã đổi trạng thái nhưng event mất"* lại quay về.
        // 📌 Gắn `INotificationEvent` vì dữ liệu đã nằm trong `OrderStatusHistory` (mất event ≠ mất dữ liệu).
        AddDistributedEvent(new OrderStatusChangedEto
        {
            OrderId = Id,
            TenantId = TenantId,
            Number = Number,
            Axis = lichSu.Axis,
            FromStatus = tu,
            ToStatus = den,
            VersionAfter = version,
            ActorType = lichSu.ActorType,
            ActorId = actorId,
            ReasonCode = reasonCode,
            CorrelationId = lichSu.CorrelationId,
            EventId = lichSu.EventId,
            OccurredAtUtc = lichSu.OccurredAtUtc
        });

        return lichSu;
    }

    /// <summary>Tăng `AggregateVersion` của **đúng trục** và trả về giá trị sau khi tăng.</summary>
    private long TangVersion(OrderAxis truc)
    {
        switch (truc)
        {
            case OrderAxis.Fulfillment:
                return ++FulfillmentVersion;
            case OrderAxis.Payment:
                return ++PaymentVersion;
            case OrderAxis.Reconciliation:
                return ++ReconciliationVersion;
            case OrderAxis.Closure:
                return ++ClosureVersion;
            default:
                throw new BusinessException(OrderErrorCodes.TrucKhongHopLe).WithData("axis", truc.ToString());
        }
    }

    private void DatTruc(OrderAxis truc, int giaTri)
    {
        switch (truc)
        {
            case OrderAxis.Fulfillment:
                FulfillmentStatus = (FulfillmentStatus)giaTri;
                break;
            case OrderAxis.Payment:
                PaymentStatus = (PaymentStatus)giaTri;
                break;
            case OrderAxis.Reconciliation:
                ReconciliationStatus = (ReconciliationStatus)giaTri;
                break;
            case OrderAxis.Closure:
                ReturnStatus = (ReturnStatus)giaTri;
                break;
            default:
                throw new BusinessException(OrderErrorCodes.TrucKhongHopLe).WithData("axis", truc.ToString());
        }
    }

    /// <summary>
    /// Suy ra `Status` từ 4 trục (`SAL-003` D2 — **một** định nghĩa, chỉ domain ghi).
    /// Thứ tự ưu tiên: **huỷ/trả** trước, rồi trục thực hiện. 📌 Không bao giờ đọc `Status` để suy ra chính nó.
    /// </summary>
    private OrderStatus TinhTrangThaiSuyRa()
        => OrderStateMachine.TinhTrangThaiSuyRa(FulfillmentStatus, ReturnStatus, IsCancelled);

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
    public bool DatTrangThaiThanhToan(
        PaymentStatus moi,
        StatusActorType actor = StatusActorType.PaymentGateway,
        Guid? actorId = null,
        string reasonCode = "CapNhatTuCongThanhToan",
        string? correlationId = null,
        DateTime? occurredAtUtc = null)
    {
        if (PaymentStatus == moi)
        {
            return false;
        }

        // 🔒 Hấp thụ: đã `Refunded` thì **không** có đường ra (kể cả "hồi sinh" về `Paid`).
        // 📌 Kiểm ở đây (không chỉ ở bảng cạnh) để giữ **hợp đồng cũ**: hàm này trả `false`, **không ném** —
        // các caller (job đối soát, handler IPN) dựa vào giá trị đó để đếm "thực sự đổi" (`F-PAY-9`).
        if (OrderStateMachine.LaHapThu(OrderAxis.Payment, (int)PaymentStatus))
        {
            return false;
        }

        if (!OrderStateMachine.CoCanh(OrderAxis.Payment, (int)PaymentStatus, (int)moi))
        {
            return false;
        }

        Chuyen(
            OrderAxis.Payment,
            (int)PaymentStatus,
            (int)moi,
            actor,
            actorId,
            reasonCode,
            correlationId,
            reasonText: null,
            occurredAtUtc);

        return true;
    }
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
