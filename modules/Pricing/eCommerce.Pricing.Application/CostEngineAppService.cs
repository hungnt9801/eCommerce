using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — ENDPOINT COMMAND ON-DEMAND CHẠY ĐỘNG CƠ GIÁ VỐN** (`PRC-001-arch` §5.2; `D-323`).
///
/// 🔐 `[Authorize]` **cấp CLASS** + policy `.CostPrice.Manage`. 📌 Worker nền **KHÔNG** đi qua lớp này (worker
/// không có principal — resolve `CostEngineService` trực tiếp, tránh `AbpAuthorizationException`).
/// </summary>
[Authorize(PricingPermissions.CostPriceManage)]
public class CostEngineAppService : ApplicationService, ICostEngineAppService
{
    private readonly CostEngineService _dongCo;

    public CostEngineAppService(CostEngineService dongCo)
    {
        _dongCo = dongCo;
    }

    /// <inheritdoc />
    public async Task<CostEngineRunDto?> ChayAsync(DateOnly from, DateOnly to, string trigger)
    {
        var run = await _dongCo.ChayAsync(from, to, trigger);

        return run is null ? null : ToDto(run);
    }

    private static CostEngineRunDto ToDto(CostEngineRun run) => new()
    {
        Id = run.Id,
        RunKey = run.RunKey,
        FromDate = run.FromDate,
        ToDate = run.ToDate,
        Trigger = run.Trigger,
        TrangThai = run.TrangThai.ToString(),
        Attempt = run.Attempt,
        StartedAtUtc = run.StartedAtUtc,
        FinishedAtUtc = run.FinishedAtUtc,
        RowsUpserted = run.RowsUpserted,
        RowsSkippedApproved = run.RowsSkippedApproved,
        RowsFlaggedMissing = run.RowsFlaggedMissing,
        ErrorMessage = run.ErrorMessage
    };
}
