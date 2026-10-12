using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.Connectors;
using eCommerce.ChannelHub.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.ChannelHub.Sync;

/// <summary>
/// Đồng bộ MỘT shop: đảm bảo token hợp lệ → kéo đơn → phát sự kiện cho app xử lý.
///
/// 🎯 Nguyên tắc: ChannelHub KHÔNG tự tạo đơn hàng. Nó chỉ phát
/// <see cref="ChannelOrderPulledEto"/>; app subscribe và tạo Order (idempotent theo
/// TenantId + ChannelCode + ExternalOrderId). Nhờ vậy:
///   • module không phụ thuộc ngược vào app,
///   • tách microservice được về sau,
///   • và lỗi khi tạo đơn không làm hỏng con trỏ đồng bộ của kênh.
/// </summary>
public class ChannelShopSyncer
{
    private readonly IRepository<ChannelShop, Guid> _channelShopRepository;
    private readonly IChannelConnectorFactory _connectorFactory;
    private readonly ChannelTokenManager _tokenManager;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<ChannelShopSyncer> _logger;

    public ChannelShopSyncer(
        IRepository<ChannelShop, Guid> channelShopRepository,
        IChannelConnectorFactory connectorFactory,
        ChannelTokenManager tokenManager,
        IDistributedEventBus distributedEventBus,
        ILogger<ChannelShopSyncer> logger)
    {
        _channelShopRepository = channelShopRepository;
        _connectorFactory = connectorFactory;
        _tokenManager = tokenManager;
        _distributedEventBus = distributedEventBus;
        _logger = logger;
    }

    public async Task<ChannelSyncResult> SyncAsync(Guid channelShopId, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var result = new ChannelSyncResult { ChannelShopId = channelShopId, StartedAtUtc = startedAt };

        var shop = await _channelShopRepository.GetAsync(channelShopId, cancellationToken: cancellationToken);
        result.ChannelCode = shop.ChannelCode;

        try
        {
            var (shopContext, tokenRefreshed) = await _tokenManager.GetValidContextAsync(shop, cancellationToken);
            result.TokenRefreshed = tokenRefreshed;

            var connector = _connectorFactory.Get(shop.ChannelCode);
            var pull = await connector.PullOrdersAsync(shopContext, cancellationToken);

            if (pull.RateLimited)
            {
                // Sàn báo hết quota: KHÔNG retry dồn, chỉ ghi nhận để supervisor giãn nhịp.
                result.RateLimited = true;
                result.Message = pull.Message ?? "Sàn báo rate limit (HTTP 429).";
                _logger.LogWarning("Shop {ShopId} trên {ChannelCode} bị rate limit.", shop.ExternalShopId, shop.ChannelCode);
            }
            else
            {
                foreach (var order in pull.Orders)
                {
                    await _distributedEventBus.PublishAsync(new ChannelOrderPulledEto
                    {
                        TenantId = shop.TenantId,
                        ChannelShopId = shop.Id,
                        ChannelCode = shop.ChannelCode,
                        ExternalShopId = shop.ExternalShopId,
                        ExternalOrderId = order.ExternalOrderId,
                        PlacedAtUtc = order.PlacedAtUtc,
                        Currency = order.Currency,
                        ShippingFee = order.ShippingFee,
                        ChannelDiscount = order.ChannelDiscount,
                        BuyerNote = order.BuyerNote,
                        Lines = order.Lines.Select(l => new ChannelOrderPulledLineEto
                        {
                            ExternalLineId = l.ExternalLineId,
                            Sku = l.Sku,
                            Quantity = l.Quantity,
                            UnitPrice = l.UnitPrice,
                            DiscountAmount = l.DiscountAmount
                        }).ToList()
                    });
                }

                result.OrdersPulled = pull.Orders.Count;

                // Chỉ tiến con trỏ khi đã phát sự kiện thành công cho cả lô.
                shop.LastSyncedAtUtc = pull.NewSyncCursorUtc ?? DateTime.UtcNow;
                shop.LastError = null;
                await _channelShopRepository.UpdateAsync(shop, autoSave: true, cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result.Errors++;
            result.Message = ex.Message;

            // Ghi lỗi để hiển thị trên admin, nhưng KHÔNG nuốt exception ở tầng worker
            // (supervisor cần biết để log/cảnh báo).
            _logger.LogError(ex, "Đồng bộ shop {ShopId} trên {ChannelCode} thất bại.", shop.ExternalShopId, shop.ChannelCode);

            shop.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await _channelShopRepository.UpdateAsync(shop, autoSave: true, cancellationToken: cancellationToken);
        }

        result.FinishedAtUtc = DateTime.UtcNow;
        return result;
    }
}
