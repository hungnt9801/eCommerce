using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Pricing;

/// <summary>
/// Module tầng Domain của **Pricing** (`PRC-001` + `PRC-002`).
///
/// 🚨 **RANH GIỚI MODULE (ghi rõ để không ai vi phạm):**
///   • Pricing **KHÔNG** tham chiếu `eCommerce.Catalog.*` (`PRC-001` §15.1 **Q1**) — Pricing làm việc theo **`Sku`**
///     (chuỗi), không theo `ProductVariantId`. Đây là **quyết định có chủ ý**: giá là dữ liệu **thương mại**,
///     không phải dữ liệu **sản phẩm**; ràng buộc "SKU phải tồn tại" được kiểm ở **biên vào** (app service
///     hoặc `SAL-002`), **không** bằng FK — nhờ vậy Pricing không bị khoá cứng vào Catalog.
///   • `CostPrice` (giá vốn) **cấm** lộ ra storefront (`PRC-001` BR-013).
///   • Mọi làm tròn **chỉ** qua `IRoundingPolicy` của `XC-001`; **cấm** `decimal.Round`/`Math.Round` trong module
///     này (`PRC-001` NFR-004/`PRC-002` ADR-05).
/// </summary>
[DependsOn(typeof(AbpDddDomainModule), typeof(eCommerce.Kernel.eCommerceKernelModule))]
public class PricingDomainModule : AbpModule
{
}
