using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Reporting;

/// <summary>**MODULE `Reporting` — ENTITYFRAMEWORKCORE** (`WP-08`) — mapping gọi từ `eCommerceDbContext`.</summary>
[DependsOn(typeof(AbpEntityFrameworkCoreModule))]
public class ReportingEntityFrameworkCoreModule : AbpModule
{
}
