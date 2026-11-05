using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Reconciliation;

/// <summary>
/// **MỘT GIAO DỊCH TRONG MỘT LƯỢT ĐỐI SOÁT** (`RecPaymentReconciliationLines`) — `REC-001` §5.2.2.
///
/// 🚨 **MÁY TRẠNG THÁI:** `ChoDoiSoat → DangTruyVan → {Khop | Lech | LoiNhaCungCap | ThieuDuLieu | BoQua}`.
/// 🔒 Trạng thái cuối **không** chuyển tiếp được (`Recon:0008`) — nếu cho phép, một dòng đã `Khop` có thể
/// bị sửa thành `Lech` ⇒ báo cáo đối soát **đổi sau khi đã đóng**.
/// 📌 `LocalStatus` là **snapshot số** (không lưu enum của PaymentHub) ⇒ module không phụ thuộc Domain
/// module khác; worker truyền `(int)transaction.Status`.
/// </summary>
public class PaymentReconciliationLine : FullAuditedEntity<Guid>, IMultiTenant
{
    public const int DoDaiMaToiDa = 64;
    public const int DoDaiProviderCodeToiDa = 32;
    public const int DoDaiFailureMessageToiDa = 1024;

    public Guid? TenantId { get; protected set; }

    public Guid RunId { get; protected set; }

    public Guid PaymentTransactionId { get; protected set; }

    public string ProviderCode { get; protected set; } = default!;

    /// <summary>`vnp_TxnRef` — mã giao dịch phía ta.</summary>
    public string MerchantReference { get; protected set; } = default!;

    /// <summary>`vnp_RequestId` — **sinh một lần rồi lưu** (BR-03).</summary>
    public string RequestId { get; protected set; } = default!;

    public PaymentReconciliationLineStatus Status { get; protected set; }

    public string? SkipReason { get; protected set; }

    public int AttemptCount { get; protected set; }

    /// <summary>Snapshot `PaymentTransactionStatus` tại thời điểm đối soát (số, không phụ thuộc module).</summary>
    public int LocalStatus { get; protected set; }

    public long LocalAmountMinorUnits { get; protected set; }

    public string? ProviderResponseCode { get; protected set; }

    public string? ProviderTransactionStatus { get; protected set; }

    public long? ProviderAmountMinorUnits { get; protected set; }

    public string? ProviderTransactionNo { get; protected set; }

    public DateTime? ProviderPayDateUtc { get; protected set; }

    /// <summary>Nguyên văn `yyyyMMddHHmmss` GMT+7 — **không** parse lại để làm bằng chứng.</summary>
    public string? ProviderPayDateGmt7 { get; protected set; }

    public string? ProviderTransactionType { get; protected set; }

    public string? FailureCode { get; protected set; }

    public string? FailureMessage { get; protected set; }

    public Guid? DiscrepancyId { get; protected set; }

    public DateTime? CheckedAtUtc { get; protected set; }

    public string? CorrelationId { get; protected set; }

    protected PaymentReconciliationLine()
    {
    }

    public PaymentReconciliationLine(
        Guid id,
        Guid runId,
        Guid paymentTransactionId,
        string providerCode,
        string merchantReference,
        string requestId,
        int localStatus,
        long localAmountMinorUnits,
        string? correlationId = null) : base(id)
    {
        RunId = runId;
        PaymentTransactionId = paymentTransactionId;
        ProviderCode = Cat(providerCode, DoDaiProviderCodeToiDa) ?? "(khong-co)";
        MerchantReference = Cat(merchantReference, DoDaiMaToiDa) ?? "(khong-co)";
        RequestId = Cat(requestId, DoDaiMaToiDa) ?? "(khong-co)";
        LocalStatus = localStatus;
        LocalAmountMinorUnits = localAmountMinorUnits;
        Status = PaymentReconciliationLineStatus.ChoDoiSoat;
        CorrelationId = Cat(correlationId, 128);
    }

    /// <summary>Ghi phản hồi thô của cổng **trước khi** kết luận (giữ bằng chứng).</summary>
    public void GhiPhanHoiCong(
        string? responseCode,
        string? transactionStatus,
        long? amountMinorUnits,
        string? transactionNo,
        DateTime? payDateUtc,
        string? payDateGmt7,
        string? transactionType)
    {
        KiemTraChuaKetThuc();

        ProviderResponseCode = Cat(responseCode, 8);
        ProviderTransactionStatus = Cat(transactionStatus, 8);
        ProviderAmountMinorUnits = amountMinorUnits;
        ProviderTransactionNo = Cat(transactionNo, 32);
        ProviderPayDateUtc = payDateUtc;
        ProviderPayDateGmt7 = Cat(payDateGmt7, 14);
        ProviderTransactionType = Cat(transactionType, 8);
        AttemptCount++;
        Status = PaymentReconciliationLineStatus.DangTruyVan;
    }

    public void Khop()
    {
        KiemTraChuaKetThuc();
        Status = PaymentReconciliationLineStatus.Khop;
        CheckedAtUtc = DateTime.UtcNow;
    }

    public void Lech(Guid discrepancyId)
    {
        KiemTraChuaKetThuc();
        DiscrepancyId = discrepancyId;
        Status = PaymentReconciliationLineStatus.Lech;
        CheckedAtUtc = DateTime.UtcNow;
    }

    public void LoiNhaCungCap(string failureCode, string? failureMessage)
    {
        KiemTraChuaKetThuc();
        FailureCode = Cat(failureCode, DoDaiMaToiDa) ?? "ProviderUnavailable";
        FailureMessage = Cat(failureMessage, DoDaiFailureMessageToiDa);
        Status = PaymentReconciliationLineStatus.LoiNhaCungCap;
        CheckedAtUtc = DateTime.UtcNow;
    }

    public void ThieuDuLieu(string failureCode = "MissingProviderCreateDate")
    {
        KiemTraChuaKetThuc();
        FailureCode = Cat(failureCode, DoDaiMaToiDa);
        Status = PaymentReconciliationLineStatus.ThieuDuLieu;
        CheckedAtUtc = DateTime.UtcNow;
    }

    public void BoQua(string skipReason)
    {
        KiemTraChuaKetThuc();

        if (string.IsNullOrWhiteSpace(skipReason))
        {
            throw new BusinessException(ReconErrorCodes.LyDoBoQuaKhongHopLe);
        }

        SkipReason = Cat(skipReason, DoDaiMaToiDa);
        Status = PaymentReconciliationLineStatus.BoQua;
        CheckedAtUtc = DateTime.UtcNow;
    }

    public bool DaKetThuc => Status is PaymentReconciliationLineStatus.Khop
        or PaymentReconciliationLineStatus.Lech
        or PaymentReconciliationLineStatus.LoiNhaCungCap
        or PaymentReconciliationLineStatus.ThieuDuLieu
        or PaymentReconciliationLineStatus.BoQua;

    private void KiemTraChuaKetThuc()
    {
        if (DaKetThuc)
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
