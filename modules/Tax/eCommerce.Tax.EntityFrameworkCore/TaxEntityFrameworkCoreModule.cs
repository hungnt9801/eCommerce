using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace eCommerce.Tax.EntityFrameworkCore;

/// <summary>
/// Lớp EF Core của module <b>Tax</b> — chỉ cung cấp hàm mapping <c>ConfigureTax()</c>.
///
/// 📌 Module KHÔNG sở hữu DbContext riêng: modular monolith dùng MỘT DbContext hợp nhất ở
/// <c>src/eCommerce.EntityFrameworkCore</c>. App gọi <c>builder.ConfigureTax()</c> và khai báo
/// <c>DbSet</c> cho entity của module — nếu quên khai báo <c>DbSet</c> thì runtime báo
/// <c>"The requested service 'IRepository&lt;TaxClassification, Guid&gt;' has not been registered."</c>
/// (lỗi này đã gặp thật ở Phase 0 với module ChannelHub).
///
/// Vì sao KHÔNG đăng ký DbContext riêng ở đây: một DbContext "rỗng" thứ hai sẽ tạo thêm
/// migration context và lần theo connection string ⇒ gây lỗi khó hiểu mà không đem lại lợi ích gì.
/// Khi tách microservice mới đổi module này sang DbContext thật của nó.
/// </summary>
[DependsOn(
    typeof(TaxDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class TaxEntityFrameworkCoreModule : AbpModule
{
}
