namespace eCommerce.Reporting;

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// 🆕 `WP-26` — HỢP ĐỒNG VẬN HÀNH GĐ3 (`REP-001` §4.1). DTO **thuần**, map tay, enum trả **số**, 0 PII.
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Đầu vào **rebuild** một khoảng — `Reason` bắt buộc ≥ 10 ký tự (`D-274`).</summary>
public class StartRebuildInput
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public RepProjection Projection { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Đầu vào **chốt kỳ** — `Reason` ≥ 10 ký tự.</summary>
public class ClosePeriodInput
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Đầu vào **mở lại kỳ** — `Reason` ≥ 20 ký tự.</summary>
public class ReopenPeriodInput
{
    public Guid PeriodId { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Đầu vào **đối chiếu**. `Projection = null` ⇒ chạy cả hai (mỗi phép một dòng `RepRunLog`).</summary>
public class RunReconciliationInput
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public RepProjection? Projection { get; set; }
}

/// <summary>Lọc + phân trang danh sách lượt chạy.</summary>
public class GetLanChayInput
{
    public int SkipCount { get; set; }

    public int MaxResultCount { get; set; } = 20;

    public RepProjection? Projection { get; set; }

    public RepRunStatus? Status { get; set; }
}

/// <summary>Lọc + phân trang danh sách kỳ chốt.</summary>
public class GetKyChotInput
{
    public int SkipCount { get; set; }

    public int MaxResultCount { get; set; } = 20;

    public RepPeriodStatus? Status { get; set; }
}

/// <summary>Lọc + phân trang danh sách dòng lệch.</summary>
public class GetLechSoInput
{
    public int SkipCount { get; set; }

    public int MaxResultCount { get; set; } = 20;

    public RepProjection? Projection { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    /// <summary>`true` = chỉ dòng đã xử lý; `false` = chỉ dòng đang mở; `null` = cả hai.</summary>
    public bool? DaXuLy { get; set; }
}

/// <summary>Một lượt chạy (nhật ký + checkpoint).</summary>
public class LanChayDto
{
    public Guid Id { get; set; }

    public string JobName { get; set; } = string.Empty;

    public RepProjection Projection { get; set; }

    public RepRunStatus Status { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public int WindowsTotal { get; set; }

    public int WindowsDone { get; set; }

    public DateOnly? CheckpointDate { get; set; }

    public long RowsWritten { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? RequestedBy { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public string? HostName { get; set; }

    public string? LastErrorCode { get; set; }

    /// <summary>Gọi lại **cùng khoảng + cùng lý do** ⇒ `true` (trả bản ghi cũ, không tạo lần hai). Không phải lỗi.</summary>
    public bool AlreadyRequested { get; set; }
}

/// <summary>Trang lượt chạy.</summary>
public class LanChayPageDto
{
    public long TotalCount { get; set; }

    public List<LanChayDto> Items { get; set; } = new();
}

/// <summary>Một kỳ chốt.</summary>
public class KyChotDto
{
    public Guid Id { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public RepPeriodStatus Status { get; set; }

    public string? SnapshotHash { get; set; }

    public Guid? ClosedBy { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string CloseReason { get; set; } = string.Empty;

    public Guid? ReopenedBy { get; set; }

    public string? ReopenReason { get; set; }

    public DateTime? ReopenedAtUtc { get; set; }

    /// <summary>Gọi lại cùng khoảng khi đã `DaChot` ⇒ `true`.</summary>
    public bool AlreadyClosed { get; set; }
}

/// <summary>Trang kỳ chốt.</summary>
public class KyChotPageDto
{
    public long TotalCount { get; set; }

    public List<KyChotDto> Items { get; set; } = new();
}

/// <summary>Một dòng lệch số.</summary>
public class LechSoDto
{
    public Guid Id { get; set; }

    public DateOnly FactDate { get; set; }

    public RepProjection Projection { get; set; }

    public RepMismatchKind MismatchKind { get; set; }

    public string FactKey { get; set; } = string.Empty;

    public decimal? ExpectedValue { get; set; }

    public decimal? ActualValue { get; set; }

    public decimal? Delta { get; set; }

    public bool AutoCorrected { get; set; }

    public DateTime DetectedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
}

/// <summary>Trang dòng lệch.</summary>
public class LechSoPageDto
{
    public long TotalCount { get; set; }

    public List<LechSoDto> Items { get; set; } = new();
}

/// <summary>Kết quả một lượt đối chiếu.</summary>
public class KetQuaDoiChieuDto
{
    public int SoNgayDaSo { get; set; }

    public int SoDongLech { get; set; }

    public int SoDongLechMoi { get; set; }
}
