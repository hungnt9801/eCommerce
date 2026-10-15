using eCommerce.Catalog;
using eCommerce.Kernel.Observability;
using eCommerce.Tax.Classifications;
using eCommerce.Tax.RateSchedules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace eCommerce.Tax;

/// <summary>
/// **Job đếm SKU chưa phân loại thuế** và ghi vào gauge `tax_unclassified_skus`.
///
/// 🎯 VÌ SAO LÀ JOB ĐỊNH KỲ (không phải đếm lúc phát hoá đơn): con số này là **khối lượng công việc
/// tồn của kế toán**. Đếm theo yêu cầu thì không ai nhìn thấy nó cho tới khi có sự cố; đếm định kỳ
/// thì nó thành một **đường trên dashboard** và có thể đặt cảnh báo khi tăng.
///
/// 📌 VỊ TRÍ: worker nằm ở **tầng app** vì nó cần CẢ HAI phía — `ProductVariant` (Catalog ở `eCommerce.Domain`)
/// và bộ phân giải thuế (module `Tax`). Quy ước #3 **cấm module tham chiếu app**, nên worker
/// **không thể** nằm trong module `Tax`. Toàn bộ luật đếm nằm ở
/// <see cref="TaxUnclassifiedSkuCounter"/> (**lõi thuần, đã có test**); lớp này chỉ nạp dữ liệu và ghi metric.
///
/// 🔒 KHOÁ PHÂN TÁN: nhiều instance cùng chạy ⇒ chỉ **một** instance đếm mỗi nhịp. Instance không
/// giành được khoá **bỏ qua lượt** và phát `lock_skipped_total`. Đây là điểm phát metric đó —
/// khác với `lock_timeout_total` (đã phát ở `ChannelTokenManager`, nơi việc bỏ lượt ảnh hưởng đồng bộ).
/// </summary>
public class TaxUnclassifiedSkusWorker : AsyncPeriodicBackgroundWorkerBase
{
    /// <summary>Nhịp đếm. 10 phút — đủ tươi để thấy việc tồn tăng, không tốn kém vì chỉ đọc.</summary>
    public const int NhipMacDinhGiay = 600;

    /// <summary>
    /// Tên khoá phân tán. ⚠️ **KHÔNG** tự thêm tiền tố `ecommerce:` — `AbpDistributedLockOptions.KeyPrefix`
    /// đã thêm (bài học C11/C13: tiền tố bị nhân đôi thành `ecommerce:ecommerce:...`).
    /// </summary>
    public const string TenKhoa = "tax:unclassified-scan";

    public TaxUnclassifiedSkusWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // ⚠️ KHÔNG khai lại `Logger` — lớp cơ sở đã có (bài học CS0108).
        Timer.Period = NhipMacDinhGiay * 1000;

        // 🔒 CHẠY NGAY LẦN ĐẦU khi app khởi động: gauge mặc định là 0, mà `0` nghĩa là
        // "kế toán không còn việc gì". Nếu chờ hết nhịp 10 phút mới đếm lần đầu thì trong 10 phút
        // đầu sau mỗi lần deploy, dashboard sẽ hiển thị **0 một cách SAI**. Đếm ngay ⇒ không có
        // khoảng thời gian hiển thị sai đó.
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // 📌 **LOG VÔ ĐIỀU KIỆN Ở ĐẦU MỖI LƯỢT** — 🚨 **BẮT BUỘC cho mọi job** (`inv-001` §27.3 + §43.4):
        // nếu chỉ log khi *"có việc"* thì *"im lặng"* **KHÔNG** phân biệt được *"job không chạy"* với
        // *"job chạy mà không có việc"* ⇒ **mọi phép kiểm chứng job mất giá trị**. Dòng này được thêm vì
        // **cơ chế** `BackgroundWorkerCoCheTests` (sau khi siết luật) phát hiện worker này **chưa** có nó.
        Logger.LogInformation("JOB TaxUnclassifiedSkusWorker: bắt đầu một lượt (mốc {Moc}).", DateTime.UtcNow);

        var khoa = workerContext.ServiceProvider.GetRequiredService<IAbpDistributedLock>();

        await using var handle = await khoa.TryAcquireAsync(TenKhoa, TimeSpan.FromSeconds(5));

