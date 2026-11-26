using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reporting;

/// <summary>
/// **CHỐT KỲ KẾ TOÁN** (`RepPeriodCloses`) — `REP-001` §3.4, `D8` + `D-171`.
///
/// 🔑 **VÌ SAO CẦN:** `XC-006` BR-008 yêu cầu xuất Mẫu 01 khớp dữ liệu **đã phát hành trong kỳ**. Không
/// chốt được thì lần xuất sau có thể khác lần trước (sự kiện đến muộn / rebuild) ⇒ kế toán không biết tin
/// bản nào. 🔒 Ràng buộc `EXCLUDE USING gist` chỉ chặn **hai kỳ `DaChot` chồng nhau** (`Status = 2`).
///
/// 🚨 **Mọi chuyển sai NÉM `ChuyenTrangThaiKhongHopLe` = `0015`.** Ngưỡng lý do (`D-274`): đóng kỳ **≥ 10**,
/// mở lại **≥ 20**; thiếu ⇒ `ThieuLyDo` = `0016`.
/// </summary>
public class RepPeriodClose : CreationAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>Ngưỡng lý do tối thiểu khi **đóng kỳ** (`D-274`).</summary>
    public const int NguongLyDoToiThieu = 10;

    /// <summary>Ngưỡng lý do tối thiểu khi **mở lại kỳ** (`D-274`).</summary>
    public const int NguongLyDoMoLaiToiThieu = 20;

    public Guid? TenantId { get; protected set; }

    public DateOnly FromDate { get; protected set; }

    public DateOnly ToDate { get; protected set; }

    public RepPeriodStatus Status { get; protected set; }

    public string? SnapshotHash { get; protected set; }

    public string CloseReason { get; protected set; } = default!;

    public Guid? ClosedBy { get; protected set; }

    public DateTime? ClosedAtUtc { get; protected set; }

    public Guid? ReopenedBy { get; protected set; }

    public string? ReopenReason { get; protected set; }

    public DateTime? ReopenedAtUtc { get; protected set; }

    public string IdempotencyKey { get; protected set; } = default!;

    protected RepPeriodClose() { }

    public RepPeriodClose(Guid id, DateOnly fromDate, DateOnly toDate, string closeReason, string idempotencyKey)
        : base(id)
    {
        KhoangChot.Kiem(fromDate, toDate);

        var lyDo = closeReason?.Trim() ?? string.Empty;

        if (lyDo.Length < NguongLyDoToiThieu)
        {
            throw new BusinessException(ReportingErrorCodes.ThieuLyDo)
                .WithData("doDai", lyDo.Length)
                .WithData("toiThieu", NguongLyDoToiThieu);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessException(ReportingErrorCodes.NguonKhongHopLe).WithData("lyDo", "Thiếu IdempotencyKey.");
        }

        FromDate = fromDate;
        ToDate = toDate;
        Status = RepPeriodStatus.Mo;
        CloseReason = lyDo;
        IdempotencyKey = idempotencyKey.Trim();
    }

    /// <summary>`Mo/DaMoLai → DangChot`. `lyDo` ≥ 10 ký tự (sau `Trim`), thiếu ⇒ `0016`.</summary>
    public void BatDauChot(Guid? by, string lyDo)
    {
        if (Status is not (RepPeriodStatus.Mo or RepPeriodStatus.DaMoLai))
        {
            throw ChuyenSai($"BatDauChot yêu cầu Mo/DaMoLai (đang {Status}).");
        }

        var lyDoSach = lyDo?.Trim() ?? string.Empty;

        if (lyDoSach.Length < NguongLyDoToiThieu)
        {
            throw new BusinessException(ReportingErrorCodes.ThieuLyDo)
                .WithData("doDai", lyDoSach.Length)
                .WithData("toiThieu", NguongLyDoToiThieu);
        }

        _ = by; // 📌 `ClosedBy` chỉ ghi khi CHỐT (giữ `by` ở chữ ký theo hợp đồng §3.4).
        CloseReason = lyDoSach;
        Status = RepPeriodStatus.DangChot;
    }

    /// <summary>`DangChot → DaChot`. `snapshotHash` phải là 64 hex.</summary>
    public void Chot(string snapshotHash, Guid? by, DateTime lucUtc)
    {
        if (Status != RepPeriodStatus.DangChot)
        {
            throw ChuyenSai($"Chot yêu cầu DangChot (đang {Status}).");
        }

        if (!LaHex64(snapshotHash))
        {
            throw ChuyenSai("SnapshotHash phải là 64 ký tự hex.");
        }

        SnapshotHash = snapshotHash;
        ClosedBy = by;
        ClosedAtUtc = lucUtc;
        Status = RepPeriodStatus.DaChot;
    }

    /// <summary>`DaChot → DaMoLai`. `lyDo` ≥ 20 ký tự, thiếu ⇒ `0016`. Giữ audit đóng kỳ.</summary>
    public void MoLai(Guid? by, string lyDo, DateTime lucUtc)
    {
        if (Status != RepPeriodStatus.DaChot)
        {
            throw ChuyenSai($"MoLai yêu cầu DaChot (đang {Status}).");
        }

        var lyDoSach = lyDo?.Trim() ?? string.Empty;

        if (lyDoSach.Length < NguongLyDoMoLaiToiThieu)
        {
            throw new BusinessException(ReportingErrorCodes.ThieuLyDo)
                .WithData("doDai", lyDoSach.Length)
                .WithData("toiThieu", NguongLyDoMoLaiToiThieu);
        }

        Status = RepPeriodStatus.DaMoLai;
        ReopenedBy = by;
        ReopenReason = lyDoSach;
        ReopenedAtUtc = lucUtc;
        // 📌 GIỮ `ClosedBy`/`ClosedAtUtc`/`CloseReason`/`SnapshotHash` để audit ai đóng, vì sao, bản nào.
    }

    private static bool LaHex64(string? s)
    {
        if (s is null || s.Length != 64)
        {
            return false;
        }

        foreach (var c in s)
        {
            if (!Uri.IsHexDigit(c) || char.IsUpper(c))
            {
                return false;
            }
        }

        return true;
    }

    private static BusinessException ChuyenSai(string chiTiet)
        => new BusinessException(ReportingErrorCodes.ChuyenTrangThaiKhongHopLe).WithData("chiTiet", chiTiet);
}
