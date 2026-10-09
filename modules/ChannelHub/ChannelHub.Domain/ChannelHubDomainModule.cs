using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace eCommerce.ChannelHub;

/// <summary>
/// Domain của module <b>ChannelHub</b> — lớp đồng bộ đa kênh.
///
/// Module này sở hữu entity của mình (Channel / ChannelShop / ChannelListing) và KHÔNG
/// tham chiếu domain của app — nhờ vậy ChannelHub có thể tách thành microservice riêng
/// mà không phải viết lại (đúng hướng dẫn modularity của ABP).
/// </summary>
[DependsOn(typeof(AbpDddDomainModule))]
public class ChannelHubDomainModule : AbpModule
{
}
