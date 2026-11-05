using eCommerce.Kernel.Locking;
using eCommerce.Reconciliation.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Reconciliation;

/// <summary>
/// Đọc **lịch sử đối soát thanh toán** + **chạy tay một lượt** (`REC-001` §6.1).
/// 🔒 Quyền `Payments.Default` cho GĐ1 (xem chú thích interface).
/// 🆕 `S15`: `TriggerRunAsync` **không** tự chạy vòng quét — nó gọi `IPaymentReconciliationRunner`, tức
/// **đúng** hiện thực mà job dùng ⇒ cùng luật, cùng khoá (`REC-001` §20).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Payments.Default)]
public class PaymentReconciliationAppService : ApplicationService, IPaymentReconciliationAppService
{
    private readonly IRepository<PaymentReconciliationRun, Guid> _runs;
    private readonly IRepository<PaymentReconciliationLine, Guid> _lines;
    private readonly IRepository<PaymentDiscrepancy, Guid> _discrepancies;
    private readonly IAsyncQueryableExecuter _thucThi;
    private readonly IPaymentReconciliationRunner _runner;

    public PaymentReconciliationAppService(
        IRepository<PaymentReconciliationRun, Guid> runs,
        IRepository<PaymentReconciliationLine, Guid> lines,
        IRepository<PaymentDiscrepancy, Guid> discrepancies,
        IAsyncQueryableExecuter thucThi,
        IPaymentReconciliationRunner runner)
    {
        _runs = runs;
        _lines = lines;
        _discrepancies = discrepancies;
        _thucThi = thucThi;
        _runner = runner;
    }

    /// <summary>
    /// 🆕 **`S15`** — chạy tay một lượt. 🔒 **Đang có lượt khác chạy** (job hoặc người khác) ⇒ runner trả
    /// `null` ⇒ **NÉM** `eCommerce:Lock:0001`, **cấm** im lặng trả về `200` với số đếm cũ.
    /// 📌 Lượt chạy tay được ghi `TriggeredBy = Manual` + `TriggeredByUserId` ⇒ truy được **ai** đã chạy
    /// (bằng chứng tài chính phải trả lời được câu đó).
    /// </summary>
    public async Task<PaymentReconciliationRunDto> TriggerRunAsync()
    {
        var run = await _runner.ChayAsync(PaymentReconciliationTrigger.Manual, CurrentUser.Id);

        if (run is null)
        {
            throw new BusinessException(LockErrorCodes.KhongGianhDuocKhoa)
                .WithData("lyDo", "Một lượt đối soát khác đang chạy ⇒ không chạy chồng (cùng khoá với job).");
        }

        Logger.LogInformation(
            "ĐỐI SOÁT CHẠY TAY: lượt {RunId} · xét {Xet} · lệch {Lech} · bởi {UserId}. [REC-TAY]",
            run.Id, run.LineTotal, run.DiscrepancyCount, CurrentUser.Id);

        return run;
    }

