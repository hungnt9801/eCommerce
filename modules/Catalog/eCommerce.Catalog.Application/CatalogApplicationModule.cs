using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Catalog;

/// <summary>
/// **MODULE `Catalog` — TẦNG APPLICATION** (tách khỏi host 24/10/2026, `P0 #1`).
///
/// 📌 `ChannelDirectory` (hiện thực `IChannelDirectory`) **ở lại host**: nó đọc entity `Channel` của
/// `ChannelHub.Domain`, và module `Catalog` **không được** tham chiếu Domain của module khác (kế hoạch L2).
/// Host vừa thấy `ChannelHub.Domain` vừa thấy hợp đồng này ⇒ đặt hiện thực ở host là đúng chỗ.
/// 🔒 Hợp đồng `IChannelDirectory` nằm trong `Catalog.Application.Contracts` ⇒ module không phụ thuộc host.
/// </summary>
[DependsOn(
    typeof(CatalogApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class CatalogApplicationModule : AbpModule
{
}
