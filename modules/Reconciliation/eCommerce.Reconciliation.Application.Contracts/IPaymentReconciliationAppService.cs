using eCommerce.Reconciliation.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Reconciliation;

/// <summary>
/// Đọc **lịch sử đối soát thanh toán** (`REC-001` §6.1) — 3 bảng đã có từ 27/10/2026.
///
/// 🔐 Dùng quyền `Payments.Default` (GĐ1) — tách `Reconciliation.View/Manage` là slice riêng
/// (thay đổi seed quyền + UI + test).
/// 🆕 **`S15` (28/10/2026):** có thêm `TriggerRunAsync` — chạy tay **đi qua CÙNG khoá** với job
/// (`IPaymentReconciliationRunner`). 📌 Quyền **vẫn** `Payments.Default` cho khớp đường chạy tay đã có
/// (`IPaymentAppService.PostDoiSoatLoatAsync` dùng cùng quyền) — 🔒 tách `Reconciliation.Manage` là **thay đổi
/// hợp đồng phân quyền** (seed + UI + test) và **chưa** cần cho GĐ1.
/// </summary>
public interface IPaymentReconciliationAppService : IApplicationService
{
    Task<PagedResultDto<PaymentReconciliationRunDto>> GetRunsAsync(GetPaymentReconciliationRunsInput input);

    Task<PagedResultDto<PaymentReconciliationLineDto>> GetLinesAsync(GetPaymentReconciliationLinesInput input);

    /// <summary>Mặc định lọc `Status = Moi` — hàng đợi người vận hành cần xử lý.</summary>
    Task<PagedResultDto<PaymentDiscrepancyDto>> GetDiscrepanciesAsync(GetPaymentDiscrepanciesInput input);

    /// <summary>
    /// 🆕 **`S15`** — chạy **một lượt** đối soát ngay (đường **chạy tay** của `REC-001` §6.1).
    ///
    /// 🔒 **Cùng khoá phân tán với job** ⇒ không chạy chồng. Đang có lượt khác chạy ⇒ **NÉM**
    /// `eCommerce:Lock:0001` (không xếp hàng im lặng).
    /// 📌 Khoảng thời gian của lượt **suy ra từ lô giao dịch** (`min(CreationTime)` → `now`) — người bấm không
    /// tự chọn kỳ; xem `REC-001` §20.3 (sai lệch so với `TriggerReconciliationDto` của đặc tả).
    /// </summary>
    Task<PaymentReconciliationRunDto> TriggerRunAsync();
}
