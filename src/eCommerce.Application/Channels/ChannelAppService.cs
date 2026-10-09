using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.Connectors;
using eCommerce.Channels.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Security.Encryption;
using Volo.Abp.Users;

namespace eCommerce.Channels;

/// <summary>
/// App service quản lý kết nối shop.
///
/// Điểm đáng chú ý về kiến trúc: app service này CHỈ biết
/// <see cref="IChannelConnectorFactory"/> của ChannelHub — không biết Shopee/Lazada.
/// Nhờ vậy thêm sàn mới không phải sửa file này.
/// </summary>
public class ChannelAppService : ApplicationService, IChannelAppService
{
    private readonly IRepository<ChannelShop, Guid> _channelShopRepository;
    private readonly IChannelConnectorFactory _connectorFactory;
    private readonly IStringEncryptionService _encryptionService;
    private readonly IGuidGenerator _guidGenerator;

    public ChannelAppService(
        IRepository<ChannelShop, Guid> channelShopRepository,
        IChannelConnectorFactory connectorFactory,
        IStringEncryptionService encryptionService,
        IGuidGenerator guidGenerator)
    {
        _channelShopRepository = channelShopRepository;
        _connectorFactory = connectorFactory;
        _encryptionService = encryptionService;
        _guidGenerator = guidGenerator;
    }

    public async Task<PagedResultDto<ChannelShopDto>> GetShopsAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _channelShopRepository.GetQueryableAsync();

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var shops = await AsyncExecuter.ToListAsync(
            queryable.OrderBy(x => x.ChannelCode).ThenBy(x => x.ShopName)
                     .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<ChannelShopDto>(totalCount, shops.Select(ToDto).ToList());
    }

    public async Task<ChannelShopDto> GetShopAsync(Guid id)
    {
        var shop = await _channelShopRepository.GetAsync(id);
        return ToDto(shop);
    }

    /// <summary>
    /// Kết nối shop. Token được mã hoá NGAY tại đây bằng <see cref="IStringEncryptionService"/>
    /// trước khi lưu — entity chỉ giữ bản đã mã hoá, và DTO không bao giờ trả token ra ngoài.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Channels.Manage)]
    public async Task<ChannelShopDto> ConnectAsync(ConnectChannelShopDto input)
    {
        if (!_connectorFactory.IsSupported(input.ChannelCode))
        {
            throw new ArgumentException(
                $"Chưa hỗ trợ kênh '{input.ChannelCode}'. Các kênh đã có connector: xem ChannelConnectorFactory.",
                nameof(input.ChannelCode));
        }

        var existing = await _channelShopRepository.FirstOrDefaultAsync(
            x => x.ChannelCode == input.ChannelCode && x.ExternalShopId == input.ExternalShopId);

        ChannelShop shop;

        if (existing is null)
        {
            shop = new ChannelShop(
                _guidGenerator.Create(),
                input.ChannelCode,
                input.ExternalShopId,
                input.ShopName)
            {
                Region = input.Region,
                AuthorizationExpiresAtUtc = input.AuthorizationExpiresAtUtc,
                IsActive = true
            };

            shop.SetTokens(
                accessTokenEncrypted: _encryptionService.Encrypt(input.AccessToken)!,
                refreshTokenEncrypted: string.IsNullOrWhiteSpace(input.RefreshToken) ? null : _encryptionService.Encrypt(input.RefreshToken),
                accessExpiresAtUtc: input.AccessTokenExpiresAtUtc,
                refreshExpiresAtUtc: input.RefreshTokenExpiresAtUtc);

            await _channelShopRepository.InsertAsync(shop, autoSave: true);
        }
        else
        {
            shop = existing;
            shop.ShopName = input.ShopName;
            shop.Region = input.Region;
            shop.AuthorizationExpiresAtUtc = input.AuthorizationExpiresAtUtc;
            shop.IsActive = true;

            shop.SetTokens(
                accessTokenEncrypted: _encryptionService.Encrypt(input.AccessToken)!,
                refreshTokenEncrypted: string.IsNullOrWhiteSpace(input.RefreshToken) ? null : _encryptionService.Encrypt(input.RefreshToken),
                accessExpiresAtUtc: input.AccessTokenExpiresAtUtc,
                refreshExpiresAtUtc: input.RefreshTokenExpiresAtUtc);

            await _channelShopRepository.UpdateAsync(shop, autoSave: true);
        }

        // Ghi chú thiết kế: KHÔNG cần "bật worker" ở đây.
        // Supervisor của ChannelHub quét shop đang hoạt động mỗi 60s và tự đăng ký dynamic worker.
        return ToDto(shop);
    }

    [Authorize(eCommerceConsts.Permissions.Channels.Manage)]
    public async Task<ChannelShopDto> SetActiveAsync(Guid id, bool isActive)
    {
        var shop = await _channelShopRepository.GetAsync(id);
        shop.IsActive = isActive;
        await _channelShopRepository.UpdateAsync(shop, autoSave: true);

        // Không cần huỷ worker thủ công: supervisor sẽ thấy shop không còn IsActive
        // và gọi RemoveAsync ở nhịp kế tiếp.
        return ToDto(shop);
    }

    /// <summary>
    /// Chạy đồng bộ ngay cho một shop. Trong skeleton, phần gọi API sàn đang mô phỏng
    /// (ShopeeConnector trả kết quả rỗng khi EnableLiveCalls = false).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Channels.Sync)]
    public async Task<ChannelSyncResultDto> SyncNowAsync(Guid id)
    {
        var shop = await _channelShopRepository.GetAsync(id);

        var connector = _connectorFactory.Get(shop.ChannelCode);

        var context = new ChannelShopContext
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

        var startedAt = DateTime.UtcNow;
        var pull = await connector.PullOrdersAsync(context);

        var result = new ChannelSyncResultDto
        {
            ChannelShopId = shop.Id,
            ChannelCode = shop.ChannelCode,
            OrdersPulled = pull.Orders.Count,
            RateLimited = pull.RateLimited,
            Message = pull.Message,
            StartedAtUtc = startedAt,
            FinishedAtUtc = DateTime.UtcNow
        };

        if (!pull.RateLimited)
        {
            shop.LastSyncedAtUtc = pull.NewSyncCursorUtc ?? DateTime.UtcNow;
            await _channelShopRepository.UpdateAsync(shop, autoSave: true);
        }

        return result;
    }

    /// <summary>
    /// Map thủ công entity → DTO.
    /// ⚠️ KHÔNG bao giờ copy trường token vào DTO, kể cả bản đã mã hoá.
    /// </summary>
    private static ChannelShopDto ToDto(ChannelShop shop) => new()
    {
        Id = shop.Id,
        ChannelCode = shop.ChannelCode,
        ExternalShopId = shop.ExternalShopId,
        ShopName = shop.ShopName,
        Region = shop.Region,
        IsActive = shop.IsActive,
        HasRefreshToken = !string.IsNullOrEmpty(shop.RefreshTokenEncrypted),
        AccessTokenExpiresAtUtc = shop.AccessTokenExpiresAtUtc,
        AuthorizationExpiresAtUtc = shop.AuthorizationExpiresAtUtc,
        LastSyncedAtUtc = shop.LastSyncedAtUtc,
        PushStatus = shop.PushStatus,
        LastError = shop.LastError,
        CreationTime = shop.CreationTime,
        LastModificationTime = shop.LastModificationTime
    };
}
