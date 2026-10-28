using eCommerce.Application.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Inventory;

/// <summary>Đọc tồn kho cho UI (`/Kho`) — 🔒 chỉ đọc, không mở đường ghi tồn từ màn hình.</summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class TonKhoAppService : ApplicationService, ITonKhoAppService
{
    private readonly IRepository<InventoryItem, Guid> _ton;
    private readonly IRepository<Warehouse, Guid> _kho;
    private readonly IAsyncQueryableExecuter _thucThi;

    public TonKhoAppService(
        IRepository<InventoryItem, Guid> ton,
        IRepository<Warehouse, Guid> kho,
        IAsyncQueryableExecuter thucThi)
    {
        _ton = ton;
        _kho = kho;
        _thucThi = thucThi;
    }

    public async Task<List<TonKhoDto>> GetAsync(Guid? warehouseId = null, string? sku = null)
    {
        var q = await _ton.GetQueryableAsync();

        if (warehouseId is { } khoId)
        {
            q = q.Where(x => x.WarehouseId == khoId);
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var tuKhoa = sku.Trim();
            q = q.Where(x => x.Sku.Contains(tuKhoa));
        }

        var ds = await _thucThi.ToListAsync(q);
        var cacKho = await _kho.GetListAsync();
        var theoId = cacKho.ToDictionary(x => x.Id);

        return ds
            .OrderBy(x => theoId.TryGetValue(x.WarehouseId, out var k) ? k.Code : string.Empty, StringComparer.Ordinal)
            .ThenBy(x => x.Sku, StringComparer.Ordinal)
            .Select(x => new TonKhoDto
            {
                WarehouseId = x.WarehouseId,
                WarehouseCode = theoId.TryGetValue(x.WarehouseId, out var k) ? k.Code : "(khong-co)",
                WarehouseName = theoId.TryGetValue(x.WarehouseId, out var k2) ? k2.Name : "(khong-co)",
                Sku = x.Sku,
                OnHand = x.OnHand,
                Reserved = x.Reserved,
                Available = x.Available
            })
            .ToList();
    }
}
