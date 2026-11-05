using eCommerce.Reconciliation.Dtos;

namespace eCommerce.Reconciliation;

/// <summary>
/// 🆕 **`S15` — BỘ CHẠY ĐỐI SOÁT THANH TOÁN** (`REC-001` §6.1 `TriggerRunAsync` + §3 Basic Flow).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **VÌ SAO CẦN INTERFACE NÀY (không để app service tự chạy lại luật):**
/// cùng một lượt đối soát có **HAI đường vào** — **job định kỳ** (mỗi 15 phút) và **chạy tay** (người vận
/// hành, khi có sự cố). Nếu mỗi đường tự viết lại vòng quét thì:
///   • luật **"chỉ đổi trạng thái khi cổng xác nhận"** có **hai bản** ⇒ lệch khi ai đó sửa một bên;
///   • **khoá phân tán** có hai tên ⇒ hai đường **chạy chồng** lên nhau, cùng gọi cổng cho một giao dịch
///     ⇒ đốt quota cổng và tạo hai `Run` cho cùng một khoảng thời gian.
/// ✅ **Một hiện thực duy nhất** (<c>PaymentReconciliationRunner</c> ở tầng app) → job và app service **cùng
/// gọi**; khoá phân tán nằm **bên trong** runner ⇒ *"chạy tay"* và *"job"* **loại trừ nhau** đúng như `A2`.
///
/// 📌 **VÌ SAO interface ở `Application.Contracts` của MODULE, còn hiện thực ở HOST:** bộ chạy cần
/// `IPaymentProvider` + `PaymentTransaction` (module PaymentHub) — thứ mà module Reconciliation **không**
/// được tham chiếu (kiến trúc module, xem `ReconciliationModuleKienTrucTests`). Chiều phụ thuộc hợp lệ là
/// **host → module** ⇒ host hiện thực hợp đồng của module, module chỉ **gọi hợp đồng**.
/// ⚠️ Nếu chưa có hiện thực (ví dụ Web host không nạp tầng `eCommerce.Application`), đường **chạy tay** sẽ
/// ném lỗi DI **ngay khi gọi** — 📌 đó là lỗi cấu hình rõ ràng, **không** phải im lặng.
/// </summary>
public interface IPaymentReconciliationRunner
{
    /// <summary>
    /// Chạy **một** lượt đối soát và ghi lịch sử (`Run` + `Line` + `Discrepancy`).
    ///
    /// 🔒 **Trả `null` khi KHÔNG giành được khoá** (một lượt khác đang chạy). Người gọi **quyết định** cách
    /// báo: job **bỏ nhịp** (log `Debug`), app service **NÉM** `eCommerce:Lock:0001`.
    /// 📌 **Không** trả `null` cho "không có giao dịch nào cần đối soát" — trường hợp đó trả một `Run`
    /// **rỗng** (`LineTotal = 0`), vì *"đã chạy và không có gì để làm"* **khác** *"không chạy"* — người vận
    /// hành cần phân biệt hai ca này.
    /// </summary>
    /// <param name="trigger">`Schedule` (job) hoặc `Manual` (người vận hành).</param>
    /// <param name="nguoiDungId">Người bấm chạy tay (`null` khi job).</param>
    Task<PaymentReconciliationRunDto?> ChayAsync(
        PaymentReconciliationTrigger trigger,
        Guid? nguoiDungId = null,
        CancellationToken cancellationToken = default);
}
