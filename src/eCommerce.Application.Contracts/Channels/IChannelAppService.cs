using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Channels.Dtos;

/// <summary>
/// Quản lý kết nối shop trên các kênh bán.
/// Triển khai thật nằm ở tầng Application; expose ra HTTP qua ABP auto API controllers.
/// </summary>
public interface IChannelAppService : IApplicationService
{
    Task<PagedResultDto<ChannelShopDto>> GetShopsAsync(PagedAndSortedResultRequestDto input);

    Task<ChannelShopDto> GetShopAsync(Guid id);

    /// <summary>Kết nối (hoặc cập nhật token cho) một shop. Token được mã hoá trước khi lưu.</summary>
    Task<ChannelShopDto> ConnectAsync(ConnectChannelShopDto input);

    /// <summary>Bật/tắt đồng bộ cho một shop.</summary>
    Task<ChannelShopDto> SetActiveAsync(Guid id, bool isActive);

    /// <summary>Chạy đồng bộ ngay (không chờ lịch nền).</summary>
    Task<ChannelSyncResultDto> SyncNowAsync(Guid id);
}
