using eCommerce.Inventory;
using eCommerce.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace eCommerce.Application.Inventory;

/// <summary>
/// 🚨 **JOB ĐỐI SOÁT TỒN KHO ĐỊNH KỲ** (`REC-001`).
///
/// **VÌ SAO CẦN (khoảng trống thật):** `InventoryReconciliationService` phát metric
/// `inventory_recon_mismatch_skus` **chỉ khi có ai đó GỌI API** đối soát (`inv-001` §36.3). Nghĩa là:
/// nếu **không ai gọi**, lệch tồn **không được phát hiện** — **và metric `0` trở thành LỜI NÓI DỐI**
/// (*"0 lệch"* chỉ vì **chưa ai đo**, không phải vì **không có lệch**).
/// 📌 Đúng loại lỗi im lặng mà các cơ chế vòng 34–37 tồn tại để chặn: **đo mà không ai chạy phép đo**.
///
/// 📌 **Theo đúng khuôn worker ĐÃ ĐƯỢC CƠ CHẾ HOÁ** (`BackgroundWorkerCoCheTests` bắt buộc):
///   • **log vô điều kiện** ở **đầu** mỗi lượt ⇒ *"im lặng"* **luôn** nghĩa là *"không chạy"*;
///   • **bọc trong `UnitOfWork` + scope** ⇒ `AsyncPeriodicBackgroundWorkerBase` **không** tự mở UoW
///     (bài học `ObjectDisposedException`, `inv-001` §27.2 **NN2**);
///   • **khoá phân tán** (⚠️ **không** thêm tiền tố `ecommerce:` — `KeyPrefix` đã thêm);
///   • **`Timer.RunOnStart = true`** (bài học `CHN-002`) ⇒ đối soát **ngay sau restart**, không chờ hết nhịp.
///
/// 🚨 **`REC-001` v2 — LƯU LỊCH SỬ + ÁP NGƯỠNG:** lượt quét nay gọi
/// `InventoryReconciliationService.DoiSoatTonKhoAsync(chinhSach)` ⇒ ghi **1 dòng `ReconRuns`** (kèm **ảnh chụp**
/// đơn vị/giá trị ngưỡng/hành động) + các dòng `ReconRunMismatches`, và **phân loại theo ngưỡng** thật.
/// 📌 Nhờ vậy log/metric **không còn** là nơi duy nhất trả lời *"job có chạy"*: bảng `ReconRuns` giữ **cả**
/// lượt không lệch ⇒ `0` lệch đọc được là *"đã đo"*, không phải *"chưa ai đo"*.
/// ⚠️ Vì lượt quét nay **GHI** dữ liệu, `UnitOfWork` của job là **transactional** (`isTransactional: true`) —
/// hoặc cả lượt + các dòng lệch được ghi, hoặc **không** gì (không để lại *"lượt có số lệch nhưng không có dòng nào"*).
/// </summary>
public class InventoryReconciliationWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>
    /// Nhịp đối soát: **15 phút**. 📌 Cân nhắc: đối soát **quét toàn bộ** `InvItems` + `InvStockLedger`
    /// ⇒ đắt hơn các job khác; 15 phút là **đủ nhanh để biết ngay trong ngày** mà **không** tạo tải quét liên tục.
    /// </summary>
    public const int NhipMacDinhGiay = 900;

    /// <summary>Tên khoá phân tán (**không** tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` đã thêm).</summary>
    public const string TenKhoa = "inventory:reconciliation-scan";

    public InventoryReconciliationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 Chạy NGAY lần đầu: nếu chờ hết nhịp 15 phút thì trong 15 phút đầu sau **mỗi lần deploy**,
        // metric `inventory_recon_mismatch_skus` sẽ là `0` **một cách SAI** (chưa ai đo) — đúng vấn đề job này sinh ra để chặn.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 **LOG VÔ ĐIỀU KIỆN** — bắt buộc (cơ chế `BackgroundWorkerCoCheTests`): không có dòng này thì
        // *"job im lặng"* **không phân biệt được** với *"job không chạy"*.
        Logger.LogInformation("JOB ĐỐI SOÁT: bắt đầu quét lệch tồn kho (mốc {Moc}). [REC001-JOB-BAT-DAU]", DateTime.UtcNow);

        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await distributedLock.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(60));

        if (handle is null)
        {
            Logger.LogDebug("JOB ĐỐI SOÁT: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", TenKhoa);
            return;
        }

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        // 🚨 Đổi từ `isTransactional: false` (v1, chỉ đọc) sang `true`: lượt quét **GHI** `ReconRuns` +
        // `ReconRunMismatches` ⇒ hai bảng phải cùng một transaction với nhau (xem chú thích ở đầu lớp).
        using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

        var service = scope.ServiceProvider.GetRequiredService<InventoryReconciliationService>();

        // 🚨 `REC-001` — **CHÍNH SÁCH CHO PHÉP CHỌN** (§93): chính sách nay là **DỮ LIỆU CẤU HÌNH**, nên phải **ĐỌC RA VÀ LOG**
        // *"đang chạy theo chính sách nào"*. 📌 Không có dòng này thì `Warn` và `RequireConfirmation` **giống hệt nhau** khi nhìn log
        // ⇒ người vận hành **không biết** cấu hình của mình có hiệu lực hay không (đúng loại "im lặng" mà các cơ chế tồn tại để chặn).
        // 🔒 Giá trị lạ ⇒ hàm đọc trả **mặc định an toàn** + `hopLe = false` ⇒ ở đây **ghi cảnh báo** (không ném, không chết job).
        var setting = scope.ServiceProvider.GetRequiredService<ISettingProvider>();

        var donVi = ChinhSach.DocDonViNguong(await setting.GetOrNullAsync(TenChinhSach.NguongDoiSoat_DonVi), out var donViHopLe);
        var giaTri = ChinhSach.DocSo(await setting.GetOrNullAsync(TenChinhSach.NguongDoiSoat_GiaTri), 0.5m, out var giaTriHopLe);
        var hanhDong = ChinhSach.DocHanhDongLech(await setting.GetOrNullAsync(TenChinhSach.NguongDoiSoat_HanhDong), out var hanhDongHopLe);

        Logger.LogInformation("JOB ĐỐI SOÁT: CHÍNH SÁCH đang áp dụng — ngưỡng {DonVi}({GiaTri}) · khi lệch thì {HanhDong}. [REC001-CHINH-SACH]",
            donVi, giaTri, hanhDong);

        if (!donViHopLe || !giaTriHopLe || !hanhDongHopLe)
        {
            Logger.LogWarning("JOB ĐỐI SOÁT: CẤU HÌNH CHÍNH SÁCH KHÔNG HỢP LỆ ở {Cho} ⇒ đã dùng MẶC ĐỊNH an toàn. " +
                "Kiểm các setting: {S1} · {S2} · {S3}.",
                string.Join(", ", new[]
                {
                    donViHopLe ? null : TenChinhSach.NguongDoiSoat_DonVi,
                    giaTriHopLe ? null : TenChinhSach.NguongDoiSoat_GiaTri,
                    hanhDongHopLe ? null : TenChinhSach.NguongDoiSoat_HanhDong
                }.Where(x => x is not null)),
                TenChinhSach.NguongDoiSoat_DonVi, TenChinhSach.NguongDoiSoat_GiaTri, TenChinhSach.NguongDoiSoat_HanhDong);
        }

        var lech = await service.DoiSoatTonKhoAsync(
            new InventoryReconciliationService.ChinhSachNguong(donVi, giaTri, hanhDong, donViHopLe && giaTriHopLe && hanhDongHopLe));

        await uow.CompleteAsync();

        // `Warning` (không `Error`): lệch tồn cần người xem nhưng **không** làm chết luồng nào ⇒ giữ mức `Error`
        // sẽ phá tiêu chí "không có dòng `fail:`" của quy trình kiểm chứng (đã ghi ở `inv-001` §22).
        if (lech.Count > 0)
        {
            Logger.LogWarning("JOB ĐỐI SOÁT: phát hiện {So} cặp (kho, SKU) LỆCH giữa OnHand và sổ cái. [REC001-CO-LECH]", lech.Count);
        }
        else
        {
            Logger.LogInformation("JOB ĐỐI SOÁT: KHÔNG có lệch (đã đo {So} cặp). [REC001-KHONG-LECH]", 0);
        }
    }
}
