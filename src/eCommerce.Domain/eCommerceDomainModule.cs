using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce;

/// <summary>
/// Tầng domain của ứng dụng: entity, domain service, repository interface, enum nghiệp vụ.
///
/// ⚠️ QUY TẮC KIẾN TRÚC (rút ra từ các nền tảng đã khảo sát):
///  1. Domain KHÔNG được tham chiếu EF Core, HTTP, hay SDK của sàn TMĐT.
///  2. Mọi trường TIỀN TỆ dùng <c>decimal</c>, KHÔNG dùng <c>double</c>
///     (GrandNode 2 dùng double cho tiền — đây là lỗi chúng ta không lặp lại).
///  3. Tồn kho là append-only ledger; không UPDATE số lượng trực tiếp.
///  4. Mọi aggregate có multi-tenant đều implement <see cref="Volo.Abp.MultiTenancy.IMultiTenant"/>.
/// </summary>
[DependsOn(
    typeof(eCommerceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class eCommerceDomainModule : AbpModule
{
}
