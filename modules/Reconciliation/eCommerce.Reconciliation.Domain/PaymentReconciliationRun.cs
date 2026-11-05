using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reconciliation;

/// <summary>
/// **MỘT LƯỢT ĐỐI SOÁT THANH TOÁN** (`RecPaymentReconciliationRuns`) — `REC-001` §5.2.1.
///
/// 🚨 **MÁY TRẠNG THÁI CÓ GUARD (quy ước #11):** chỉ `DangChay` mới được đóng; đóng lần hai hoặc đóng lượt
/// đã `ThatBai` **NÉM** `Recon:0007` — không im lặng ghi đè. Lý do: số đếm (`LineTotal`/`DiscrepancyCount`)
/// là **bằng chứng tài chính**; cho phép ghi đè nghĩa là báo cáo cũ có thể bị sửa mà không ai biết.
///
/// 🔒 **BẤT BIẾN:** `PeriodFromUtc &lt; PeriodToUtc`; `LineMatched + LineSkipped &lt;= LineTotal`; số lệch
/// quyết định `HoanTat` hay `HoanTatCoLech` — **không** do người gọi tự chọn.
/// </summary>
public class PaymentReconciliationRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiFailureCodeToiDa = 64;
    public const int DoDaiCorrelationIdToiDa = 128;

    public Guid? TenantId { get; protected set; }

    public PaymentReconciliationRunStatus Status { get; protected set; }

    public PaymentReconciliationTrigger TriggeredBy { get; protected set; }

    /// <summary>`NULL` khi do nhịp job; có giá trị khi chạy tay.</summary>
    public Guid? TriggeredByUserId { get; protected set; }

    public DateTime PeriodFromUtc { get; protected set; }

    public DateTime PeriodToUtc { get; protected set; }

    public DateTime StartedAtUtc { get; protected set; }

    /// <summary>`NULL` khi `DangChay` (bất biến: lượt đã đóng **phải** có mốc kết thúc).</summary>
    public DateTime? FinishedAtUtc { get; protected set; }

    public int LineTotal { get; protected set; }

    public int LineMatched { get; protected set; }

    public int LineSkipped { get; protected set; }

    public int DiscrepancyCount { get; protected set; }

    /// <summary>Mã lỗi **ĐÓNG** khi `ThatBai` (`InvalidCredential`, `StaleRun`… — `REC-001` §5.4).</summary>
    public string? FailureCode { get; protected set; }

    public string? CorrelationId { get; protected set; }

    protected PaymentReconciliationRun()
    {
    }

    public PaymentReconciliationRun(
        Guid id,
        DateTime periodFromUtc,
        DateTime periodToUtc,
        PaymentReconciliationTrigger triggeredBy,
        Guid? triggeredByUserId = null,
        string? correlationId = null) : base(id)
    {
        if (periodFromUtc >= periodToUtc)
        {
            throw new BusinessException(ReconErrorCodes.LuotDaDong)
                .WithData("lyDo", "PeriodFromUtc phải NHỎ HƠN PeriodToUtc.");
        }

        PeriodFromUtc = periodFromUtc;
        PeriodToUtc = periodToUtc;
        TriggeredBy = triggeredBy;
        TriggeredByUserId = triggeredByUserId;
        Status = PaymentReconciliationRunStatus.DangChay;
        StartedAtUtc = DateTime.UtcNow;
        CorrelationId = Cat(correlationId, DoDaiCorrelationIdToiDa);
    }

    /// <summary>
    /// Đóng lượt. 🔒 Trạng thái **suy ra** từ `discrepancyCount` — người gọi **không** được chọn
    /// `HoanTat` khi còn lệch (cùng khuôn `InventoryReconciliationService`).
    /// </summary>
    public void KetThuc(int lineTotal, int lineMatched, int lineSkipped, int discrepancyCount)
    {
        KiemTraDangChay();

        if (lineTotal < 0 || lineMatched < 0 || lineSkipped < 0 || discrepancyCount < 0
            || lineMatched + lineSkipped > lineTotal)
        {
            throw new BusinessException(ReconErrorCodes.LuotDaDong)
                .WithData("lyDo", "Số đếm không hợp lệ (âm hoặc matched + skipped > total).")
                .WithData("lineTotal", lineTotal)
                .WithData("lineMatched", lineMatched)
                .WithData("lineSkipped", lineSkipped);
        }

        LineTotal = lineTotal;
        LineMatched = lineMatched;
        LineSkipped = lineSkipped;
        DiscrepancyCount = discrepancyCount;
        FinishedAtUtc = DateTime.UtcNow;
        Status = discrepancyCount > 0
            ? PaymentReconciliationRunStatus.HoanTatCoLech
            : PaymentReconciliationRunStatus.HoanTat;
    }

    /// <summary>Đánh dấu lượt **thất bại** (lỗi cấu hình/cổng). 🔒 Không đóng lượt đã đóng.</summary>
    public void ThatBai(string failureCode)
    {
        KiemTraDangChay();

        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new BusinessException(ReconErrorCodes.LuotDaDong)
                .WithData("lyDo", "ThatBai bắt buộc có FailureCode.");
        }

        FailureCode = Cat(failureCode, DoDaiFailureCodeToiDa);
        FinishedAtUtc = DateTime.UtcNow;
        Status = PaymentReconciliationRunStatus.ThatBai;
    }

    private void KiemTraDangChay()
    {
        if (Status != PaymentReconciliationRunStatus.DangChay)
        {
            throw new BusinessException(ReconErrorCodes.LuotDaDong)
                .WithData("trangThai", Status.ToString())
                .WithData("lyDo", "Chỉ lượt DangChay mới được đóng/lỗi.");
        }
    }

    private static string Cat(string? giaTri, int toiDa)
    {
        var s = (giaTri ?? string.Empty).Trim();
        return s.Length <= toiDa ? s : s[..toiDa];
    }
}
