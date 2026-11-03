using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace eCommerce;

/// <summary>
/// Tầng HTTP API **tường minh** cho Web Admin (`D-13`, `D-14`).
///
/// 🔑 **VÌ SAO CÓ PROJECT NÀY (không nhét controller vào Host):**
/// * Host là nơi *wiring* hạ tầng (Redis, OpenIddict, health, CORS) — trộn 20 controller vào đó thì
///   không test được controller mà không dựng cả Host.
/// * `/api/v1/**` là **hợp đồng công khai** với Next.js ⇒ phải có chỗ riêng để version, review và
///   sinh OpenAPI ổn định (`OperationId` không đổi khi refactor Host).
/// * `/api/app/**` (ABP conventional controller) **giữ nguyên** — Razor và tích hợp cũ không bị ảnh hưởng.
/// </summary>
[DependsOn(
    typeof(AbpAspNetCoreMvcModule),
    typeof(eCommerceApplicationContractsModule),
    typeof(Catalog.CatalogApplicationContractsModule))]
public class eCommerceHttpApiModule : AbpModule
{
}
