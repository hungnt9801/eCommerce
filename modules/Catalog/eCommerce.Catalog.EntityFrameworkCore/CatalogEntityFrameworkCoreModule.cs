using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Catalog;

/// <summary>
/// **MODULE `Catalog` — TẦNG ENTITYFRAMEWORKCORE** (tách khỏi host 24/10/2026, `P0 #1`).
///
/// 🚧 **CHƯA CHỨA MAPPING** — mapping của Catalog hiện vẫn nằm ở `eCommerceDbContext.ConfigureCommerce`
/// (host). Lý do của lát cắt này: tách **Domain + Contracts + Application** trước, **giữ nguyên lược đồ**
/// (điều kiện: `dotnet ef migrations add` cho ra migration **rỗng**).
/// 📌 Lát cắt kế tiếp: chuyển các block `builder.Entity<Catalog.*>(...)` từ host sang
/// `CatalogDbContextModelCreatingExtensions.ConfigureCatalog(builder)` trong project này, rồi gọi từ host.
/// 🔒 Chưa chuyển **không** có nghĩa "module đã đủ": đây là **nợ có mã**, không phải hoàn thành.
/// </summary>
public class CatalogEntityFrameworkCoreModule : AbpModule
{
}
