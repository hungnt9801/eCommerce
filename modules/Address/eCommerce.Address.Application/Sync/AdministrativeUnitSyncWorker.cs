using eCommerce.Address.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace eCommerce.Address;

/// <summary>
/// **Job đồng bộ danh mục ĐVHC** từ nguồn chính thức (`ADDR-001`).
///
/// 🚨 MẶC ĐỊNH **TẮT** (`Address:Sync:Enabled = false`).
///   Lý do: job này **gọi API bên ngoài** rồi **sửa danh mục địa chỉ đang dùng**. Bật mặc định nghĩa là
///   một dịch vụ ngoài (hoặc mạng) có thể **thay đổi dữ liệu vận hành** mà không ai chủ động quyết định.
///   Bật bằng cấu hình ⇒ người vận hành **chủ động**, và có thể tắt ngay khi nguồn có sự cố.
///
/// 🔒 BỐN HÀNG RÀO (đã có test ở `AdministrativeUnitSynchronizer`) là **lưới an toàn cuối**:
///   danh mục rỗng · quá ít đơn vị · thiếu cấp tỉnh · **đóng vượt 20%**. Khi hàng rào chặn, job
///   **KHÔNG áp dụng gì** và ghi log **Error** — thay vì đóng hàng loạt địa chỉ đang dùng.
///
/// 📌 VỊ TRÍ: worker nằm ở tầng app của module Address; toàn bộ luật nằm ở
///   <see cref="ProvincesOpenApiParser"/> + <see cref="AdministrativeUnitSynchronizer"/> (đều thuần, đã test).
/// </summary>
public class AdministrativeUnitSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp đồng bộ. 1 ngày — danh mục ĐVHC thay đổi rất chậm, không cần nhanh hơn.</summary>
    public const int NhipMacDinhGiay = 86_400;

    /// <summary>
    /// Tên khoá phân tán. ⚠️ KHÔNG thêm tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix`
    /// đã thêm (bài học C11/C13: tiền tố bị nhân đôi).
    /// </summary>
    public const string TenKhoa = "address:sync-catalog";

    // ── Khoá cấu hình ──
    public const string CauHinhBat = "Address:Sync:Enabled";
    public const string CauHinhPhienBan = "Address:Sync:SchemaVersion";

    /// <summary>Nhịp đồng bộ (giây) đọc từ cấu hình — để vận hành chỉnh được mà không phải build lại.</summary>
    public const string CauHinhNhip = "Address:Sync:PeriodSeconds";

    public AdministrativeUnitSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // ⚠️ KHÔNG khai lại `Logger` — lớp cơ sở đã có (bài học CS0108).
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🚨 LỖI THẬT ĐÃ SỬA (14/10/2026, do kiểm chứng end-to-end bắt được):
        //   Ban đầu tôi KHÔNG đặt `RunOnStart` và định đọc nhịp từ cấu hình **bên trong** `DoWorkAsync`.
        //   Nhưng `DoWorkAsync` chỉ chạy **SAU KHI hết nhịp đầu tiên** (mặc định 1 NGÀY) ⇒ cấu hình
        //   `Address:Sync:PeriodSeconds` **không bao giờ có tác dụng ở lượt đầu**, và tệ hơn: worker
        //   **im lặng không chạy gì** trong suốt ngày đầu sau mỗi lần deploy.
        //   Kiểm chứng thật cho thấy đúng như vậy: worker "Started" nhưng KHÔNG có dòng log nào và
        //   DB vẫn 0 đơn vị.
        //
        //   Nay đặt `RunOnStart = true`: lượt đầu chạy NGAY ⇒ đọc cấu hình ngay và áp nhịp ngay.
        //   Điều này AN TOÀN vì job **mặc định TẮT** (`Address:Sync:Enabled = false`): lượt chạy ngay
        //   chỉ đọc cấu hình rồi thoát, **không gọi API ngoài**. Khi người vận hành chủ động bật thì
        //   đồng bộ ngay lúc khởi động là hành vi MONG MUỐN (dữ liệu tươi ngay sau deploy).
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 **LOG VÔ ĐIỀU KIỆN Ở ĐẦU MỖI LƯỢT** — 🚨 **BẮT BUỘC cho mọi job** (`inv-001` §27.3 + §43.4):
        // nếu chỉ log khi *"có việc"* thì *"im lặng"* **KHÔNG** phân biệt được *"job không chạy"* với
        // *"job chạy mà không có việc"* ⇒ **mọi phép kiểm chứng job mất giá trị**. Dòng này được thêm vì
        // **cơ chế** `BackgroundWorkerCoCheTests` (sau khi siết luật) phát hiện worker này **chưa** có nó.
        Logger.LogInformation("JOB AdministrativeUnitSyncWorker: bắt đầu một lượt (mốc {Moc}).", DateTime.UtcNow);

        var cauHinh = workerContext.ServiceProvider.GetRequiredService<IConfiguration>();

        // Nhịp đọc từ cấu hình (mặc định 1 ngày). Đặt ở đây vì `AbpAsyncTimer.Period` đổi được lúc chạy.
        var nhipGiay = cauHinh.GetValue(CauHinhNhip, NhipMacDinhGiay);

        if (nhipGiay > 0 && Timer.Period != nhipGiay * 1000)
        {
            Timer.Period = nhipGiay * 1000;
        }

        if (!cauHinh.GetValue(CauHinhBat, defaultValue: false))
        {
            Logger.LogDebug(
                "Đồng bộ danh mục ĐVHC đang TẮT ({CauHinh} = false). Bật bằng cấu hình khi muốn đồng bộ.",
                CauHinhBat);
            return;
        }

        var khoa = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await khoa.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            Logger.LogWarning("Không giành được khoá {TenKhoa}; bỏ qua lượt đồng bộ danh mục ĐVHC.", TenKhoa);
            return;
        }

        await DongBoAsync(workerContext, cauHinh);
    }

    private async Task DongBoAsync(PeriodicBackgroundWorkerContext workerContext, IConfiguration cauHinh)
    {
        var phienBan = cauHinh.GetValue(CauHinhPhienBan, defaultValue: 2);
        var nguon = workerContext.ServiceProvider.GetRequiredService<IKhungDonViHanhChinhSource>();
        var repo = workerContext.ServiceProvider.GetRequiredService<IRepository<AdministrativeUnit, Guid>>();
        var dongHo = workerContext.ServiceProvider.GetRequiredService<IClock>();

        try
        {
            var danhMuc = await nguon.TaiDanhMucAsync(phienBan);
            var hienCo = await repo.GetListAsync();

            // 🚨 Hàng rào nằm ở đây: ném lỗi ⇒ nhảy xuống catch ⇒ KHÔNG áp dụng gì.
            var keHoach = AdministrativeUnitSynchronizer.LapKeHoach(phienBan, hienCo, danhMuc);

            if (!keHoach.CoThayDoi)
            {
                Logger.LogInformation(
                    "Đồng bộ danh mục ĐVHC: không có thay đổi (nguồn {Nguon}, phiên bản {PhienBan}, {SoDonVi} đơn vị).",
                    danhMuc.Nguon, phienBan, danhMuc.DonVis.Count);
                return;
            }

            var deThem = AdministrativeUnitSynchronizer.ApDung(keHoach);

            if (deThem.Count > 0)
            {
                await repo.InsertManyAsync(deThem, autoSave: true);
            }

            // Các entity bị đổi tên / mở lại / đóng đã được sửa tại chỗ ⇒ cần cập nhật.
            var daSua = keHoach.ThayDois
                .Where(x => x is { Loai: not LoaiThayDoi.ThemMoi, HienCo: not null })
                .Select(x => x.HienCo!)
                .Distinct()
                .ToList();

            if (daSua.Count > 0)
            {
                await repo.UpdateManyAsync(daSua, autoSave: true);
            }

            Logger.LogInformation(
                "Đồng bộ danh mục ĐVHC xong (nguồn {Nguon}, phiên bản {PhienBan}): +{Them} mới, ~{DoiTen} đổi tên, ~{MoLai} mở lại, -{Dong} đóng.",
                danhMuc.Nguon, phienBan, keHoach.SoThemMoi, keHoach.SoDoiTen, keHoach.SoMoLai, keHoach.SoDong);

            _ = dongHo;   // (giữ lại để trống chỗ nếu sau này cần ghi dấu vết thời điểm đồng bộ)
        }
        catch (BusinessException ex)
        {
            // 🚨 HÀNG RÀO ĐÃ CHẶN. Đây là ca quan trọng nhất: nguồn không đáng tin (rỗng/quá ít/thiếu cấp/
            // đóng quá nhiều) ⇒ KHÔNG áp dụng gì, và phải ồn ào để người vận hành biết đồng bộ đang KHÔNG chạy.
            Logger.LogError(
                "🚨 Đồng bộ danh mục ĐVHC BỊ TỪ CHỐI (mã {Ma}). KHÔNG áp dụng thay đổi nào — danh mục giữ nguyên. Lý do: {DuLieu}",
                ex.Code, string.Join("; ", ex.Data.Keys.Cast<object>().Select(k => $"{k}={ex.Data[k]}")));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Đồng bộ danh mục ĐVHC thất bại (lỗi không mong đợi). Không áp dụng thay đổi nào.");
        }
    }
}
