using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>🆕 **`S14`** — vòng đời **phiếu trả hàng** (`SAL-006` §3).</summary>
public enum ReturnLifecycleStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
    Received = 4,
    Closed = 5
}

/// <summary>🆕 **`S14`** — trạng thái **hàng hoàn** (trục thứ hai của phiếu, `SAL-006` §3).</summary>
public enum ReturnGoodsStatus
{
    Awaiting = 0,
    PartiallyReceived = 1,
    Received = 2,
    Waived = 3,
    NotReturned = 4
}

/// <summary>🆕 **`S14`** — cách hoàn tiền. 📌 `Q4` = **chỉ ghi nhận**: `Manual` + `RefundedAmount` do kế toán
/// ghi kèm chứng từ; **không** tự gọi cổng (gọi cổng là slice riêng có duyệt).</summary>
public enum ReturnRefundMethod
{
    Original = 0,
    Manual = 1,
    None = 2
}

/// <summary>🆕 **`S14`** — cách xử lý hàng nhận về (chỉ đặt sau khi **đã nhận**).</summary>
public enum ReturnDisposition
{
    Unset = 0,
    Restock = 1,
    Damaged = 2,
    ReturnToSupplier = 3
}

/// <summary>
/// 🆕 **`S14` — PHIẾU TRẢ HÀNG (RMA)** (`OrdReturns`) — `SAL-006`.
///
/// 🚨 **HAI TRỤC ĐỘC LẬP:** `ReturnLifecycleStatus` (thủ tục) và `ReturnGoodsStatus` (hàng đã về chưa).
/// 📌 Gộp làm một là mất khả năng trả lời *"phiếu đã duyệt nhưng hàng chưa về"* — đúng trạng thái vận hành
/// phổ biến nhất, và là chỗ **không được** hoàn tiền / **không được** hoàn kho.
///
/// 🔒 **BẤT BIẾN:** mọi chuyển trạng thái **NÉM** `BusinessException` khi sai guard (`D12` — cấm no-op im lặng);
/// `RefundedAmount ≤ Σ dòng` do tầng dịch vụ kiểm (DB **không** có cột "đã thu" trên đơn — `SAL-006` F11).
/// </summary>
public class OrderReturn : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid OrderId { get; protected set; }

    /// <summary>Mã phiếu `RT-yyyyMMdd-N` — **unique** theo tenant (`NULLS NOT DISTINCT`).</summary>
    public string ReturnNumber { get; protected set; } = default!;

    /// <summary>Copy từ `Order.ChannelCode` — **không** nhận từ client (chống giả mạo đối soát).</summary>
    public string ChannelCode { get; protected set; } = default!;

    public ReturnLifecycleStatus Status { get; protected set; } = ReturnLifecycleStatus.Requested;

    public ReturnGoodsStatus GoodsStatus { get; protected set; } = ReturnGoodsStatus.Awaiting;

    public string ReasonCode { get; protected set; } = default!;

    public string? ReasonText { get; protected set; }

    public Guid WarehouseId { get; protected set; }

    public ReturnRefundMethod RefundMethod { get; protected set; } = ReturnRefundMethod.None;

    /// <summary>Σ tiền **đã ghi nhận** hoàn của phiếu (ghi tay ở slice này — xem `Q4`).</summary>
    public decimal RefundedAmount { get; protected set; }

    public string RefundCurrency { get; protected set; } = "VND";

    public string RequestedByActorType { get; protected set; } = default!;

    public Guid? RequestedByActorId { get; protected set; }

    /// <summary>Khoá chống trùng yêu cầu tạo phiếu (`XC-002` BR-005) — **unique** `(TenantId, IdempotencyKey)`.</summary>
    public string IdempotencyKey { get; protected set; } = default!;

    /// <summary>SHA-256 payload canonical — phát hiện **dùng lại khoá với nội dung khác** (`Messaging:0002`).</summary>
    public string PayloadHash { get; protected set; } = default!;

    /// <summary>Bộ đếm của **phiếu** — mỗi chuyển trạng thái +1 (dùng cho `AggregateVersion` của ETO).</summary>
    public long Version { get; protected set; }

    public string CorrelationId { get; protected set; } = string.Empty;

    public DateTime? ReceivedAtUtc { get; protected set; }

    public DateTime? ClosedAtUtc { get; protected set; }

    /// <summary>Còn mở (chưa đóng/huỷ/từ chối) — chỉ phiếu còn mở mới nhận hàng/hoàn tiền.</summary>
    public bool ConMo => Status is ReturnLifecycleStatus.Requested or ReturnLifecycleStatus.Approved
        or ReturnLifecycleStatus.Received;

    protected OrderReturn() { }

    public OrderReturn(
        Guid id,
        Guid orderId,
        string returnNumber,
        string channelCode,
        Guid warehouseId,
        string reasonCode,
        string? reasonText,
        string idempotencyKey,
        string payloadHash,
        string requestedByActorType,
        Guid? requestedByActorId,
        string refundCurrency = "VND",
        string? correlationId = null) : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw Loi("Thiếu `OrderId` — phiếu trả **luôn** thuộc một đơn.");
        }

        if (string.IsNullOrWhiteSpace(returnNumber))
        {
            throw Loi("Thiếu `ReturnNumber`.");
        }

        if (string.IsNullOrWhiteSpace(channelCode))
        {
            throw Loi("Thiếu `ChannelCode` (phải copy từ đơn).");
        }

        if (warehouseId == Guid.Empty)
        {
            throw Loi("Thiếu `WarehouseId` — kho nhận hàng hoàn.");
        }

        // 📌 `ReasonCode` là **mã đóng**, không phải văn bản tự do (`BR-014`): lý do tự do không đếm/so sánh được
        // và dễ chứa PII (số điện thoại, địa chỉ) ⇒ `XC-005` BR-005.
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw Loi("Thiếu `ReasonCode` (lý do trả hàng phải là MÃ ĐÓNG, không phải văn bản tự do).");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw Loi("Thiếu `IdempotencyKey` — biên vào tạo phiếu **bắt buộc** idempotent (`XC-002` BR-004).");
        }

        OrderId = orderId;
        ReturnNumber = returnNumber.Trim();
        ChannelCode = channelCode.Trim();
        WarehouseId = warehouseId;
        ReasonCode = reasonCode.Trim();
        ReasonText = string.IsNullOrWhiteSpace(reasonText) ? null : reasonText.Trim();
        IdempotencyKey = idempotencyKey.Trim();
        PayloadHash = payloadHash;
        RequestedByActorType = requestedByActorType;
        RequestedByActorId = requestedByActorId;
        RefundCurrency = refundCurrency;
        CorrelationId = correlationId ?? string.Empty;
    }

    private BusinessException Loi(string lyDo)
        => new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe).WithData("lyDo", lyDo).WithData("returnId", Id);

    /// <summary>`Requested → Approved` (`BR-008`). **NÉM** nếu không ở `Requested`.</summary>
    public void Duyet(string actorType, Guid? actorId)
    {
        if (Status != ReturnLifecycleStatus.Requested)
        {
            throw Loi("Chỉ phiếu `Requested` mới duyệt được.").WithData("status", Status.ToString());
        }

        Status = ReturnLifecycleStatus.Approved;
        Version++;
        GhiNguoiDung(actorType, actorId);
    }

    /// <summary>`Requested → Rejected`. **NÉM** nếu không ở `Requested`. `BR-008`: phải có lý do.</summary>
    public void TuChoi(string actorType, Guid? actorId, string lyDo)
    {
        if (Status != ReturnLifecycleStatus.Requested)
        {
            throw Loi("Chỉ phiếu `Requested` mới từ chối được.").WithData("status", Status.ToString());
        }

        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw Loi("Từ chối phiếu trả **bắt buộc** có lý do.");
        }

        ReasonText = lyDo.Length > 512 ? lyDo[..512] : lyDo;
        Status = ReturnLifecycleStatus.Rejected;
        Version++;
        GhiNguoiDung(actorType, actorId);
    }

    /// <summary>`Requested|Approved → Cancelled` (khách/đơn huỷ). 🚨 **Cấm** huỷ khi **đã nhận hàng**.</summary>
    public void Huy(string actorType, Guid? actorId, string lyDo)
    {
        if (Status is ReturnLifecycleStatus.Rejected or ReturnLifecycleStatus.Cancelled or ReturnLifecycleStatus.Closed)
        {
            throw Loi("Phiếu đã kết thúc ⇒ không huỷ được.").WithData("status", Status.ToString());
        }

        if (GoodsStatus is ReturnGoodsStatus.Received or ReturnGoodsStatus.PartiallyReceived)
        {
            throw Loi("ĐÃ NHẬN HÀNG ⇒ không huỷ phiếu được (phải nhận lại đủ rồi đóng).");
        }

        if (string.IsNullOrWhiteSpace(lyDo))
        {
            throw Loi("Huỷ phiếu trả **bắt buộc** có lý do.");
        }

        Status = ReturnLifecycleStatus.Cancelled;
        Version++;
        GhiNguoiDung(actorType, actorId);
    }

    /// <summary>
    /// Ghi nhận **đã nhận hàng hoàn** và disposition cho từng dòng. **NÉM** nếu chưa `Approved`,
    /// số lượng nhận không hợp lệ, hoặc chưa chọn disposition.
    /// 📌 **Không** tự hoàn kho ở đây: bút toán kho do tầng dịch vụ tạo qua chứng từ `CustomerReturn`
    /// (slice riêng) — entity chỉ giữ **sự thật nghiệp vụ**.
    /// </summary>
    public void NhanHang(
        string actorType,
        Guid? actorId,
        IReadOnlyCollection<(OrderReturnLine Dong, int SoLuongNhan, ReturnDisposition XuLy)> ketQua,
        DateTime atUtc)
    {
        if (Status != ReturnLifecycleStatus.Approved)
        {
            throw Loi("Chỉ phiếu `Approved` mới ghi nhận hàng hoàn.").WithData("status", Status.ToString());
        }

        if (ketQua.Count == 0)
        {
            throw Loi("Không có dòng nào để ghi nhận.");
        }

        foreach (var (dong, soLuongNhan, xuLy) in ketQua)
        {
            dong.GhiNhanHang(soLuongNhan, xuLy);
        }

        GoodsStatus = ketQua.All(x => x.SoLuongNhan >= x.Dong.Quantity)
            ? ReturnGoodsStatus.Received
            : ReturnGoodsStatus.PartiallyReceived;

        if (GoodsStatus == ReturnGoodsStatus.Received)
        {
            Status = ReturnLifecycleStatus.Received;
            ReceivedAtUtc = atUtc;
        }

        Version++;
        GhiNguoiDung(actorType, actorId);
    }

    /// <summary>
    /// Đóng phiếu (`→ Closed`). **NÉM** nếu chưa duyệt, hoặc hàng chưa ở trạng thái kết thúc
    /// (`Received`/`Waived`/`NotReturned`) — 📌 đóng khi hàng còn `Awaiting` nghĩa là **bỏ quên** hàng hoàn.
    /// </summary>
    public void Dong(string actorType, Guid? actorId, DateTime atUtc)
    {
        if (Status is ReturnLifecycleStatus.Closed or ReturnLifecycleStatus.Rejected or ReturnLifecycleStatus.Cancelled)
        {
            throw Loi("Phiếu đã kết thúc ⇒ không đóng được.").WithData("status", Status.ToString());
        }

        if (GoodsStatus is ReturnGoodsStatus.Awaiting or ReturnGoodsStatus.PartiallyReceived)
        {
            throw Loi("Hàng hoàn CHƯA về đủ ⇒ không đóng phiếu.").WithData("goods", GoodsStatus.ToString());
        }

        Status = ReturnLifecycleStatus.Closed;
        ClosedAtUtc = atUtc;
        Version++;
        GhiNguoiDung(actorType, actorId);
    }

    /// <summary>Kế toán ghi nhận **đã hoàn tiền** (thủ công) — `Q4`: **chỉ ghi nhận**, không gọi cổng.</summary>
    public void GhiNhanHoanTien(decimal soTien, ReturnRefundMethod phuongThuc)
    {
        if (!ConMo)
        {
            throw Loi("Phiếu đã kết thúc ⇒ không ghi nhận hoàn tiền.").WithData("status", Status.ToString());
        }

        if (soTien <= 0m)
        {
            throw Loi("Số tiền hoàn phải > 0 (bản ghi hoàn 0 đồng không phải hoàn tiền).").WithData("soTien", soTien);
        }

        RefundedAmount += soTien;
        RefundMethod = phuongThuc;
        Version++;
    }

    private void GhiNguoiDung(string actorType, Guid? actorId)
    {
        RequestedByActorType = actorType;
        RequestedByActorId = actorId;
    }
}

