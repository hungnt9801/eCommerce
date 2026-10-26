namespace eCommerce.Application.Contracts.Sales;

/// <summary>
/// DTO trạng thái đơn trên **4 trục** (`SAL-003` §6.1) — 🔒 **chỉ đọc**: mọi thay đổi đi qua app service
/// có permission riêng, không có `setter` nào ở đây.
/// </summary>
public class OrderStatusDto
{
    public Guid OrderId { get; set; }

    public FulfillmentStatus FulfillmentStatus { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public ReconciliationStatus ReconciliationStatus { get; set; }

    public ReturnStatus ReturnStatus { get; set; }

    public bool IsCancelled { get; set; }

    /// <summary>Giá trị **SUY RA** từ 4 trục (`SAL-003` D2) — chỉ để hiển thị/tương thích.</summary>
    public OrderStatus DerivedStatus { get; set; }

    public long FulfillmentVersion { get; set; }

    public long PaymentVersion { get; set; }

    public long ReconciliationVersion { get; set; }

    public long ClosureVersion { get; set; }

    /// <summary>Id dòng lịch sử vừa sinh — để UI cuộn tới đúng dòng.</summary>
    public Guid? HistoryEntryId { get; set; }
}

/// <summary>Một dòng lịch sử trạng thái (`OrdStatusHistory`) — **append-only**.</summary>
public class OrderStatusHistoryDto
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string Axis { get; set; } = default!;

    public int FromStatus { get; set; }

    public int ToStatus { get; set; }

    public string ActorType { get; set; } = default!;

    public Guid? ActorId { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public long VersionAfter { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>
/// Chuyển **trục 1 — thực hiện**. `ExpectedVersion` là `FulfillmentVersion` UI đã đọc
/// ⇒ lệch là `eCommerce:Order:0003` (BR-009), **không** ghi đè im lặng.
/// </summary>
public class ChangeFulfillmentStatusDto
{
    public Guid OrderId { get; set; }

    public FulfillmentStatus ToStatus { get; set; }

    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }
}

/// <summary>Chuyển **trục 3 — đối soát** (chỉ job/kế toán).</summary>
public class ChangeReconciliationStatusDto
{
    public Guid OrderId { get; set; }

    public ReconciliationStatus ToStatus { get; set; }

    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }
}

/// <summary>Khách/nhân viên **yêu cầu trả** (`Open → ReturnRequested`).</summary>
public class RequestReturnDto
{
    public Guid OrderId { get; set; }

    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }
}

/// <summary>Duyệt/từ chối trả (`ReturnRequested → ReturnApproved | ReturnRejected`).</summary>
public class ApproveReturnDto
{
    public Guid OrderId { get; set; }

    public bool Approve { get; set; }

    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }
}

/// <summary>
/// Huỷ đơn. 🚨 **KHÔNG** phải "đặt trạng thái": nghiệp vụ bù trừ (nhả giữ chỗ) thuộc `SAL-004`;
/// app service gọi cùng đường với `DonHangAppService.HuyDonAsync` để **không** có hai định nghĩa.
/// </summary>
public class CancelOrderDto
{
    public Guid OrderId { get; set; }

    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }
}
