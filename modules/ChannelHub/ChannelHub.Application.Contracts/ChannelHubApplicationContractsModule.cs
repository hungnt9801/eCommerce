using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace eCommerce.ChannelHub;

/// <summary>
/// Hợp đồng của ChannelHub: abstraction connector + model trung lập + app service interface.
///
/// 🎯 Đây là điểm mấu chốt của kiến trúc: tầng nghiệp vụ ở app CHỈ biết
/// <c>IChannelConnector</c>, không biết Shopee/Lazada. Thêm sàn mới = thêm một
/// implementation + một dòng đăng ký DI, không sửa tầng đơn hàng/tồn kho.
/// </summary>
[DependsOn(
    typeof(ChannelHubDomainModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class ChannelHubApplicationContractsModule : AbpModule
{
}
