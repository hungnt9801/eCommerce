using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;

namespace eCommerce.Reporting;

/// <summary>
/// **MODULE `Reporting` — APPLICATION** (`WP-08`).
///
/// 🔒 **Phụ thuộc:** chỉ `Reporting.Application.Contracts` + `AbpDddApplicationModule`. Việc đọc entity
/// của `Pos`/`Sales`/`Inventory` nằm ở **project reference** của csproj (tầng Domain của chúng), không phải
/// ở `[DependsOn]` — để đồ thị module **không** kéo toàn bộ tầng Application của ba module kia vào mọi test.
/// </summary>
[DependsOn(
    typeof(ReportingApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundWorkersModule)
)]
public class ReportingApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 📌 Section `Reporting` (múi giờ, nhịp job, ngưỡng "sắp hết"). Mặc định trong `ReportingOptions`
        // đã đúng ⇒ không cấu hình vẫn chạy được; cấu hình chỉ để **đổi** hành vi.
        Configure<ReportingOptions>(context.Services.GetConfiguration().GetSection("Reporting"));
    }

    /// <summary>
    /// 🚨 **ĐĂNG KÝ WORKER — THIẾU DÒNG NÀY THÌ JOB KHÔNG BAO GIỜ CHẠY** và không có lỗi biên dịch nào
    /// (bài học `inv-001` §27.3; lưới an toàn `BackgroundWorkerCoCheTests`).
    /// </summary>
    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<ReportingAggregationWorker>();
    }
}
