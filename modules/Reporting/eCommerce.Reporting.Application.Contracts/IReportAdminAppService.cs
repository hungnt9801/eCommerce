using Volo.Abp.Application.Services;

namespace eCommerce.Reporting;

/// <summary>
/// **API VẬN HÀNH GĐ3** (`WP-26`, `REP-001` §4.1) — rebuild · chốt kỳ · đối chiếu.
///
/// 🔐 `[Authorize]` cấp class = `eCommerce.Reports`; method **ghi** gắn thêm quyền hẹp
/// (`Reports.Rebuild` / `Reports.PeriodClose` / `Reports.PeriodReopen` / `Reports.Reconcile`).
/// 📌 Map tay (không AutoMapper), enum trả **số**, 0 PII.
/// </summary>
public interface IReportAdminAppService : IApplicationService
{
    Task<LanChayDto> StartRebuildAsync(StartRebuildInput input);

    Task<LanChayDto> ResumeRebuildAsync(Guid runId);

    Task<LanChayDto> GetLanChayAsync(Guid runId);

    Task<LanChayPageDto> GetLanChaysAsync(GetLanChayInput input);

    Task<KyChotDto> ClosePeriodAsync(ClosePeriodInput input);

    Task<KyChotDto> ReopenPeriodAsync(ReopenPeriodInput input);

    Task<KyChotDto> GetKyChotAsync(Guid id);

    Task<KyChotPageDto> GetKyChotsAsync(GetKyChotInput input);

    Task<KetQuaDoiChieuDto> RunReconciliationAsync(RunReconciliationInput input);

    Task<LechSoPageDto> GetLechSoAsync(GetLechSoInput input);
}
