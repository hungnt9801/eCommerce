using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Inventory;

/// <summary>
/// **MODULE `Inventory` — TẦNG DOMAIN** (tách khỏi host 24/10/2026, `P0 #1`).
///
/// 🔒 **GIỮ NGUYÊN namespace `eCommerce.Inventory`** ⇒ tên bảng (`InvItems`…), route, `using` cũ không đổi.
/// 📌 Phần **đối soát kho** (`ReconRun`, `DoiSoatTonKho`, `InventoryReconciliationService`, `NguongLech`,
/// `DoiSoatRetention`) **còn ở host** — sẽ chuyển sang module `Reconciliation` (lát cắt riêng), không gộp
/// vào đây để tránh trộn hai bounded context trong một bước.
/// 🚨 `ChinhSach`/`TenChinhSach` đã chuyển về `shared/eCommerce.Kernel` (namespace `eCommerce.Settings`)
/// ⇒ module **không** phải tham chiếu host Domain (module → host là CẤM).
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class InventoryDomainModule : AbpModule
{
}
