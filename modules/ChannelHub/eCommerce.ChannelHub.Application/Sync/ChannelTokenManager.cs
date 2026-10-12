using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.Connectors;
using eCommerce.ChannelHub.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Uow;

namespace eCommerce.ChannelHub.Sync;

/// <summary>
/// Quản lý vòng đời token của shop — đây là chỗ dễ mất dữ liệu nhất khi tích hợp sàn.
///
/// 🔒 VÌ SAO PHẢI CÓ DISTRIBUTED LOCK:
/// Shopee refresh token CÓ ROTATE. Nếu hai worker (hoặc hai instance app) refresh cùng lúc,
/// request thứ hai dùng refresh token CŨ đã bị vô hiệu → shop mất kết nối hoàn toàn.
/// Vì vậy: mọi lần refresh đều phải nằm trong lock per-shop, và phải DOUBLE-CHECK bên trong lock
/// (worker khác có thể đã refresh xong trước khi ta giành được lock).
/// </summary>
public class ChannelTokenManager
{
    private readonly IRepository<ChannelShop, Guid> _channelShopRepository;
    private readonly IChannelConnectorFactory _connectorFactory;
    private readonly IStringEncryptionService _encryptionService;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IOptions<ChannelHubOptions> _options;
    private readonly ILogger<ChannelTokenManager> _logger;

    public ChannelTokenManager(
        IRepository<ChannelShop, Guid> channelShopRepository,
        IChannelConnectorFactory connectorFactory,
        IStringEncryptionService encryptionService,
        IAbpDistributedLock distributedLock,
        IDistributedEventBus distributedEventBus,
        IOptions<ChannelHubOptions> options,
        ILogger<ChannelTokenManager> logger)
    {
        _channelShopRepository = channelShopRepository;
        _connectorFactory = connectorFactory;
        _encryptionService = encryptionService;
        _distributedLock = distributedLock;
        _distributedEventBus = distributedEventBus;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Trả về context đã có access token hợp lệ; tự refresh nếu cần.
    /// </summary>
    public async Task<(ChannelShopContext Context, bool TokenRefreshed)> GetValidContextAsync(
        ChannelShop shop,
        CancellationToken cancellationToken = default)
    {
        var safetyMargin = TimeSpan.FromMinutes(_options.Value.TokenRefreshSafetyMarginMinutes);
        var now = DateTime.UtcNow;

        if (!shop.NeedsTokenRefresh(now, safetyMargin))
        {
            return (ToContext(shop), false);
        }

        var lockName = ChannelHubConsts.LockKeys.ChannelTokenRefresh(shop.ChannelCode, shop.ExternalShopId);

        await using var handle = await _distributedLock.TryAcquireAsync(
            lockName,
            TimeSpan.FromSeconds(_options.Value.LockTimeoutSeconds),
            cancellationToken);

        if (handle is null)
        {
            // Không giành được lock: worker khác đang refresh. Không được refresh song song
            // (sẽ làm mất token). Bỏ lượt này và để nhịp sau xử lý.
            _logger.LogWarning(
                "Không giành được lock {LockName}; bỏ qua lượt đồng bộ này (worker khác đang refresh token).",
                lockName);

            return (ToContext(shop), false);
        }

        // Double-check bên trong lock: có thể worker khác đã refresh xong trước khi ta vào.
        var fresh = await _channelShopRepository.GetAsync(shop.Id, cancellationToken: cancellationToken);

        if (!fresh.NeedsTokenRefresh(DateTime.UtcNow, safetyMargin))
        {
            return (ToContext(fresh), false);
        }

        var connector = _connectorFactory.Get(fresh.ChannelCode);
        var result = await connector.RefreshTokenAsync(ToContext(fresh), cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.AccessToken))
        {
            fresh.LastError = result.Error ?? "Refresh token thất bại.";

            await _channelShopRepository.UpdateAsync(fresh, autoSave: true, cancellationToken: cancellationToken);

            throw new BusinessException(ChannelHubErrorCodes.ChannelTokenExpired)
                .WithData("ChannelCode", fresh.ChannelCode)
                .WithData("ExternalShopId", fresh.ExternalShopId);
        }

        // Ghi CẢ access token MỚI và refresh token MỚI (rotate) trong một lần update.
        fresh.SetTokens(
            accessTokenEncrypted: _encryptionService.Encrypt(result.AccessToken)!,
            refreshTokenEncrypted: string.IsNullOrEmpty(result.RefreshToken) ? null : _encryptionService.Encrypt(result.RefreshToken),
            accessExpiresAtUtc: result.AccessTokenExpiresAtUtc,
            refreshExpiresAtUtc: result.RefreshTokenExpiresAtUtc);

        await _channelShopRepository.UpdateAsync(fresh, autoSave: true, cancellationToken: cancellationToken);

        await _distributedEventBus.PublishAsync(new ChannelShopTokenRefreshedEto
        {
            TenantId = fresh.TenantId,
            ChannelShopId = fresh.Id,
            ChannelCode = fresh.ChannelCode,
            ExternalShopId = fresh.ExternalShopId,
            AccessTokenExpiresAtUtc = result.AccessTokenExpiresAtUtc,
            RefreshedAtUtc = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Đã refresh token cho shop {ShopId} trên kênh {ChannelCode}; access token hết hạn lúc {ExpiresAtUtc}.",
            fresh.ExternalShopId, fresh.ChannelCode, result.AccessTokenExpiresAtUtc);

        return (ToContext(fresh), true);
    }

    /// <summary>Giải mã token để dùng trong bộ nhớ. KHÔNG log giá trị trả về.</summary>
    private ChannelShopContext ToContext(ChannelShop shop) => new()
    {
        ChannelShopId = shop.Id,
        TenantId = shop.TenantId,
        ChannelCode = shop.ChannelCode,
        ExternalShopId = shop.ExternalShopId,
        Region = shop.Region,
        AccessToken = string.IsNullOrEmpty(shop.AccessTokenEncrypted)
            ? null
            : _encryptionService.Decrypt(shop.AccessTokenEncrypted),
        RefreshToken = string.IsNullOrEmpty(shop.RefreshTokenEncrypted)
            ? null
            : _encryptionService.Decrypt(shop.RefreshTokenEncrypted),
        LastSyncedAtUtc = shop.LastSyncedAtUtc
    };
}
