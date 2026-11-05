using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Pos;

/// <summary>**MODULE `Pos` — ENTITYFRAMEWORKCORE** (`WP-06`) — mapping gọi từ `eCommerceDbContext`.</summary>
[DependsOn(typeof(AbpEntityFrameworkCoreModule))]
public class PosEntityFrameworkCoreModule : AbpModule
{
}
