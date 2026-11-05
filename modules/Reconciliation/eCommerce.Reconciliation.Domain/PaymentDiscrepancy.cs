using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reconciliation;

/// <summary>
/// **SAI LỆCH CẦN NGƯỜI XỬ LÝ** (`RecPaymentDiscrepancies`) — `REC-001` §5.2.3.
///
/// 🚨 **MÁY TRẠNG THÁI:** `Moi → DaXacNhan → DaXuLy` (hoặc `BoQua`). 🔒 `DaXuLy` **bắt buộc** ghi chú ≥ 10
/// ký tự (`Recon:0009`): đây là **chênh lệch tiền**, ghi chú rỗng khiến người sau không phân biệt được
/// *"đã kiểm và khớp lại"* với *"bấm nút cho hết danh sách"*.
/// 📌 `IgnoreReason` **ĐÓNG** theo `REC-001` §5.4 — lý do tự do thì báo cáo không gom nhóm được.
/// </summary>
public class PaymentDiscrepancy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiGhiChuToiDa = 2048;
    public const int DoDaiGhiChuToiThieu = 10;
    public const int DoDaiMaToiDa = 64;

    private static readonly HashSet<string> LyDoBoQuaHopLe = new(StringComparer.Ordinal)
    {
        "DuplicatedWebhook",
        "TestTransaction",
        "KnownProviderDelay",
        "AlreadyHandledManually"
    };

    public Guid? TenantId { get; protected set; }

    public Guid RunId { get; protected set; }

    public Guid LineId { get; protected set; }

    public Guid PaymentTransactionId { get; protected set; }

    public Guid? OrderId { get; protected set; }

    public string ProviderCode { get; protected set; } = default!;

    public string MerchantReference { get; protected set; } = default!;

    public PaymentDiscrepancyKind Kind { get; protected set; }

    public PaymentDiscrepancySeverity Severity { get; protected set; }

    public long? LocalAmountMinorUnits { get; protected set; }

    public long? ProviderAmountMinorUnits { get; protected set; }

    public string Currency { get; protected set; } = "VND";

    public PaymentDiscrepancyStatus Status { get; protected set; }

    public Guid? AcknowledgedBy { get; protected set; }

    public DateTime? AcknowledgedAtUtc { get; protected set; }

    public Guid? ResolvedBy { get; protected set; }

    public DateTime? ResolvedAtUtc { get; protected set; }

    public string? ResolutionNote { get; protected set; }

    public string? IgnoreReason { get; protected set; }

    public DateTime DetectedAtUtc { get; protected set; }

    public string? CorrelationId { get; protected set; }

    protected PaymentDiscrepancy()
    {
    }

    public PaymentDiscrepancy(
        Guid id,
        Guid runId,
        Guid lineId,
        Guid paymentTransactionId,
        Guid? orderId,
        string providerCode,
        string merchantReference,
        PaymentDiscrepancyKind kind,
        PaymentDiscrepancySeverity severity,
        long? localAmountMinorUnits = null,
        long? providerAmountMinorUnits = null,
        string currency = "VND",
        string? correlationId = null) : base(id)
    {
        RunId = runId;
        LineId = lineId;
        PaymentTransactionId = paymentTransactionId;
        OrderId = orderId;
        ProviderCode = Cat(providerCode, 32) ?? "(khong-co)";
        MerchantReference = Cat(merchantReference, 64) ?? "(khong-co)";
        Kind = kind;
        Severity = severity;
        LocalAmountMinorUnits = localAmountMinorUnits;
        ProviderAmountMinorUnits = providerAmountMinorUnits;
        Currency = Cat(currency, 3) ?? "VND";
        Status = PaymentDiscrepancyStatus.Moi;
        DetectedAtUtc = DateTime.UtcNow;
        CorrelationId = Cat(correlationId, 128);
    }

    /// <summary>Người vận hành **xác nhận đã thấy** lệch (chưa xử lý).</summary>
    public void XacNhan(Guid nguoiDungId)
    {
        KiemTraChuaDong();

        if (Status != PaymentDiscrepancyStatus.Moi)
        {
            throw new BusinessException(ReconErrorCodes.LyDoBoQuaKhongHopLe)
                .WithData("lyDo", "Chỉ lệch Moi mới được xác nhận.");
        }

        AcknowledgedBy = nguoiDungId;
        AcknowledgedAtUtc = DateTime.UtcNow;
        Status = PaymentDiscrepancyStatus.DaXacNhan;
    }

    /// <summary>Đóng lệch **kèm ghi chú** (≥ 10 ký tự) — bắt buộc theo `REC-001` §5.2.3.</summary>
    public void XuLy(Guid nguoiDungId, string ghiChu)
    {
        KiemTraChuaDong();

        var s = (ghiChu ?? string.Empty).Trim();

        if (s.Length < DoDaiGhiChuToiThieu)
        {
            throw new BusinessException(ReconErrorCodes.LyDoXuLyNgan)
                .WithData("doDai", s.Length)
                .WithData("toiThieu", DoDaiGhiChuToiThieu)
                .WithData("lyDo", "Ghi chú xử lý lệch phải có ít nhất 10 ký tự.");
        }

        ResolutionNote = Cat(s, DoDaiGhiChuToiDa);
        ResolvedBy = nguoiDungId;
        ResolvedAtUtc = DateTime.UtcNow;
        Status = PaymentDiscrepancyStatus.DaXuLy;
    }

    /// <summary>Bỏ qua lệch — lý do **phải thuộc danh sách ĐÓNG** (`REC-001` §5.4).</summary>
    public void BoQua(string lyDo)
    {
        KiemTraChuaDong();

        var s = (lyDo ?? string.Empty).Trim();

        if (!LyDoBoQuaHopLe.Contains(s))
        {
            throw new BusinessException(ReconErrorCodes.LyDoBoQuaKhongHopLe)
                .WithData("lyDo", s)
                .WithData("hopLe", string.Join(", ", LyDoBoQuaHopLe));
        }

        IgnoreReason = s;
        ResolvedAtUtc = DateTime.UtcNow;
        Status = PaymentDiscrepancyStatus.BoQua;
    }

    private void KiemTraChuaDong()
    {
        if (Status == PaymentDiscrepancyStatus.DaXuLy || Status == PaymentDiscrepancyStatus.BoQua)
        {
            throw new BusinessException(ReconErrorCodes.DongDaKetThuc)
                .WithData("trangThai", Status.ToString());
        }
    }

    private static string? Cat(string? giaTri, int toiDa)
    {
        var s = giaTri?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }

        return s.Length <= toiDa ? s : s[..toiDa];
    }
}
