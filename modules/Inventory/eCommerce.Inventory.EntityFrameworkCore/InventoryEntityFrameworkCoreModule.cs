using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Inventory;

/// <summary>
/// **MODULE `Inventory` — ENTITYFRAMEWORKCORE** (tách khỏi host 24/10/2026, `P0 #1`).
/// 🚧 **CHƯA CHỨA MAPPING** — mapping `InvItems`/`InvStockLedger`… vẫn ở `eCommerceDbContext.ConfigureCommerce`;
/// chuyển sang đây là lát cắt kế tiếp (điều kiện: migration **RỖNG**). 📌 Đây là **nợ có mã**, không phải xong.
/// </summary>
public class InventoryEntityFrameworkCoreModule : AbpModule
{
}
