using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Reconciliation;

/// <summary>
/// **MODULE `Reconciliation` — ENTITYFRAMEWORKCORE** (`P0 #1`, 24/10/2026).
/// 🚧 Mapping `ReconRuns`/`ReconRunMismatches` **còn ở host** (`ConfigureCommerce`) — chuyển sang đây là lát cắt
/// kế tiếp (điều kiện: migration **RỖNG**). 📌 Nợ có mã, không phải xong.
/// </summary>
public class ReconciliationEntityFrameworkCoreModule : AbpModule
{
}
