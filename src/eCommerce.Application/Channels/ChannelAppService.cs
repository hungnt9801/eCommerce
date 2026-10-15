using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.Connectors;
using eCommerce.ChannelHub.Sync;
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
///
/// 🔐 BẢO MẬT — BÀI HỌC KHI CHẠY THẬT:
/// ABP KHÔNG tự bảo vệ application service. Nếu chỉ đặt [Authorize] ở một vài method thì
/// các method còn lại (ví dụ GetShopsAsync) trở thành API CÔNG KHAI — đã quan sát được
/// đúng như vậy khi chạy thử. Vì thế đặt <b>[Authorize] ở cấp CLASS</b> để mặc định
/// mọi method đều yêu cầu đăng nhập; method nào cần quyền cụ thể thì ghi thêm.
/// </summary>
[Authorize]
public class ChannelAppService : ApplicationService, IChannelAppService
{
    private readonly IRepository<ChannelShop, Guid> _channelShopRepository;
    private readonly IChannelConnectorFactory _connectorFactory;
    private readonly IStringEncryptionService _encryptionService;
    private readonly IGuidGenerator _guidGenerator;

    /// <summary>
    /// 🚨 Cổng đồng bộ DÙNG CHUNG với worker nền — xem ghi chú ở <see cref="SyncNowAsync"/>.
    /// Cố ý **không** tự kéo đơn ở đây: hai đường đồng bộ song song là hai cơ hội để lệch nhau.
    /// </summary>
    private readonly IChannelShopSyncService _channelShopSyncService;

    public ChannelAppService(
        IRepository<ChannelShop, Guid> channelShopRepository,
        IChannelConnectorFactory connectorFactory,
        IStringEncryptionService encryptionService,
        IGuidGenerator guidGenerator,
        IChannelShopSyncService channelShopSyncService)
    {
        _channelShopRepository = channelShopRepository;
        _connectorFactory = connectorFactory;
        _encryptionService = encryptionService;
        _guidGenerator = guidGenerator;
        _channelShopSyncService = channelShopSyncService;
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
    /// Chạy đồng bộ ngay cho một shop (nút "Đồng bộ ngay" trên admin).
    ///
    /// 🚨 **BIẾN THỂ THỨ HAI CỦA LỖI #22 — ĐÃ SỬA (14/10/2026).**
    /// Trước đây method này **tự kéo đơn rồi tự tiến con trỏ** (`shop.LastSyncedAtUtc = …`) mà
    /// **không hề phát sự kiện** cho bất kỳ ai ⇒ mọi đơn vừa kéo bị **vứt bỏ**, con trỏ vẫn tiến
    /// ⇒ sàn **không gửi lại** ⇒ **đơn mất vĩnh viễn**, và API trả về *"thành công, N đơn"*.
    /// Đây là đường **do người vận hành bấm** — tức đúng cái nút sẽ được bấm khi thấy đồng bộ có vẻ sai.
    ///
    /// Nay đi qua **cùng một cổng** với worker nền (<see cref="IChannelShopSyncService"/>) ⇒ chỉ còn
    /// **MỘT** logic đồng bộ và **MỘT** chỗ quyết định fail-closed.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Channels.Sync)]
    public async Task<ChannelSyncResultDto> SyncNowAsync(Guid id)
    {
        var ketQua = await _channelShopSyncService.SyncAsync(id);

        return new ChannelSyncResultDto
        {
            ChannelShopId = ketQua.ChannelShopId,
            ChannelCode = ketQua.ChannelCode,
            OrdersPulled = ketQua.OrdersPulled,
            ListingsPushed = ketQua.ListingsPushed,
            Errors = ketQua.Errors,
            RateLimited = ketQua.RateLimited,
            TokenRefreshed = ketQua.TokenRefreshed,
            BiChanDoChuaCoNoiNhanDon = ketQua.BiChanDoChuaCoNoiNhanDon,
            LyDoChan = ketQua.LyDoChan,
            Message = ketQua.Message,
            StartedAtUtc = ketQua.StartedAtUtc,
            FinishedAtUtc = ketQua.FinishedAtUtc
        };
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
