using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Reconciliation.Dtos;

/// <summary>Một lượt đối soát thanh toán (`RecPaymentReconciliationRuns`).</summary>
public class PaymentReconciliationRunDto
{
    public Guid Id { get; set; }
    public PaymentReconciliationRunStatus Status { get; set; }
    public PaymentReconciliationTrigger TriggeredBy { get; set; }
    public Guid? TriggeredByUserId { get; set; }
    public DateTime PeriodFromUtc { get; set; }
    public DateTime PeriodToUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int LineTotal { get; set; }
    public int LineMatched { get; set; }
    public int LineSkipped { get; set; }
    public int DiscrepancyCount { get; set; }
    public string? FailureCode { get; set; }
    public string? CorrelationId { get; set; }
}

public class PaymentReconciliationLineDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public string ProviderCode { get; set; } = default!;
    public string MerchantReference { get; set; } = default!;
    public string RequestId { get; set; } = default!;
    public PaymentReconciliationLineStatus Status { get; set; }
    public string? SkipReason { get; set; }
    public int AttemptCount { get; set; }
    public int LocalStatus { get; set; }
    public long LocalAmountMinorUnits { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? ProviderTransactionStatus { get; set; }
    public long? ProviderAmountMinorUnits { get; set; }
    public string? ProviderTransactionNo { get; set; }
    public DateTime? ProviderPayDateUtc { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public Guid? DiscrepancyId { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
}

public class PaymentDiscrepancyDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid LineId { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public Guid? OrderId { get; set; }
    public string ProviderCode { get; set; } = default!;
    public string MerchantReference { get; set; } = default!;
    public PaymentDiscrepancyKind Kind { get; set; }
    public PaymentDiscrepancySeverity Severity { get; set; }
    public long? LocalAmountMinorUnits { get; set; }
    public long? ProviderAmountMinorUnits { get; set; }
    public string Currency { get; set; } = default!;
    public PaymentDiscrepancyStatus Status { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
    public string? IgnoreReason { get; set; }
}

public class GetPaymentReconciliationRunsInput : PagedAndSortedResultRequestDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public PaymentReconciliationRunStatus? Status { get; set; }
}

public class GetPaymentReconciliationLinesInput : PagedAndSortedResultRequestDto
{
    [Required]
    public Guid RunId { get; set; }

    public PaymentReconciliationLineStatus? Status { get; set; }
}

public class GetPaymentDiscrepanciesInput : PagedAndSortedResultRequestDto
{
    public PaymentDiscrepancyStatus? Status { get; set; } = PaymentDiscrepancyStatus.Moi;

    public PaymentDiscrepancySeverity? Severity { get; set; }

    public PaymentDiscrepancyKind? Kind { get; set; }

    public Guid? RunId { get; set; }
}