/// <summary>🆕 **`S14`** — **MỘT DÒNG TRẢ** (`OrdReturnLines`) — bám đúng **một** dòng đơn (`SAL-006` §5.1).</summary>
public class OrderReturnLine : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ReturnId { get; protected set; }

    public Guid OrderLineId { get; protected set; }

    /// <summary>Copy từ dòng đơn (snapshot) — SKU trên đơn có thể đổi sau này.</summary>
    public string Sku { get; protected set; } = default!;

    public int Quantity { get; protected set; }

    public int ReceivedQuantity { get; protected set; }

    public ReturnDisposition Disposition { get; protected set; } = ReturnDisposition.Unset;

    public decimal UnitPriceSnapshot { get; protected set; }

    public decimal DiscountAmountSnapshot { get; protected set; }

    public decimal TaxAmountSnapshot { get; protected set; }

    public decimal RefundAmount { get; protected set; }

    public Guid? LedgerEntryId { get; protected set; }

    protected OrderReturnLine() { }

    public OrderReturnLine(
        Guid id,
        Guid returnId,
        Guid orderLineId,
        string sku,
        int quantity,
        decimal unitPriceSnapshot,
        decimal discountAmountSnapshot,
        decimal taxAmountSnapshot) : base(id)
    {
        if (quantity < 1)
        {
            throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                .WithData("lyDo", "Số lượng trả phải ≥ 1.").WithData("sku", sku).WithData("quantity", quantity);
        }

        ReturnId = returnId;
        OrderLineId = orderLineId;
        Sku = sku.Trim();
        Quantity = quantity;
        UnitPriceSnapshot = unitPriceSnapshot;
        DiscountAmountSnapshot = discountAmountSnapshot;
        TaxAmountSnapshot = taxAmountSnapshot;

        // 📌 Tiền hoàn của dòng = giá **sau giảm** (thuế giữ riêng để đảo hoá đơn — `SAL-006` ADR-03).
        RefundAmount = (unitPriceSnapshot - discountAmountSnapshot) * quantity;
    }

    /// <summary>Ghi nhận hàng về cho dòng này. **NÉM** nếu vượt số yêu cầu hoặc disposition không hợp lệ.</summary>
    public void GhiNhanHang(int soLuongNhan, ReturnDisposition xuLy)
    {
        if (soLuongNhan < 0 || soLuongNhan > Quantity)
        {
            throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                .WithData("lyDo", "Số lượng nhận phải trong [0, Quantity].")
                .WithData("soLuongNhan", soLuongNhan).WithData("quantity", Quantity);
        }

        if (xuLy == ReturnDisposition.Unset)
        {
            throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                .WithData("lyDo", "Đã nhận hàng ⇒ phải chọn cách xử lý (Restock/Damaged/ReturnToSupplier).");
        }

        ReceivedQuantity = soLuongNhan;
        Disposition = xuLy;
    }
}