    public async Task<PagedResultDto<PaymentReconciliationRunDto>> GetRunsAsync(GetPaymentReconciliationRunsInput input)
    {
        var q = await _runs.GetQueryableAsync();

        if (input.FromUtc is { } tu)
        {
            q = q.Where(x => x.StartedAtUtc >= tu);
        }

        if (input.ToUtc is { } den)
        {
            q = q.Where(x => x.StartedAtUtc <= den);
        }

        if (input.Status is { } trangThai)
        {
            q = q.Where(x => x.Status == trangThai);
        }

        var tong = await _thucThi.CountAsync(q);
        var ds = await _thucThi.ToListAsync(
            q.OrderByDescending(x => x.StartedAtUtc).Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<PaymentReconciliationRunDto>(tong, ds.Select(ToDto).ToList());
    }

    public async Task<PagedResultDto<PaymentReconciliationLineDto>> GetLinesAsync(GetPaymentReconciliationLinesInput input)
    {
        var q = (await _lines.GetQueryableAsync()).Where(x => x.RunId == input.RunId);

        if (input.Status is { } trangThai)
        {
            q = q.Where(x => x.Status == trangThai);
        }

        var tong = await _thucThi.CountAsync(q);
        var ds = await _thucThi.ToListAsync(
            q.OrderBy(x => x.MerchantReference).Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<PaymentReconciliationLineDto>(tong, ds.Select(ToDto).ToList());
    }

    public async Task<PagedResultDto<PaymentDiscrepancyDto>> GetDiscrepanciesAsync(GetPaymentDiscrepanciesInput input)
    {
        var q = await _discrepancies.GetQueryableAsync();

        if (input.Status is { } trangThai)
        {
            q = q.Where(x => x.Status == trangThai);
        }

        if (input.Severity is { } muc)
        {
            q = q.Where(x => x.Severity == muc);
        }

        if (input.Kind is { } loai)
        {
            q = q.Where(x => x.Kind == loai);
        }

        if (input.RunId is { } runId)
        {
            q = q.Where(x => x.RunId == runId);
        }

        var tong = await _thucThi.CountAsync(q);
        var ds = await _thucThi.ToListAsync(
            q.OrderBy(x => x.Severity).ThenByDescending(x => x.DetectedAtUtc)
                .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<PaymentDiscrepancyDto>(tong, ds.Select(ToDto).ToList());
    }

    private static PaymentReconciliationRunDto ToDto(PaymentReconciliationRun x) => new()
    {
        Id = x.Id,
        Status = x.Status,
        TriggeredBy = x.TriggeredBy,
        TriggeredByUserId = x.TriggeredByUserId,
        PeriodFromUtc = x.PeriodFromUtc,
        PeriodToUtc = x.PeriodToUtc,
        StartedAtUtc = x.StartedAtUtc,
        FinishedAtUtc = x.FinishedAtUtc,
        LineTotal = x.LineTotal,
        LineMatched = x.LineMatched,
        LineSkipped = x.LineSkipped,
        DiscrepancyCount = x.DiscrepancyCount,
        FailureCode = x.FailureCode,
        CorrelationId = x.CorrelationId
    };

    private static PaymentReconciliationLineDto ToDto(PaymentReconciliationLine x) => new()
    {
        Id = x.Id,
        RunId = x.RunId,
        PaymentTransactionId = x.PaymentTransactionId,
        ProviderCode = x.ProviderCode,
        MerchantReference = x.MerchantReference,
        RequestId = x.RequestId,
        Status = x.Status,
        SkipReason = x.SkipReason,
        AttemptCount = x.AttemptCount,
        LocalStatus = x.LocalStatus,
        LocalAmountMinorUnits = x.LocalAmountMinorUnits,
        ProviderResponseCode = x.ProviderResponseCode,
        ProviderTransactionStatus = x.ProviderTransactionStatus,
        ProviderAmountMinorUnits = x.ProviderAmountMinorUnits,
        ProviderTransactionNo = x.ProviderTransactionNo,
        ProviderPayDateUtc = x.ProviderPayDateUtc,
        FailureCode = x.FailureCode,
        FailureMessage = x.FailureMessage,
        DiscrepancyId = x.DiscrepancyId,
        CheckedAtUtc = x.CheckedAtUtc
    };

    private static PaymentDiscrepancyDto ToDto(PaymentDiscrepancy x) => new()
    {
        Id = x.Id,
        RunId = x.RunId,
        LineId = x.LineId,
        PaymentTransactionId = x.PaymentTransactionId,
        OrderId = x.OrderId,
        ProviderCode = x.ProviderCode,
        MerchantReference = x.MerchantReference,
        Kind = x.Kind,
        Severity = x.Severity,
        LocalAmountMinorUnits = x.LocalAmountMinorUnits,
        ProviderAmountMinorUnits = x.ProviderAmountMinorUnits,
        Currency = x.Currency,
        Status = x.Status,
        DetectedAtUtc = x.DetectedAtUtc,
        ResolvedBy = x.ResolvedBy,
        ResolvedAtUtc = x.ResolvedAtUtc,
        ResolutionNote = x.ResolutionNote,
        IgnoreReason = x.IgnoreReason
    };
}