        if (handle is null)
        {
            // Instance khác đang đếm ⇒ BỎ QUA LƯỢT NÀY. Đo lại để biết việc đếm có bị tranh chấp
            // liên tục không (nhịp quá ngắn so với thời gian đếm, hoặc có instance bị treo).
            var metrics = workerContext.ServiceProvider.GetService<eCommerceMetrics>();

            metrics?.LockSkipped.Add(1,
                new KeyValuePair<string, object?>(eCommerceMetricTags.Module, "tax"),
                new KeyValuePair<string, object?>(eCommerceMetricTags.Reason, "khoa-ban"));

            return;
        }

        await DemVaGhiAsync(workerContext);
    }

    private async Task DemVaGhiAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var metrics = workerContext.ServiceProvider.GetService<eCommerceMetrics>();
        var dongHo = workerContext.ServiceProvider.GetRequiredService<IClock>();
        var variantRepo = workerContext.ServiceProvider.GetRequiredService<IRepository<ProductVariant, Guid>>();
        var classificationRepo = workerContext.ServiceProvider.GetRequiredService<IRepository<TaxClassification, Guid>>();
        var scheduleRepo = workerContext.ServiceProvider.GetRequiredService<IRepository<TaxRateSchedule, Guid>>();

        var homNay = DateOnly.FromDateTime(dongHo.Now);

        try
        {
            var variants = await variantRepo.GetListAsync();
            var classifications = await classificationRepo.GetListAsync();
            var schedules = await scheduleRepo.GetListAsync();

            // Chỉ đưa vào những bản ghi THỰC SỰ có hiệu lực tại hôm nay — dùng lại đúng luật của entity
            // (`IsUsableAt` chỉ nhận Approved/Active — BR-007), KHÔNG tự viết lại điều kiện ở đây.
            var dungDuoc = classifications.Where(c => c.IsUsableAt(homNay)).ToList();
            var hieuLuc = schedules.Where(s => s.IsEffectiveAt(homNay)).ToList();

            var ketQua = TaxUnclassifiedSkuCounter.Dem(
                variants.Select(v => new SkuCanDem(v.Id, v.IsActive)),
                dungDuoc,
                hieuLuc,
                homNay);

            metrics?.SetTaxUnclassifiedSkus(ketQua.SoChuaPhanLoai);

            // Ghi log MỌI lần đếm thành công (1 dòng mỗi 10 phút — không đáng kể):
            //   • là dấu vết duy nhất chứng minh job THỰC SỰ CHẠY (không chỉ "đã đăng ký"),
            //   • và cho biết gauge đang mang giá trị nào mà không cần mở dashboard.
            Logger.LogInformation(
                "Đếm SKU chưa phân loại thuế: {SoChuaPhanLoai}/{TongSo} SKU đang bán chưa phân loại.",
                ketQua.SoChuaPhanLoai, ketQua.TongSoSkuDangBan);

            if (ketQua.SoChuaPhanLoai > 0)
            {
                // Warning: đây là VIỆC ĐANG TỒN CHẶN phát hành hoá đơn, không phải thông tin thường.
                Logger.LogWarning(
                    "Có {SoChuaPhanLoai}/{TongSo} SKU đang bán CHƯA phân loại thuế — các SKU này CHẶN phát hành hoá đơn (eCommerce:Tax:0003). Kế toán cần xử lý.",
                    ketQua.SoChuaPhanLoai, ketQua.TongSoSkuDangBan);
            }
        }
        catch (Exception ex)
        {
            // 🚨 KHÔNG để gauge giữ giá trị 0 khi đếm lỗi: `0` nghĩa là "kế toán không còn việc gì"
            // ⇒ sẽ là LỜI NÓI DỐI IM LẶNG. Đặt sentinel "chưa xác định" để phân biệt rõ.
            Logger.LogError(ex,
                "Đếm SKU chưa phân loại thuế THẤT BẠI — gauge được đặt về {ChuaXacDinh} (chưa xác định), KHÔNG phải 0.",
                TaxUnclassifiedSkuCounter.ChuaXacDinh);

            metrics?.SetTaxUnclassifiedSkus(TaxUnclassifiedSkuCounter.ChuaXacDinh);
        }
    }
}
