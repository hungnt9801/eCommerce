using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **NHẬT KÝ MỘT LƯỢT CHẠY + CHECKPOINT** (`RepRunLogs`) — `REP-001` §3.2, `D-271`.
///
/// 🔑 **VÌ SAO CẦN (không đủ log tệp):** rebuild một năm bị đứt ở tháng thứ 7 phải **chạy tiếp** từ
/// `CheckpointDate + 1` mà **không** tính lại 6 tháng đầu (`NFR-009`/`TC-10`). Log tệp không phải trạng
/// thái bền — nó mất khi xoay vòng log, và không ai query được *"lượt chạy nào đang Queued"*.
///
/// 🚨 **MÁY TRẠNG THÁI NÉM LỖI** ở **mọi** nhánh sai (`ReportingErrorCodes.ChuyenTrangThaiKhongHopLe`
/// = `0015`) — không có *"bỏ qua im lặng"*. `CheckpointDate` = ngày **cuối đã COMMIT**; chạy tiếp bắt
/// đầu ở `CheckpointDate + 1`.
/// </summary>
public class RepRunLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Tên job đã đóng — CHECK `IN` ở DB. Người dùng không truyền tự do.</summary>
    public static class TenJob
    {
        public const string Rebuild = "Rebuild";
        public const string Reconcile = "Reconcile";
        public const string Aggregate = "Aggregate";
    }

    /// <summary>Lý do cố định cho dòng do **hệ thống** sinh — luôn ≥ 10 ký tự để thoả CHECK `Reason`.</summary>
    public static class LyDoHeThong
    {
        public const string DoiChieu = "Đối chiếu theo yêu cầu";
        public const string TongHopDinhKy = "Tổng hợp định kỳ";
    }

    public Guid? TenantId { get; protected set; }

    public string JobName { get; protected set; } = default!;

    public RepProjection Projection { get; protected set; }

    public RepRunStatus Status { get; protected set; }

    public DateOnly FromDate { get; protected set; }

    public DateOnly ToDate { get; protected set; }

    public int WindowsTotal { get; protected set; }

    public int WindowsDone { get; protected set; }

    /// <summary>Ngày **cuối đã COMMIT** của lượt chạy; `null` = chưa commit cửa sổ nào.</summary>
    public DateOnly? CheckpointDate { get; protected set; }

    public long RowsWritten { get; protected set; }

    public string Reason { get; protected set; } = default!;

    public Guid? RequestedBy { get; protected set; }

    public DateTime? StartedAtUtc { get; protected set; }

    public DateTime? FinishedAtUtc { get; protected set; }

    public string? HostName { get; protected set; }

    public string? CorrelationId { get; protected set; }

    public string? LastErrorCode { get; protected set; }

    /// <summary>
    /// Khoá idempotency — **NOT NULL kể cả dòng hệ thống** (`REP-001` §5.6). Dòng rebuild do người dùng
    /// dùng khoá theo (khoảng + lý do); dòng `Reconcile`/`Aggregate` dùng khoá **duy nhất theo lượt**
    /// (`…:{runId}`) ⇒ chạy lại cùng khoảng ngày **không bao giờ** `unique_violation`.
    /// </summary>
    public string IdempotencyKey { get; protected set; } = default!;

    protected RepRunLog() { }

    public RepRunLog(
        Guid id,
        string jobName,
        RepProjection projection,
        DateOnly fromDate,
        DateOnly toDate,
        int windowsTotal,
        string reason,
        Guid? requestedBy,
        string idempotencyKey) : base(id)
    {
        if (jobName is not (TenJob.Rebuild or TenJob.Reconcile or TenJob.Aggregate))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("jobName", jobName)
                .WithData("lyDo", "JobName chỉ nhận Rebuild/Reconcile/Aggregate.");
        }

        if (!Enum.IsDefined(projection))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("projection", projection);
        }

        if (toDate < fromDate)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe)
                .WithData("tu", fromDate)
                .WithData("den", toDate);
        }

        if (windowsTotal < 0)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe).WithData("windowsTotal", windowsTotal);
        }

        var lyDo = reason?.Trim() ?? string.Empty;

        if (lyDo.Length < RepPeriodClose.NguongLyDoToiThieu)
        {
            throw new BusinessException(ReportingErrorCodes.ThieuLyDo)
                .WithData("doDai", lyDo.Length)
                .WithData("toiThieu", RepPeriodClose.NguongLyDoToiThieu);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("lyDo", "Thiếu IdempotencyKey.");
        }

        JobName = jobName;
        Projection = projection;
        Status = RepRunStatus.Queued;
        FromDate = fromDate;
        ToDate = toDate;
        WindowsTotal = windowsTotal;
        Reason = lyDo;
        RequestedBy = requestedBy;
        IdempotencyKey = idempotencyKey.Trim();
    }

    /// <summary>`Queued → Running`. Ghi `StartedAtUtc`, `HostName`.</summary>
    public void BatDau(DateTime lucUtc, string? hostName)
    {
        if (Status != RepRunStatus.Queued)
        {
            throw ChuyenSai($"BatDau yêu cầu Queued (đang {Status}).");
        }

        Status = RepRunStatus.Running;
        StartedAtUtc = lucUtc;
        HostName = hostName;
    }

    /// <summary>
    /// Ghi một cửa sổ ngày **đã commit**. `CheckpointDate IS NULL ⇒ ngay == FromDate` (cửa sổ đầu);
    /// ngược lại `ngay > CheckpointDate` (không lùi, không nhảy cóc).
    /// </summary>
    public void GhiCuaSo(DateOnly ngay, long soDong)
    {
        if (Status != RepRunStatus.Running)
        {
            throw ChuyenSai($"GhiCuaSo yêu cầu Running (đang {Status}).");
        }

        if (soDong < 0)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe).WithData("soDong", soDong);
        }

        if (ngay < FromDate || ngay > ToDate)
        {
            throw ChuyenSai($"Ngày {ngay} ngoài khoảng [{FromDate}, {ToDate}].");
        }

        if (CheckpointDate is null)
        {
            if (ngay != FromDate)
            {
                throw ChuyenSai($"Cửa sổ đầu phải là {FromDate} (nhận {ngay}).");
            }
        }
        else if (ngay <= CheckpointDate.Value)
        {
            throw ChuyenSai($"Ngày {ngay} phải SAU checkpoint {CheckpointDate}.");
        }

        if (WindowsDone >= WindowsTotal)
        {
            throw ChuyenSai("Không thể ghi thêm cửa sổ — đã đủ WindowsTotal.");
        }

        WindowsDone++;
        CheckpointDate = ngay;
        RowsWritten += soDong;
    }

    /// <summary>`Running → Succeeded`. Yêu cầu `WindowsDone == WindowsTotal`.</summary>
    public void HoanTat(DateTime lucUtc)
    {
        if (Status != RepRunStatus.Running)
        {
            throw ChuyenSai($"HoanTat yêu cầu Running (đang {Status}).");
        }

        if (WindowsDone != WindowsTotal)
        {
            throw ChuyenSai($"HoanTat nhưng WindowsDone={WindowsDone} ≠ WindowsTotal={WindowsTotal}.");
        }

        Status = RepRunStatus.Succeeded;
        FinishedAtUtc = lucUtc;
    }

    /// <summary>`Running → PartiallySucceeded`. Giữ `CheckpointDate` để chạy tiếp.</summary>
    public void MotPhan(string maLoi)
    {
        if (Status != RepRunStatus.Running)
        {
            throw ChuyenSai($"MotPhan yêu cầu Running (đang {Status}).");
        }

        if (WindowsDone >= WindowsTotal)
        {
            throw ChuyenSai("MotPhan nhưng đã chạy đủ cửa sổ (phải là HoanTat).");
        }

        Status = RepRunStatus.PartiallySucceeded;
        LastErrorCode = maLoi;
    }

    /// <summary>`Running → Failed`.</summary>
    public void ThatBai(string maLoi, DateTime lucUtc)
    {
        if (Status != RepRunStatus.Running)
        {
            throw ChuyenSai($"ThatBai yêu cầu Running (đang {Status}).");
        }

        Status = RepRunStatus.Failed;
        LastErrorCode = maLoi;
        FinishedAtUtc = lucUtc;
    }

    /// <summary>`PartiallySucceeded/Failed → Queued`. Không có `CheckpointDate` ⇒ NÉM `0017`.</summary>
    public void YeuCauChayLai()
    {
        if (Status is not (RepRunStatus.PartiallySucceeded or RepRunStatus.Failed))
        {
            throw ChuyenSai($"YeuCauChayLai yêu cầu PartiallySucceeded/Failed (đang {Status}).");
        }

        if (CheckpointDate is null)
        {
            throw new BusinessException(ReportingErrorCodes.CheckpointKhongCo)
                .WithData("runId", Id)
                .WithData("lyDo", "Không có checkpoint ⇒ phải Rebuild tường minh.");
        }

        Status = RepRunStatus.Queued;
        // 📌 Giữ `LastErrorCode` để đối chiếu lịch sử; giữ `FinishedAtUtc` cũ cho audit.
    }

    private static BusinessException ChuyenSai(string chiTiet)
        => new BusinessException(ReportingErrorCodes.ChuyenTrangThaiKhongHopLe).WithData("chiTiet", chiTiet);
}
