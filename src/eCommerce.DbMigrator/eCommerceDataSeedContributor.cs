using eCommerce.ChannelHub.Channels;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace eCommerce.DbMigrator;

/// <summary>
/// Seed dữ liệu nền: danh sách kênh bán. Phải idempotent — chạy lại nhiều lần không tạo trùng.
/// </summary>
public class eCommerceDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Channel, Guid> _channelRepository;
    private readonly IGuidGenerator _guidGenerator;

    public eCommerceDataSeedContributor(
        IRepository<Channel, Guid> channelRepository,
        IGuidGenerator guidGenerator)
    {
        _channelRepository = channelRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Code kênh khớp với IChannelConnector.ChannelCode ("shopee") và ChannelConnectorFactory.
        await EnsureChannelAsync("web", "Website bán hàng", type: 0, holdsInventory: false);
        await EnsureChannelAsync("shopee", "Shopee", type: 1, holdsInventory: true);
        await EnsureChannelAsync("lazada", "Lazada", type: 1, holdsInventory: true);
        await EnsureChannelAsync("tiktok", "TikTok Shop", type: 1, holdsInventory: true);
    }

    private async Task EnsureChannelAsync(string code, string name, int type, bool holdsInventory)
    {
        var existing = await _channelRepository.FirstOrDefaultAsync(x => x.Code == code);
        if (existing is not null)
        {
            return;
        }

        var channel = new Channel(_guidGenerator.Create(), code, name, type)
        {
            IsActive = true,
            HoldsInventory = holdsInventory
        };

        await _channelRepository.InsertAsync(channel, autoSave: true);
    }
}
