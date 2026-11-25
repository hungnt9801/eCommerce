using eCommerce.Address.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Address.Caching;

/// <summary>
/// Handler xoá cache **đa instance** khi một lượt nạp danh mục đã commit (`ADDR-001` BR-014, `D-242`).
///
/// 🔒 **Chỉ xoá cache, KHÔNG ghi DB** ⇒ **không** cần tự mở UoW; xử lý trùng ⇒ **vô hại**.
/// 📌 Nếu sau này handler ghi DB ⇒ **bắt buộc** `IUnitOfWorkManager.Begin(requiresNew: true, isTransactional: true)`
/// (E2 của `abp-module-rules`: handler `IDistributedEventBus` ghi dữ liệu phải mở UoW riêng).
/// </summary>
public class AddressCacheInvalidatedHandler
    : IDistributedEventHandler<AddressCacheInvalidatedEto>, ITransientDependency
{
    private readonly AddressCatalogCache _cache;
    private readonly ILogger<AddressCacheInvalidatedHandler> _logger;

    public AddressCacheInvalidatedHandler(
        AddressCatalogCache cache,
        ILogger<AddressCacheInvalidatedHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleEventAsync(AddressCacheInvalidatedEto eventData)
    {
        await _cache.XoaAsync(eventData.SchemaVersion, eventData.ParentCodes);

        _logger.LogInformation(
            "Đã xoá cache danh mục ĐVHC sau lượt nạp: phiên bản {PhienBan}, {SoKhoa} khoá (parentCode).",
            eventData.SchemaVersion, eventData.ParentCodes.Count);
    }
}
