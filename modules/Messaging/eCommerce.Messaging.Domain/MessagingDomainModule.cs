using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Messaging;

/// <summary>
/// Module tầng Domain của `Messaging` — sở hữu hợp đồng chống trùng ở biên vào (`XC-002`)
/// và **bộ dò độ phủ sự kiện** dùng chung (`XC-002`/lỗi #22).
/// </summary>
[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(eCommerce.Kernel.eCommerceKernelModule)
)]
public class MessagingDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 🚨 ĐĂNG KÝ TƯỜNG MINH (có chủ ý — không dựa vào đăng ký quy ước):
        // `IEventCoverageProbe` là **cổng an toàn chống mất dữ liệu** (fail-closed ở lỗi #22).
        // Nếu nó biến mất khỏi DI thì nơi dùng sẽ **không resolve được ⇒ ứng dụng không khởi động**
        // (ồn ào, phát hiện ngay) — thay vì âm thầm quay lại hành vi "publish cho không ai".
        context.Services.AddSingleton<EventCoverage.IEventCoverageProbe, EventCoverage.EventCoverageProbe>();
    }
}
