using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Promotions;

/// <summary>
/// Module <b>Promotions</b> — tầng Domain (`PRO-002`): aggregate `Promotion`, cây điều kiện AND/OR,
/// validator, registry rule thuần, mã lỗi + khoá phân tán + settings.
///
/// 🔒 Ranh giới (`PRO-002-arch` §2): Domain CHỈ tham chiếu `eCommerce.Kernel` + `eCommerce.Domain.Shared`;
/// KHÔNG tham chiếu Application/Host/module nghiệp vụ khác.
/// </summary>
[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(eCommerce.Kernel.eCommerceKernelModule),
    typeof(eCommerceDomainSharedModule)
)]
public class PromotionsDomainModule : AbpModule
{
}
