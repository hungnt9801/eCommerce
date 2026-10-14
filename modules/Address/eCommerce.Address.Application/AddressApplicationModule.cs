using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.Address.Application;

/// <summary>
/// Module tầng Application của `Address`.
///
/// 🚨 BÀI HỌC ĐÃ TRẢ GIÁ (14/10/2026): tạo project + viết `AdministrativeUnitAppService` là **CHƯA ĐỦ**.
/// Thiếu class module này thì assembly **không nằm trong ĐỒ THỊ MODULE** ⇒ ABP:
///   • **không nạp** assembly ⇒ không quét được app service (DI không đăng ký),
///   • và `ConventionalControllers` (kể cả bản tự động của C16, vì bản đó chỉ thấy assembly **đã nạp**)
///     **không** tạo endpoint.
/// Kết quả: API **trả `404` body rỗng** mà **không có lỗi biên dịch nào** — đúng y triệu chứng đã gặp ở C8.
///
/// 🔒 QUY TẮC RÚT RA: mỗi project tầng Application của module **PHẢI** có một `AbpModule`, và module đó
/// **PHẢI** được khai trong `[DependsOn]` của Host. Đây là điều kiện **bắt buộc**, không phải tuỳ chọn.
/// </summary>
[DependsOn(
    typeof(AddressDomainModule),
    typeof(AbpDddApplicationModule)
)]
public class AddressApplicationModule : AbpModule
{
}
