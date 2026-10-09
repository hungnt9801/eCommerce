using Microsoft.Extensions.DependencyInjection;

namespace eCommerce.ChannelHub.Connectors;

/// <summary>
/// Chọn connector theo mã kênh. Thêm sàn mới = thêm một class + một dòng ở đây,
/// KHÔNG sửa tầng đơn hàng/tồn kho.
/// </summary>
public class ChannelConnectorFactory : IChannelConnectorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _connectors;

    public ChannelConnectorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _connectors = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            [ShopeeConnector.Code] = typeof(ShopeeConnector)

            // Thêm sàn mới ở đây, ví dụ:
            // [LazadaConnector.Code] = typeof(LazadaConnector),
            // [TikTokShopConnector.Code] = typeof(TikTokShopConnector),
        };
    }

    public bool IsSupported(string channelCode) => _connectors.ContainsKey(channelCode);

    public IChannelConnector Get(string channelCode)
    {
        if (!_connectors.TryGetValue(channelCode, out var type))
        {
            throw new ArgumentException(
                $"Chưa có connector cho kênh '{channelCode}'. Thêm vào ChannelConnectorFactory.",
                nameof(channelCode));
        }

        return (IChannelConnector)_serviceProvider.GetRequiredService(type);
    }
}
