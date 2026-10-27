using eCommerce.Reconciliation.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Reconciliation;

/// <summary>
/// Đọc **lịch sử đối soát thanh toán** (`REC-001` §6.1) — 3 bảng đã có từ 27/10/2026.
///
/// 🔐 Dùng quyền `Payments.Default` (GĐ1) — tách `Reconciliation.View/Manage` là slice riêng
/// (thay đổi seed quyền + UI + test).
/// 📌 **Chỉ ĐỌC** ở slice này: ghi lượt/dòng/lệch do worker đảm nhiệm (slice 2b, xem `REC-001` §19).
/// </summary>
public interface IPaymentReconciliationAppService : IApplicationService
{
    Task<PagedResultDto<PaymentReconciliationRunDto>> GetRunsAsync(GetPaymentReconciliationRunsInput input);

    Task<PagedResultDto<PaymentReconciliationLineDto>> GetLinesAsync(GetPaymentReconciliationLinesInput input);

    /// <summary>Mặc định lọc `Status = Moi` — hàng đợi người vận hành cần xử lý.</summary>
    Task<PagedResultDto<PaymentDiscrepancyDto>> GetDiscrepanciesAsync(GetPaymentDiscrepanciesInput input);
}
