using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **MỘT DÒNG LỆCH SỐ PHẢI LỘ RA** (`RepConsistencyChecks`) — `REP-001` BR-012.
///
/// 🔑 **VÌ SAO CẦN:** job thay thế theo ngày nên số khớp **tại thời điểm tổng hợp**; khi nguồn đổi **sau**
/// đó, lệch số phải **LỘ RA** — không được sửa tay (sửa = `Rebuild` có audit). Unique **partial**
/// (`WHERE "ResolvedAtUtc" IS NULL`) chống trùng dòng lệch **đang mở** khi đối chiếu chạy lặp mỗi nhịp.
///
/// 🚨 **So tiền bằng `Delta`** (`decimal.Compare(delta, 0m) != 0`), **CẤM** `==` (`XC-001` BR-003).
/// `MismatchKind = SuKienTre` **không có số** ⇒ `ExpectedValue`/`ActualValue`/`Delta` đều NULL.
/// </summary>
public class RepConsistencyCheck : CreationAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiFactKeyToiDa = 128;

    public Guid? TenantId { get; protected set; }

    public DateOnly FactDate { get; protected set; }

    public RepProjection Projection { get; protected set; }

    public RepMismatchKind MismatchKind { get; protected set; }

    /// <summary>Khoá tự nhiên của đối tượng lệch (vd `0:2026-09-15`) — dùng để chống trùng dòng mở.</summary>
    public string FactKey { get; protected set; } = default!;

    /// <summary>Số **nguồn** (expected). NULL khi loại lệch không có số (sự kiện trễ).</summary>
    public decimal? ExpectedValue { get; protected set; }

    /// <summary>Số **read-model** (actual). NULL khi loại lệch không có số.</summary>
    public decimal? ActualValue { get; protected set; }

    /// <summary>`Actual − Expected`; NULL khi một vế NULL.</summary>
    public decimal? Delta { get; protected set; }

    /// <summary>WP-26 **luôn** `false` — sửa lệch chỉ qua `Rebuild` có audit (cột để dành).</summary>
    public bool AutoCorrected { get; protected set; }

    /// <summary>Tham chiếu **mềm** tới `RepRunLogs.Id` (0 FK — bảng audit append-only).</summary>
    public Guid? DetectedByRunId { get; protected set; }

    public DateTime DetectedAtUtc { get; protected set; }

    public DateTime? ResolvedAtUtc { get; protected set; }

    public Guid? ResolvedBy { get; protected set; }

    protected RepConsistencyCheck() { }

    /// <summary>
    /// **MỘT CỬA DUY NHẤT** tạo dòng lệch. `Delta = Actual − Expected` khi **cả hai** vế có số; ngược lại NULL.
    /// </summary>
    public static RepConsistencyCheck GhiNhan(
        Guid id,
        DateOnly ngay,
        RepProjection projection,
        RepMismatchKind kind,
        string factKey,
        decimal? expected,
        decimal? actual,
        DateTime detectedAtUtc,
        Guid? detectedByRunId = null)
    {
        if (!Enum.IsDefined(projection))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("projection", projection);
        }

        if (!Enum.IsDefined(kind))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("kind", kind);
        }

        var khoa = factKey?.Trim() ?? string.Empty;

        if (khoa.Length == 0)
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("lyDo", "FactKey không được rỗng.");
        }

        if (khoa.Length > DoDaiFactKeyToiDa)
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe)
                .WithData("truong", nameof(factKey))
                .WithData("toiDa", DoDaiFactKeyToiDa);
        }

        // 📌 Sự kiện trễ KHÔNG có số — ép cả hai vế NULL để CHECK `Delta` ở DB không bao giờ mâu thuẫn.
        if (kind == RepMismatchKind.SuKienTre)
        {
            expected = null;
            actual = null;
        }

        return new RepConsistencyCheck
        {
            Id = id,
            FactDate = ngay,
            Projection = projection,
            MismatchKind = kind,
            FactKey = khoa,
            ExpectedValue = expected,
            ActualValue = actual,
            Delta = expected.HasValue && actual.HasValue ? actual.Value - expected.Value : null,
            AutoCorrected = false,
            DetectedByRunId = detectedByRunId,
            DetectedAtUtc = detectedAtUtc
        };
    }

    /// <summary>Đánh dấu đã xử lý — **idempotent**: đã xử lý thì giữ nguyên dấu thời gian/người.</summary>
    public void DanhDauDaXuLy(Guid by, DateTime lucUtc)
    {
        if (ResolvedAtUtc is not null)
        {
            return;
        }

        ResolvedBy = by;
        ResolvedAtUtc = lucUtc;
    }
}
