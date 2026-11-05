using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.Catalog;

/// <summary>
/// **MODULE `Catalog` — TẦNG DOMAIN** (tách khỏi host 24/10/2026, `P0 #1`).
///
/// 🔒 **BẤT BIẾN CỦA VIỆC TÁCH MODULE (đừng phá):**
///   • **Namespace giữ nguyên `eCommerce.Catalog`** ⇒ tên bảng (`CatProducts`…), route, và mọi `using` cũ
///     **không đổi**; migration hiện tại vẫn đúng.
///   • Domain **KHÔNG** tham chiếu `eCommerce.Application.Contracts`/EF Core (quy ước #8).
///   • `CatalogMediaModule` vẫn là module riêng (đăng ký `IMediaStorage`) và được **depends** ở đây — lý do
///     lịch sử: quét DI tự động **không** đăng ký được nó (sự cố 16/10/2026, xem chú thích lớp đó).
///
/// 📌 **CÒN NỢ (ghi rõ, không nói quá):** mapping EF của Catalog **vẫn nằm** trong `eCommerceDbContext`
/// (`ConfigureCommerce`) ở host. Lát cắt này tách **Domain + Contracts + Application**; chuyển mapping sang
/// `eCommerce.Catalog.EntityFrameworkCore` là lát cắt kế tiếp (điều kiện: migration **rỗng**).
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule),
    typeof(CatalogMediaModule)
)]
public class CatalogDomainModule : AbpModule
{
}
