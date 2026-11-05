namespace eCommerce.Application.Contracts.Sales;

using eCommerce.Orders;

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

    /// <summary>
    /// 🆕 **`WP-05`** — **một nhãn hiển thị** suy ra từ 4 trục (`OrderStateMachine.NhanHienThi`,
    /// `03 §3.5`). UI hiện đúng nhãn này; DB vẫn giữ 4 trục.
    /// </summary>
    public string StatusLabel { get; set; } = default!;

    /// <summary>
    /// 🆕 **`WP-05`** — **tác động kho của bước vừa rồi**: `ChotGiuCho` (xuất kho) · `NhapHangHoan` ·
    /// `Khong`. 📌 Trả về để UI **nói ra** điều đã xảy ra với kho thay vì để người dùng tự đoán.
    /// </summary>
    public string StockEffect { get; set; } = nameof(OrderStateMachine.TacDongKho.Khong);

    /// <summary>Số **lượt giữ chỗ đã chốt** (hoặc số dòng đã nhập lại kho) ở bước vừa rồi.</summary>
    public int StockAffectedLines { get; set; }

    /// <summary>
    /// Cảnh báo **không chặn** (ví dụ: đơn xác nhận **không có** lượt giữ chỗ nào ⇒ **không** có bút toán kho).
    /// 📌 `null` = không có gì bất thường. UI hiện màu vàng — đây là chỗ *"im lặng làm người dùng tin sai"* hay xảy ra.
    /// </summary>
    public string? Warning { get; set; }

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

/// <summary>
/// 🆕 **`WP-05`** — huỷ đơn **theo 18 trạng thái Nhanh**: nêu rõ **AI huỷ** (`Khách huỷ`/`Hệ thống huỷ`/
/// `HVC huỷ`) + **lý do nghiệp vụ** (24 lý do).
///
/// 🚨 **VÌ SAO TÁCH KHỎI `ChangeFulfillmentStatusDto`:** huỷ cần quyền `Orders.Cancel`, đổi trạng thái chỉ cần
/// `Orders.StatusChange`. Cho huỷ đi qua cửa đổi-trạng-thái là **leo thang quyền im lặng** (người giao hàng
/// có thể huỷ đơn). ⇒ API có **hai** cửa, mỗi cửa **một** quyền — đúng `OrderStatusApiHopDongTests`.
/// </summary>
public class CancelOrderByStatusDto
{
    public Guid OrderId { get; set; }

    /// <summary>`CustomerCancelled` (63) · `SystemCancelled` (64) · `CarrierCancelled` (58).</summary>
    public FulfillmentStatus CancelledBy { get; set; }

    public long ExpectedVersion { get; set; }

    /// <summary>Mã **hành động** (chuỗi, ghi lịch sử) — ví dụ `KhachYeuCauHuy`.</summary>
    public string ReasonCode { get; set; } = default!;

    /// <summary>**Lý do nghiệp vụ** (enum 24 giá trị Nhanh) — nuôi báo cáo *"lý do huỷ"*.</summary>
    public OrderReasonCode? BusinessReason { get; set; }

    public string? ReasonText { get; set; }
}
