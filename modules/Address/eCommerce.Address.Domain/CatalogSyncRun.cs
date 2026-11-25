using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Address;

/// <summary>Đường nạp danh mục ĐVHC — `0` = job, `1` = import. Enum lưu DB chỉ **thêm ở cuối** (`D-17`).</summary>
public enum CatalogSyncTrigger
{
    /// <summary>Job định kỳ <c>AdministrativeUnitSyncWorker</c> (chỉ dev/test).</summary>
    Job = 0,

    /// <summary>Nạp qua <c>ImportDatasetAsync</c> (đường file/API).</summary>
    Import = 1
}

/// <summary>Kết cục một lượt nạp — `0` áp dụng, `1` bị hàng rào chặn, `2` lỗi. Chỉ **thêm ở cuối** (`D-17`).</summary>
public enum CatalogSyncOutcome
{
    /// <summary>Áp dụng thành công (kể cả lượt không có thay đổi: 0 + 0 + 0 + 0).</summary>
    DaApDung = 0,

    /// <summary>Bị hàng rào (`0601`…`0605`, `0008`) từ chối — **0** thay đổi được áp.</summary>
    BiChan = 1,

    /// <summary>Lỗi không mong đợi — **0** thay đổi được áp; chỉ lưu **loại** exception, cấm message/stack.</summary>
    Loi = 2
}

/// <summary>
/// **BẢN GHI LÔ NẠP DANH MỤC ĐVHC** (`D-78`, BR-009) — sổ **append-only**.
///
/// 🔒 BẤT BIẾN:
///   • `CreationAuditedAggregateRoot` (**không** `FullAudited`) ⇒ **không** `IsDeleted`, **không** UPDATE/DELETE;
///   • **0** public setter, **0** method đổi — chỉ có factory <see cref="Ghi"/>;
///   • mỗi lượt nạp (job **và** import, kể cả lượt **bị từ chối**) ghi **một** dòng;
///   • `SourceHash` do **server** tính trên danh mục chuẩn hoá (`D-78` §7.3) — **không** nhận từ client;
///   • `ErrorType` chỉ là `ex.GetType().FullName` — **cấm** `ex.Message`/stack/payload (BR-011).
/// </summary>
public class CatalogSyncRun : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public int SchemaVersion { get; private set; }

    public CatalogSyncTrigger Trigger { get; private set; }

    /// <summary>`danhMuc.Nguon`; `null` **chỉ khi** lỗi xảy ra trước khi biết nguồn (adapter ném trước khi trả danh mục).</summary>
    public string? SourceName { get; private set; }

    /// <summary>SHA-256 hex **thường** 64 ký tự của danh mục chuẩn hoá; `null` khi lỗi trước khi có danh mục.</summary>
    public string? SourceHash { get; private set; }

    /// <summary>Thời điểm **bắt đầu** — lấy **trước** khi gọi adapter.</summary>
    public DateTime StartedAtUtc { get; private set; }

    /// <summary>Thời điểm **kết thúc/commit** của lượt nạp (tên theo `D-78`; `ADDR-002` gọi `FinishedAtUtc`).</summary>
    public DateTime ImportedAtUtc { get; private set; }

    /// <summary>`job:AdministrativeUnitSyncWorker` hoặc `user:{id}`.</summary>
    public string ImportedBy { get; private set; } = default!;

    public CatalogSyncOutcome Outcome { get; private set; }

    /// <summary>Mã lỗi đã ném khi `BiChan`; `null` cho `DaApDung`/`Loi`.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>`ex.GetType().FullName` khi `Loi`; **cấm** message/stack/payload (BR-011).</summary>
    public string? ErrorType { get; private set; }

    public int SoThemMoi { get; private set; }

    public int SoDoiTen { get; private set; }

    public int SoMoLai { get; private set; }

    public int SoDong { get; private set; }

    protected CatalogSyncRun()
    {
    }

    public static CatalogSyncRun Ghi(
        int schemaVersion,
        CatalogSyncTrigger trigger,
        string? sourceName,
        string? sourceHash,
        DateTime startedAtUtc,
        DateTime importedAtUtc,
        string importedBy,
        CatalogSyncOutcome outcome,
        string? errorCode,
        string? errorType,
        int soThemMoi,
        int soDoiTen,
        int soMoLai,
        int soDong,
        Guid? tenantId = null,
        Guid? id = null)
    {
        // Phiên bản schema phải là 1/2 (ném 0005 nếu sai) — cùng nguồn sự thật với danh mục.
        AdministrativeSchemaVersion.TheoSo(schemaVersion);

        if (!Enum.IsDefined(trigger))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Trigger không hợp lệ.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Outcome không hợp lệ.");
        }

        importedBy = Check.NotNullOrWhiteSpace(importedBy, nameof(importedBy), maxLength: 128);

        if (sourceHash is not null)
        {
            Check.NotNullOrWhiteSpace(sourceHash, nameof(sourceHash), maxLength: 64);

            if (sourceHash.Length != 64 || !sourceHash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                throw new ArgumentException("SourceHash phải là SHA-256 hex thường 64 ký tự.", nameof(sourceHash));
            }
        }

        if (soThemMoi < 0 || soDoiTen < 0 || soMoLai < 0 || soDong < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(soThemMoi), "Số đếm của lượt nạp không được âm.");
        }

        return new CatalogSyncRun
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            SchemaVersion = schemaVersion,
            Trigger = trigger,
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim(),
            SourceHash = sourceHash,
            StartedAtUtc = ChuanHoaUtc(startedAtUtc),
            ImportedAtUtc = ChuanHoaUtc(importedAtUtc),
            ImportedBy = importedBy.Trim(),
            Outcome = outcome,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim(),
            ErrorType = string.IsNullOrWhiteSpace(errorType) ? null : errorType.Trim(),
            SoThemMoi = soThemMoi,
            SoDoiTen = soDoiTen,
            SoMoLai = soMoLai,
            SoDong = soDong
        };
    }

    /// <summary>Npgsql yêu cầu `DateTime` Kind=UTC cho cột `timestamptz`; chuẩn hoá thay vì để lỗi lúc ghi.</summary>
    private static DateTime ChuanHoaUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