/// <summary>🆕 **`S14`** — **NHẬT KÝ CHUYỂN TRẠNG THÁI PHIẾU TRẢ** (`OrdReturnHistory`) — **APPEND-ONLY**.</summary>
public class OrderReturnHistory : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid ReturnId { get; protected set; }

    /// <summary>`Lifecycle` hoặc `Goods` (chuỗi để đọc log/SQL được).</summary>
    public string Axis { get; protected set; } = default!;

    public int FromStatus { get; protected set; }

    public int ToStatus { get; protected set; }

    public string ActorType { get; protected set; } = default!;

    public Guid? ActorId { get; protected set; }

    public string ReasonCode { get; protected set; } = default!;

    public string? ReasonText { get; protected set; }

    public string CorrelationId { get; protected set; } = string.Empty;

    public Guid EventId { get; protected set; }

    public long VersionAfter { get; protected set; }

    public DateTime OccurredAtUtc { get; protected set; }

    protected OrderReturnHistory() { }

    public OrderReturnHistory(
        Guid id,
        Guid returnId,
        string axis,
        int fromStatus,
        int toStatus,
        string actorType,
        Guid? actorId,
        string reasonCode,
        string? reasonText,
        Guid eventId,
        long versionAfter,
        DateTime occurredAtUtc,
        string? correlationId = null) : base(id)
    {
        if (fromStatus == toStatus)
        {
            throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                .WithData("lyDo", "Lịch sử chỉ ghi khi trạng thái THAY ĐỔI (from = to là dòng vô nghĩa).");
        }

        ReturnId = returnId;
        Axis = axis;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorType = actorType;
        ActorId = actorId;
        ReasonCode = reasonCode;
        ReasonText = reasonText;
        EventId = eventId;
        VersionAfter = versionAfter;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId ?? string.Empty;
    }
}


