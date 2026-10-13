using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.EInvoice.EntityFrameworkCore;

/// <summary>Module hạ tầng lưu trữ của `EInvoice`.</summary>
[DependsOn(
    typeof(EInvoiceDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class EInvoiceEntityFrameworkCoreModule : AbpModule
{
}
