using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;

namespace eCommerce;

/// <summary>
/// Tầng application: điều phối use case, gọi domain + repository, publish event.
/// Không chứa logic nghiệp vụ cốt lõi (nằm ở Domain) và không truy cập DbContext trực tiếp.
///
/// 📌 QUYẾT ĐỊNH: KHÔNG dùng AutoMapper.
///   • Chỉ cần map một số DTO đơn giản ⇒ viết tay rõ ràng hơn và ít rủi ro hơn.
///   • Volo.Abp.AutoMapper kéo theo AutoMapper 14.0.0, bản này có cảnh báo bảo mật
///     NU1903 (GHSA-rvv3-g6hj-g44x) trong lần restore đầu tiên.
///   • Nếu sau này cần map phức tạp, cân nhắc Mapperly (source generator, không reflection).
///
/// 📌 Phase 0: các module ABP được thêm bằng ABP CLI; attribute đã được gộp lại.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainModule),
    typeof(eCommerceApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    // 🚨 `SAL-005` — nơi nhận đơn sàn (`MarketplaceOrderIntakeHandler`) cần `IIdempotencyGuard`
    // của module Messaging (`XC-002`). Thiếu dòng này ⇒ handler không resolve được ⇒ đơn mất im lặng.
    typeof(Messaging.MessagingDomainModule),
    typeof(Tax.TaxDomainModule)
)]
[DependsOn(typeof(AbpAccountApplicationModule))]
    public class eCommerceApplicationModule : AbpModule
{
    /// <summary>
    /// Đăng ký job đếm SKU chưa phân loại thuế (`tax_unclassified_skus`).
    ///
    /// ⚠️ ĐÂY LÀ CHỖ DỄ SAI NHẤT (đã gặp 2 lần trong dự án): worker phải được **đăng ký tường minh**
    /// thì mới chạy. Nếu chỉ có class mà không có dòng dưới đây thì **KHÔNG có lỗi build**, app vẫn
    /// khởi động bình thường, nhưng job **im lặng không bao giờ chạy** — và metric sẽ mãi mãi là 0.
    /// </summary>
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<Tax.TaxUnclassifiedSkusWorker>();

        // INV-002 — **JOB NHẢ CHỖ GIỮ HÀNG QUÁ HẠN**. 📌 Không có job này thì giữ chỗ **vô thời hạn trên thực tế**
        // (khách bỏ ngang ⇒ hàng không ai mua được, **KHÔNG có lỗi nào** để lần ra). `Timer.RunOnStart = true`
        // trong worker để hàng đã quá hạn **TỪ TRƯỚC** lúc deploy được nhả ngay (bài học `CHN-002`).
        // 🚨 **BÀI HỌC VÒNG 35:** dòng đăng ký này **đã từng bị bỏ sót im lặng** — script sửa tệp báo "thành công"
        // nhưng tệp **không đổi**, worker **không bao giờ chạy**, và phép kiểm chứng chỉ ra *"không có log"* mà
        // **không** phân biệt được *"không chạy"* với *"chạy mà không có việc"*. ⇒ Sửa tệp bằng `edit` (không dùng
        // ghép chuỗi trong script) và **kiểm lại tệp sau khi sửa** (`Select-String`) — xem `inv-001` §26.3.
        await context.AddBackgroundWorkerAsync<eCommerce.Application.Inventory.StockReservationExpiryWorker>();

        // REC-001 — **JOB ĐỐI SOÁT TỒN KHO ĐỊNH KỲ**. 📌 Metric `inventory_recon_mismatch_skus` trước đây chỉ phát
        // khi **có người gọi API** ⇒ nếu không ai gọi thì **lệch tồn KHÔNG được phát hiện**, và metric `0` trở thành
        // **LỜI NÓI DỐI** (*"0 lệch"* vì **chưa ai đo**, không phải vì **không có lệch**). Job này khiến phép đo
        // **luôn chạy** (và chạy **ngay** sau restart nhờ `Timer.RunOnStart`).
        await context.AddBackgroundWorkerAsync<eCommerce.Application.Inventory.InventoryReconciliationWorker>();

        // 🆕 `S7` — **JOB DỌN LỊCH SỬ ĐỐI SOÁT THEO RETENTION** (`REC-001` §17.8 #1 — nợ *"bảng chỉ tăng"*).
        // 📌 Không có dòng này thì `ReconRuns`/`ReconRunMismatches` **chỉ tăng mãi** và **không có lỗi nào**:
        // bảng phình làm truy vấn *"còn việc gì"* chậm dần, và không ai quy được cho bảng nào.
        // 🔒 Đăng ký là **điều kiện** để `BackgroundWorkerCoCheTests` xanh (nó quét mã nguồn tìm dòng này).
        await context.AddBackgroundWorkerAsync<eCommerce.Application.Inventory.ReconRetentionWorker>();

            // 🚨 `SAL-005` **D12** + `XC-002` — đối soát sàn + cảnh báo DLQ. 📌 **BẮT BUỘC đăng ký**:
            // thiếu dòng này thì worker **KHÔNG BAO GIỜ CHẠY** mà **không có lỗi nào** (đã trả giá thật ở
            // vòng 35 với `StockReservationExpiryWorker` — `inv-001` §27 **NN1**).
            // 🔑 Và `BackgroundWorkerCoCheTests` **bắt** điều này bằng phép quét mã nguồn.
            await context.AddBackgroundWorkerAsync<eCommerce.Application.Marketplace.MarketplaceReconciliationWorker>();
        // 🚨 `REC-001` muc 7 — **JOB DOI SOAT THANH TOAN DINH KY**. 📌 Bien endpoint `doi-soat-loat`
        // (phai BAM TAY) thanh co che TU CHAY: neu chi co endpoint thi **IPN mat** => giao dich ket `Pending`
        // => 🔑 **tien da vao ma he thong khong biet** cho toi khi **tinh co** co nguoi bam.
        await context.AddBackgroundWorkerAsync<eCommerce.Application.PaymentHub.PaymentReconciliationWorker>();
    }
}
