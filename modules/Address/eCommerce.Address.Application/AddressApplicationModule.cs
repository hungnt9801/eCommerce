using Volo.Abp.Application;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace eCommerce.Address.Application;

/// <summary>
/// Module tầng Application của `Address`.
///
/// 🚨 BÀI HỌC ĐÃ TRẢ GIÁ (14/10/2026): tạo project + viết `AdministrativeUnitAppService` là **CHƯA ĐỦ**.
/// Thiếu class module này thì assembly **không nằm trong ĐỒ THỊ MODULE** ⇒ ABP:
///   • **không nạp** assembly ⇒ không quét được app service (DI không đăng ký),
///   • và `ConventionalControllers` (kể cả bản tự động của C16, vì bản đó chỉ thấy assembly **đã nạp**)
///     **không** tạo endpoint.
/// Kết quả: API **trả `404` body rỗng** mà **không có lỗi biên dịch nào** — đúng y triệu chứng đã gặp ở C8.
///
/// 🔒 QUY TẮC RÚT RA: mỗi project tầng Application của module **PHẢI** có một `AbpModule`, và module đó
/// **PHẢI** được khai trong `[DependsOn]` của Host. Đây là điều kiện **bắt buộc**, không phải tuỳ chọn.
/// </summary>
[DependsOn(
    typeof(AddressDomainModule),
    typeof(AbpDddApplicationModule)
)]
public class AddressApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // ⚠️ `AddHttpClient` KHÔNG đặt ở đây: project thư viện này không có gói `Microsoft.Extensions.Http`.
        // Client HTTP được đăng ký ở **Host** — đúng mẫu đã có cho `TaxAuthorityClient`.
        // Ở đây chỉ nối interface về đúng lớp hiện thực.
        context.Services.AddTransient<Sync.IKhungDonViHanhChinhSource>(
            sp => sp.GetRequiredService<ProvincesOpenApiSource>());
    }

    /// <summary>
    /// Đăng ký job đồng bộ danh mục ĐVHC.
    /// ⚠️ Thiếu dòng này thì **KHÔNG có lỗi build**, app vẫn khởi động, nhưng job **im lặng không bao giờ chạy**
    /// (đây là dạng lỗi đã gặp nhiều lần trong dự án).
    /// </summary>
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<AdministrativeUnitSyncWorker>();
    }
}