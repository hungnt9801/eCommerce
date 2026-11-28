using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — TRẠNG THÁI MỘT LẦN CHẠY ĐỘNG CƠ GIÁ VỐN** (`PrcCostEngineRuns.TrangThai`).
///
/// 🚨 **Enum lưu DB dạng SỐ** ⇒ **CHỈ THÊM Ở CUỐI DẢI, KHÔNG ĐỔI SỐ ĐÃ DÙNG** (`D-17`).
/// </summary>
public enum TrangThaiChayDongCo
{
    /// <summary>Đang chạy (hoặc bị bỏ dở) — lượt mới cùng `RunKey` ⇒ **409/bận**.</summary>
    DangChay = 0,

    /// <summary>Chạy xong — lượt mới cùng `RunKey` ⇒ **trả run cũ** (idempotent — `K06-08`).</summary>
    ThanhCong = 1,

    /// <summary>Lỗi — lượt mới cùng `RunKey` ⇒ **retry attempt mới trên CÙNG dòng** (`K06-08b`).</summary>
    Loi = 2
}

/// <summary>
/// 🆕 **`W17-2` — NHẬT KÝ MỘT LẦN CHẠY ĐỘNG CƠ GIÁ VỐN** (bảng `PrcCostEngineRuns`; `PRC-001-arch` §4.1 **S1**).
///
/// 🔑 **VÌ SAO CẦN:** `XC-002` (`IIdempotencyGuard`/`InboxRecords`) **chưa có** ⇒ `W17-2` dùng **bảng run** làm
/// hàng rào idempotency cho job/command: cùng `(TenantId, RunKey)` **không** chạy hai lần; trạng thái quyết định
/// *trả run cũ* / *409 bận* / *retry attempt mới* (`§5.6`). Khi `XC-002` xong, thay lớp khoá trong suốt, 0 schema.
///
/// 📌 **Audit ABP:** kế thừa `FullAuditedAggregateRoot` để có `CreationTime/CreatorId/...` (theo khuôn entity
/// sổ/audit hiện có). 🚨 **Không** là sổ cái append-only: một dòng **được UPDATE** khi retry/hoàn tất (đó là
/// mục đích của `TrangThai`/`Attempt`) — khác hẳn sổ cái kho (chỉ ghi thêm).
/// </summary>
public class CostEngineRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Khoá chống trùng: `cost:{tenant}:{from}:{to}:{trigger}` (§5.6).</summary>
    public string RunKey { get; protected set; } = default!;

    public DateOnly FromDate { get; protected set; }

    public DateOnly ToDate { get; protected set; }

    /// <summary>Nguồn kích hoạt: `Schedule` (worker) / `Manual` (command).</summary>
    public string Trigger { get; protected set; } = default!;

    public TrangThaiChayDongCo TrangThai { get; protected set; } = TrangThaiChayDongCo.DangChay;

    /// <summary>Số lần thử — bắt đầu `1`, tăng khi retry một lượt `Loi`.</summary>
    public int Attempt { get; protected set; }

    public DateTime StartedAtUtc { get; protected set; }

    public DateTime? FinishedAtUtc { get; protected set; }

    public int RowsUpserted { get; protected set; }

    public int RowsSkippedApproved { get; protected set; }

    public int RowsFlaggedMissing { get; protected set; }

    public string? ErrorMessage { get; protected set; }

    protected CostEngineRun()
    {
    }

    public CostEngineRun(
        Guid id,
        string runKey,
        DateOnly fromDate,
        DateOnly toDate,
        string trigger,
        DateTime startedAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(runKey))
        {
            throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                .WithData("truong", nameof(runKey));
        }

        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                .WithData("truong", nameof(trigger));
        }

        // Khoảng ngày ngược ⇒ mọi truy vấn không thấy dòng nào ⇒ động cơ "chạy xong" mà 0 việc.
        if (toDate < fromDate)
        {
            throw new BusinessException(PricingErrorCodes.CauHinhGiaVonKhongHopLe)
                .WithData("lyDo", "`ToDate` phải ≥ `FromDate`.")
                .WithData("from", fromDate)
                .WithData("to", toDate);
        }

        RunKey = runKey.Trim();
        FromDate = fromDate;
        ToDate = toDate;
        Trigger = trigger.Trim();
        StartedAtUtc = startedAtUtc;
        Attempt = 1;
        TrangThai = TrangThaiChayDongCo.DangChay;
    }

    /// <summary>
    /// Bắt đầu **attempt MỚI** trên **cùng dòng** sau một lượt `Loi` (`K06-08b`). 🚨 **NÉM** nếu không phải
    /// `Loi` — máy trạng thái **NÉM ở nhánh sai**, không im lặng (`D6`).
    /// </summary>
    public void BatDauLai(DateTime startedAtUtc)
    {
        if (TrangThai != TrangThaiChayDongCo.Loi)
        {
            throw new InvalidOperationException(
                $"Chỉ được retry một lượt đã `{nameof(TrangThaiChayDongCo.Loi)}`; lượt này đang `{TrangThai}`.");
        }

        Attempt++;
        StartedAtUtc = startedAtUtc;
        FinishedAtUtc = null;
        ErrorMessage = null;
        TrangThai = TrangThaiChayDongCo.DangChay;
    }

    public void HoanTat(
        DateTime finishedAtUtc, int rowsUpserted, int rowsSkippedApproved, int rowsFlaggedMissing)
    {
        if (TrangThai != TrangThaiChayDongCo.DangChay)
        {
            throw new InvalidOperationException($"Chỉ hoàn tất một lượt đang chạy; lượt này đang `{TrangThai}`.");
        }

        TrangThai = TrangThaiChayDongCo.ThanhCong;
        FinishedAtUtc = finishedAtUtc;
        RowsUpserted = rowsUpserted;
        RowsSkippedApproved = rowsSkippedApproved;
        RowsFlaggedMissing = rowsFlaggedMissing;
        ErrorMessage = null;
    }

    public void ThatBai(DateTime finishedAtUtc, string errorMessage)
    {
        if (TrangThai != TrangThaiChayDongCo.DangChay)
        {
            throw new InvalidOperationException($"Chỉ đánh lỗi một lượt đang chạy; lượt này đang `{TrangThai}`.");
        }

        TrangThai = TrangThaiChayDongCo.Loi;
        FinishedAtUtc = finishedAtUtc;
        ErrorMessage = CatNgan(errorMessage);
    }

    /// <summary>Trần cột `ErrorMessage` là `varchar(2048)` ⇒ cắt ngắn thay vì để DB ném khi ghi log lỗi.</summary>
    private static string CatNgan(string? message)
    {
        var giaTri = message ?? "Lỗi không có thông điệp.";

        return giaTri.Length <= 2048 ? giaTri : giaTri[..2048];
    }
}
