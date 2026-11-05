using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Pos;

/// <summary>
/// **MODULE `Pos` — TẦNG DOMAIN** (`WP-06`, `07 §WP-06`).
///
/// 📌 Bounded context **riêng** (`D-18`): hoá đơn bán lẻ + ca thu ngân **không** nằm trong `Sales` vì
/// chúng không có vòng đời giao hàng và có bất biến **tiền mặt theo ca** — trộn vào `Order` sẽ buộc
/// state machine 18 trạng thái phải học một loại chứng từ không đi qua nó.
///
/// 🔒 **Phụ thuộc:** chỉ `Domain.Shared` + `Kernel`. **Không** tham chiếu `Inventory.Domain` — mọi đường ghi
/// tồn đi qua **hợp đồng** `IPosStockGateway` (tầng Application), đúng luật `03 §2`.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class PosDomainModule : AbpModule
{
}
