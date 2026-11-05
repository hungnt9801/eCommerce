using System.ComponentModel.DataAnnotations;
using eCommerce.Pos;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Application.Contracts.Pos;

/// <summary>
/// **Hoá đơn bán lẻ** — DTO đọc (DS + chi tiết dùng **chung** một kiểu: Nhanh cũng vậy, cột DS là
/// thuộc tính suy ra của cùng chứng từ; tách hai kiểu chỉ làm hai nơi lệch nhau).
/// 📌 Mọi số tiền **đã tính ở domain** (`TinhTienHoaDon`) — client **không** nhân lại.
/// </summary>
public class RetailBillDto
{
    public Guid Id { get; set; }

    public string Number { get; set; } = default!;

    /// <summary>Khoá idempotency của máy POS — trả về để client biết **đã** đồng bộ (offline).</summary>
    public Guid ClientBillId { get; set; }

    public Guid WarehouseId { get; set; }
    public Guid CashSessionId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SaleStaffId { get; set; }

    /// <summary>`Completed` / `Voided` (chuỗi — client không phải biết số enum).</summary>
    public string Status { get; set; } = default!;

    public decimal Subtotal { get; set; }
    public decimal LineDiscountTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal PointsUsed { get; set; }
    public decimal PointsUsedAmount { get; set; }
    public decimal PointsEarned { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal DebtAmount { get; set; }
    public decimal CustomerGivenAmount { get; set; }
    public DateTime? DueDateUtc { get; set; }

    public string? Note { get; set; }

    public Guid? StockDocumentId { get; set; }

    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? VoidStockDocumentId { get; set; }

    /// <summary>Ca **đã kết** ⇒ UI **ẩn nút Huỷ** (gọi sẽ nhận `0010`).</summary>
    public bool CashSessionClosed { get; set; }

    public DateTime CreationTime { get; set; }

    public List<RetailBillLineDto> Lines { get; set; } = new();

    public List<RetailBillPaymentDto> Payments { get; set; } = new();

    /// <summary>Các phương thức **đã dùng** (để DS hiện icon) — suy từ `Payments`, không lưu cột riêng.</summary>
    public List<string> PaymentMethods { get; set; } = new();
}

public class RetailBillLineDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = default!;
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? VatPercent { get; set; }
    public decimal Amount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal ReturnedQuantity { get; set; }
}

public class RetailBillPaymentDto
{
    public Guid Id { get; set; }

    /// <summary>`Cash` / `BankTransfer` / `Card` / `Points` / `Installment` / `Other`.</summary>
    public string Method { get; set; } = default!;

