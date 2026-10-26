using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Inventory;

/// <summary>
/// **MODULE `Inventory` — APPLICATION** (tách khỏi host 24/10/2026, `P0 #1`).
/// 📌 `ChinhSachHanMucAppService` **ở lại host** vì đọc entity `Channel` của ChannelHub — cùng lý do
/// `ChannelDirectory` của Catalog (module không tham chiếu Domain module khác).
/// </summary>
[DependsOn(
    typeof(InventoryApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class InventoryApplicationModule : AbpModule
{
}
