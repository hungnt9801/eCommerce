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
/// 🚨 **JOB DỌN LỊCH SỬ ĐỐI SOÁT THEO RETENTION** (`REC-001` §17.8 **#1** — nợ *"bảng chỉ tăng"*).
///
/// **VÌ SAO PHẢI LÀ JOB, KHÔNG PHẢI CHỈ MỘT NÚT BẤM:** nút bấm nghĩa là *"có ai đó nhớ ra"* — và đúng cái
/// nghĩa đó làm bảng phình suốt 6 tháng. 📌 Nhưng **ngược lại cũng sai**: chỉ có job thì **không ai kiểm được**
/// nó có chạy và nó xoá cái gì ⇒ nên **cùng một luật** còn có **đường bấm tay** (`PostDonDepAsync`, có
/// *xem trước*) do người có quyền `eCommerce.Reconciliation.Confirm` gọi.
///
/// 🔒 **Luật nằm ở `DoiSoatRetention` (hàm THUẦN) và `DoiSoatRetentionService`** — job này **KHÔNG** tự chứa
/// luật nào, để job và nút bấm **không thể lệch nhau** (bài học *"hai nguồn sự thật"*, đã trả giá nhiều lần).
///
/// 📌 **Theo đúng khuôn worker đã được cơ chế hoá** (`BackgroundWorkerCoCheTests` bắt buộc):
///   • **log vô điều kiện ở CÂU LỆNH ĐẦU TIÊN** ⇒ *"im lặng"* luôn nghĩa là *"không chạy"*;
///   • **`Timer.RunOnStart = true`** (`CHN-002`) ⇒ dọn ngay sau restart, không chờ hết nhịp;
///   • **khoá phân tán** (⚠️ **không** thêm tiền tố `ecommerce:` — `KeyPrefix` đã thêm);
///   • **bọc `UnitOfWork` + scope** vì có resolve **dịch vụ miền** (`DoiSoatRetentionService`) — nếu không:
///     `ObjectDisposedException: Cannot access a disposed context instance` (đã đo được ở vòng 35).
/// </summary>
public class ReconRetentionWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>
    /// Nhịp dọn: **6 giờ**. 📌 Retention tính theo **NGÀY** ⇒ nhịp dày hơn không làm dữ liệu sạch hơn;
    /// chọn 6 giờ chỉ để *"vừa đổi chính sách xuống 30 ngày là thấy hiệu lực trong cùng buổi"*.
    /// </summary>
    public const int NhipMacDinhGiay = 21_600;

    /// <summary>Tên khoá phân tán (**không** tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix` đã thêm).</summary>
    public const string TenKhoa = "inventory:recon-retention";

    public ReconRetentionWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = NhipMacDinhGiay * 1000;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 LOG VÔ ĐIỀU KIỆN — câu lệnh ĐẦU TIÊN (cơ chế `BackgroundWorkerCoCheTests`).
        Logger.LogInformation("JOB DỌN ĐỐI SOÁT: bắt đầu một lượt (mốc {Moc}). [REC-RETENTION-BAT-DAU]", DateTime.UtcNow);

        var distributedLock = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await distributedLock.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(60));

        if (handle is null)
        {
            Logger.LogDebug("JOB DỌN ĐỐI SOÁT: instance khác đang giữ khoá {Khoa} ⇒ bỏ lượt này.", TenKhoa);
            return;
        }

        using var scope = workerContext.ServiceProvider.CreateScope();

        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(requiresNew: true, isTransactional: true);

        var setting = scope.ServiceProvider.GetRequiredService<ISettingProvider>();

        var soNgay = ChinhSach.DocSoNgayLuuTru(
            await setting.GetOrNullAsync(TenChinhSach.LuuTruDoiSoat_SoNgay), out var hopLe);

        if (!hopLe)
        {
            Logger.LogWarning(
                "JOB DỌN ĐỐI SOÁT: setting `{Setting}` KHÔNG hợp lệ ⇒ dùng mặc định an toàn {SoNgay} ngày. [REC-RETENTION-SETTING-SAI]",
                TenChinhSach.LuuTruDoiSoat_SoNgay, soNgay);
        }

        // 🔒 `0` = TẮT (chủ ý) ⇒ **không** gọi đường dọn (đường đó NÉM khi `<= 0`, đúng thiết kế: `0` ở *lệnh*
        // nghĩa là "xoá hết" — vô nghĩa). 📌 Ghi log vì *"tắt"* và *"job không chạy"* phải **phân biệt được**.
        if (soNgay <= 0)
        {
            Logger.LogInformation(
                "JOB DỌN ĐỐI SOÁT: retention đang TẮT (`{Setting}` = {GiaTri}) ⇒ giữ toàn bộ lịch sử. [REC-RETENTION-TAT]",
                TenChinhSach.LuuTruDoiSoat_SoNgay, soNgay);
            await uow.CompleteAsync();
            return;
        }

        var service = scope.ServiceProvider.GetRequiredService<DoiSoatRetentionService>();

        var ketQua = await service.DonDepAsync(DateTime.UtcNow, soNgay, chiXemTruoc: false);

        await uow.CompleteAsync();

        Logger.LogInformation(
            "JOB DỌN ĐỐI SOÁT: XONG · số ngày={SoNgay} · mốc={Moc:O} · đã xoá={DaXoa} · " +
            "giữ lại (chưa xử lý)={ChuaXuLy} · giữ lại (chờ xác nhận)={ChoXacNhan}. [REC-RETENTION-XONG]",
            ketQua.SoNgay, ketQua.MocUtc, ketQua.SoDongDaXoa, ketQua.SoDongGiuLaiChuaXuLy, ketQua.SoDongGiuLaiChoXacNhan);
    }
}
