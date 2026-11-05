using eCommerce.PaymentHub.Payments;
using eCommerce.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace eCommerce.Application.PaymentHub;

/// <summary>
/// 🚨 **JOB ĐỐI SOÁT THANH TOÁN ĐỊNH KỲ** (`REC-001` mục 7) — biến một endpoint **phải bấm tay** thành
/// **cơ chế tự chạy**.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🆕 **`S15` (28/10/2026) — LỚP NÀY THÀNH "VỎ NHỊP": toàn bộ vòng quét dời sang
/// `PaymentReconciliationRunner`.**
/// 🔑 **VÌ SAO PHẢI DỜI:** `REC-001` §6.1 yêu cầu `TriggerRunAsync` (chạy tay) **đi qua CÙNG khoá** với job.
/// Nếu vòng quét nằm trong worker thì app service **phải chép** 250 dòng luật tiền — hai bản luật, và hai khoá
/// khác tên ⇒ hai đường **chạy chồng**, cùng gọi cổng cho một giao dịch (đốt quota + hai `Run` cho cùng kỳ).
/// ✅ Nay worker chỉ: theo nhịp → gọi runner → ghi neo log. Một luật, một khoá, hai đường vào.
///
/// 🚨 **VẪN GIỮ NGUYÊN BA ĐIỀU QUAN TRỌNG (đừng "dọn" khi sửa lớp này):**
///   1. **Chạy NGAY lần đầu** (`Timer.RunOnStart`) — chờ 15 phút sau mỗi lần deploy thì giao dịch kẹt
///      `Pending` **không có số liệu nào** trong đúng khoảng thời gian dễ xảy ra sự cố nhất.
///   2. **Neo mở đầu `[PAY-JOB-BAT-DAU]` + neo kết thúc `[PAY-JOB-XONG]`** — 🔑 thiếu neo kết thúc thì
///      *"job chạy xong"* và *"job ném giữa đường"* **không phân biệt được** từ log (bài học `SAL-005`).
///   3. **KHÔNG gọi app service có `[Authorize]`** (bài học `SAL-005`: interceptor từ chối principal ẩn danh
///      ⇒ job chết; và các ca kiểm *"job không ghi gì"* vẫn XANH vì job chết, không phải vì nó cố ý không ghi).
///      📌 Runner **không** mang `[Authorize]` ⇒ gọi an toàn từ đây.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public class PaymentReconciliationWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>
    /// Nhịp chạy: **15 phút**. 📌 Cân nhắc: đối soát **gọi HTTP ra cổng** cho **từng** giao dịch ⇒ 🔒 **không**
    /// được dày như `MarketplaceReconciliationWorker` (10 phút, chỉ **đọc** DB). ⚠️ Nhịp dày hơn **không**
    /// làm cảnh báo tốt hơn — 📌 điều quyết định là **con người có đọc log hay không** — mà lại **tiêu quota**
    /// của cổng (tài nguyên **có giới hạn**).
    /// </summary>
    public const int NhipMacDinhGiay = 900;

    public PaymentReconciliationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 Chạy NGAY lần đầu: chờ hết nhịp 15 phút thì trong 15 phút đầu sau **mỗi lần deploy**, giao dịch
        // kẹt `Pending` **không có số liệu nào** — đúng khoảng trống job này sinh ra để bịt.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 🚨 **CÂU LỆNH ĐẦU TIÊN — BẮT BUỘC** (`BackgroundWorkerCoCheTests`): không có dòng này thì
        // *"job im lặng"* **không phân biệt được** với *"job không chạy"*.
        Logger.LogInformation("JOB REC-001: bắt đầu lượt đối soát thanh toán (mốc {Moc}). [PAY-JOB-BAT-DAU]", DateTime.UtcNow);

        var runner = workerContext.ServiceProvider.GetRequiredService<IPaymentReconciliationRunner>();

        var run = await runner.ChayAsync(PaymentReconciliationTrigger.Schedule);

        if (run is null)
        {
            // `Debug` (không `Warning`): instance khác đang chạy là **bình thường**, không phải sự cố.
            Logger.LogDebug("JOB REC-001: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", PaymentReconciliationRunner.TenKhoa);
            return;
        }

        // 📌 Neo `[PAY-JOB-RONG]` **do runner** ghi khi không có giao dịch (một chỗ, cả job lẫn chạy tay dùng
        // chung câu chữ). 🔑 Worker **không** log lại — hai dòng cho một sự kiện làm log khó đọc và dễ bị
        // đếm sai khi phân tích.

        // 🚨 **NEO KẾT THÚC** — 🔑 bắt buộc để phân biệt *"job chạy XONG"* với *"job chạy rồi NÉM"*.
        // 📌 Bài học **ĐO ĐƯỢC** ở `SAL-005`: lượt chạy đầu chỉ có neo mở đầu mà **không** có neo kết thúc
        // ⇒ job **ném giữa đường**, và 🔒 nếu không có neo này thì **không có cách nào** phân biệt hai ca đó từ log.
        Logger.LogInformation("JOB REC-001: xong lượt (mốc {Moc}). [PAY-JOB-XONG]", DateTime.UtcNow);
    }
}