    public decimal Amount { get; set; }
    public string? ReferenceCode { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// **Tạo hoá đơn bán lẻ** (`POST /api/v1/pos/bills`).
///
/// 🚨 **`ClientBillId` là khoá idempotency** (`D-42`): máy POS sinh **một lần** khi mở tab và **giữ nguyên**
/// qua mọi lần retry (offline). Gửi lại cùng khoá ⇒ **200 + hoá đơn cũ**, không tạo hoá đơn thứ hai.
/// 📌 Client **KHÔNG** gửi `TotalPayment`/`Subtotal` — tiền do server tính (`TinhTienHoaDon`).
/// </summary>
public class CreateRetailBillDto
{
    /// <summary>UUID do **máy POS** sinh (`crypto.randomUUID()`), không đổi giữa các lần gửi.</summary>
    [Required]
    public Guid ClientBillId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>`null` = server tự lấy **ca đang mở** của kho (bình thường); gửi tường minh khi đồng bộ offline
    /// muộn — nhưng ca **phải** đang mở, nếu không ⇒ `0010`.</summary>
    public Guid? CashSessionId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? SaleStaffId { get; set; }

    /// <summary>Chiết khấu **tổng hoá đơn** (`F6`). Bị chặn (`0004`) nếu có dòng nào chiết khấu.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Số điểm tiêu — quy đổi theo `TinhTienHoaDon.TyLeDiemMacDinh` (1 điểm = 1.000 đ).</summary>
    public decimal PointsUsed { get; set; }

    /// <summary>Điểm **tích** của hoá đơn (WP-06 chỉ ghi nhận; cộng vào khách là `WP-07`).</summary>
    public decimal PointsEarned { get; set; }

    /// <summary>Tiền mặt khách **đưa** (để tính tiền thối + in bill) — không phải một dòng thanh toán.</summary>
    public decimal CustomerGivenAmount { get; set; }

    /// <summary>Hạn thanh toán — **bắt buộc** khi còn nợ (`0006`).</summary>
    public DateTime? DueDateUtc { get; set; }

    [StringLength(2048)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateRetailBillLineDto> Lines { get; set; } = new();

    public List<CreateRetailBillPaymentDto> Payments { get; set; } = new();
}

public class CreateRetailBillLineDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPrice { get; set; }

    /// <summary>Chiết khấu **theo dòng** (tiền). Dùng cái này **hoặc** `DiscountAmount` của hoá đơn.</summary>
    public decimal DiscountAmount { get; set; }

    public decimal? VatPercent { get; set; }

    /// <summary>Tên in lên bill (snapshot) — không bắt buộc.</summary>
    [StringLength(256)]
    public string? ProductName { get; set; }
}

public class CreateRetailBillPaymentDto
{
    public RetailPaymentMethod Method { get; set; } = RetailPaymentMethod.Cash;

    public decimal Amount { get; set; }

    [StringLength(128)]
    public string? ReferenceCode { get; set; }

    [StringLength(256)]
    public string? Note { get; set; }
}

/// <summary>Huỷ hoá đơn (`POST /pos/bills/{id}/void`) — **bắt buộc** lý do (audit + đối chiếu két).</summary>
public class VoidRetailBillDto
{
    [Required]
    [StringLength(512)]
    public string Reason { get; set; } = default!;
}

/// <summary>Kết quả huỷ — trả **cả** hoá đơn **và** chứng từ nhập lại (để UI in/đối chiếu được).</summary>
public class VoidRetailBillResultDto
{
    public RetailBillDto Bill { get; set; } = default!;

    /// <summary>`false` = hoá đơn **đã huỷ trước đó** (idempotent, không nhập kho lần hai).</summary>
    public bool Voided { get; set; }
}

/// <summary>Tham số lọc DS hoá đơn (màn #13).</summary>
public class RetailBillListInput : PagedResultRequestDto
{
    /// <summary>Tìm theo **số hoá đơn** hoặc mã tham chiếu thanh toán.</summary>
    public string? Q { get; set; }

    public Guid? WarehouseId { get; set; }
    public Guid? CashSessionId { get; set; }
    public Guid? CustomerId { get; set; }
    public RetailBillStatus? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// CA THU NGÂN
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

public class CashSessionDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public Guid? CashierId { get; set; }

    /// <summary>`Open` / `Closed`.</summary>
    public string Status { get; set; } = default!;

    public decimal OpeningCash { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedBy { get; set; }

    public decimal CashRevenue { get; set; }
    public decimal CashRefund { get; set; }
    public decimal OtherExpense { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal HandoverCash { get; set; }
    public decimal Variance { get; set; }

    /// <summary>Số hoá đơn trong ca (đếm **sống** — xem chú thích ở app service).</summary>
    public int BillCount { get; set; }

    public string? Note { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>Mở ca (`POST /pos/sessions/open`). 📌 Một quầy **một** ca mở ⇒ mở lần hai nhận `0012`.</summary>
public class OpenCashSessionDto
{
    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>Tiền đầu ca (đếm mệnh giá hoặc nhập tổng — Nhanh cho cả hai).</summary>
    public decimal OpeningCash { get; set; }
}

/// <summary>Kết ca (`POST /pos/sessions/{id}/close`).</summary>
public class CloseCashSessionDto
{
    /// <summary>**Tiền bàn giao thực tế** — thu ngân đếm két.</summary>
    public decimal HandoverCash { get; set; }

    /// <summary>Chi phí khác **bằng tiền mặt** trong ca (điện nước, ship nội bộ…).</summary>
    public decimal OtherExpense { get; set; }

    [StringLength(1024)]
    public string? Note { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// TRẢ HÀNG
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

public class RetailReturnDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public Guid RetailBillId { get; set; }
    public string RetailBillNumber { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public Guid? CashSessionId { get; set; }
    public Guid? CustomerId { get; set; }

    /// <summary>`Posted` / `Voided`.</summary>
    public string Status { get; set; } = default!;

    public decimal GoodsAmount { get; set; }
    public decimal ReturnFeeAmount { get; set; }
    public decimal DebtReductionAmount { get; set; }
    public decimal RefundAmount { get; set; }

    /// <summary>`Cash` / `BankTransfer` / …</summary>
    public string RefundMethod { get; set; } = default!;

    public decimal PointsReversed { get; set; }
    public string? Note { get; set; }
    public Guid? StockDocumentId { get; set; }
    public DateTime CreationTime { get; set; }

    public List<RetailReturnLineDto> Lines { get; set; } = new();
}

public class RetailReturnLineDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public decimal RefundAmount { get; set; }
}

/// <summary>Tạo phiếu trả (`POST /pos/returns`) — **chỉ trả từ hoá đơn** (`D-42`).</summary>
public class CreateRetailReturnDto
{
    [Required]
    public Guid RetailBillId { get; set; }

    public decimal ReturnFeeAmount { get; set; }

    public RetailPaymentMethod RefundMethod { get; set; } = RetailPaymentMethod.Cash;

    public decimal PointsReversed { get; set; }

    [StringLength(1024)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateRetailReturnLineDto> Lines { get; set; } = new();
}

public class CreateRetailReturnLineDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    public decimal Quantity { get; set; }
}

/// <summary>Tham số lọc DS phiếu trả.</summary>
public class RetailReturnListInput : PagedResultRequestDto
{
    public Guid? RetailBillId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
