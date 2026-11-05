using eCommerce.Application.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
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

    /// <summary>
    /// 🆕 **`WP-04`** — tồn kho **9 chiều** có phân trang.
    ///
    /// 📌 **`Available`/`InWarehouse` tính Ở ĐÂY (không trong SQL):** chúng là **property suy ra** trên entity
    /// (`=>`), EF **không** dịch được vào `SELECT` — cố dịch sẽ thành lỗi runtime. Vì vậy phân trang chạy ở DB
    /// (Skip/Take trên cột thật), rồi map 12 số trong bộ nhớ. 📌 Trang **50 dòng** ⇒ chi phí không đáng kể.
    /// </summary>
    public async Task<PagedResultDto<StockBalanceDto>> GetBalancesAsync(
        Guid? warehouseId = null, string? sku = null, int skipCount = 0, int maxResultCount = 50)
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

        var tong = await _thucThi.CountAsync(q);

        // 🔒 Sắp xếp TẤT ĐỊNH (kho, SKU) rồi mới Skip/Take: không sắp thì hai lần gọi cùng `skip` có thể trả
        // **hai tập khác nhau** (PostgreSQL không bảo đảm thứ tự) ⇒ bảng phân trang bị **mất/lặp dòng**.
        // ⚠️ **KHÔNG truyền `StringComparer.Ordinal` vào `ThenBy` trên `IQueryable`**: EF **không dịch được**
        // comparer ra SQL ⇒ `500` (*"could not be translated"*). Đã đo thật bằng probe HTTP (`WP-04`).
        var ds = await _thucThi.ToListAsync(
            q.OrderBy(x => x.WarehouseId).ThenBy(x => x.Sku)
                .Skip(skipCount).Take(maxResultCount));

        var cacKho = await _kho.GetListAsync();
        var theoId = cacKho.ToDictionary(x => x.Id);

        var items = ds.Select(x => new StockBalanceDto
        {
            WarehouseId = x.WarehouseId,
            WarehouseCode = theoId.TryGetValue(x.WarehouseId, out var k) ? k.Code : "(khong-co)",
            WarehouseName = theoId.TryGetValue(x.WarehouseId, out var k2) ? k2.Name : "(khong-co)",
            Sku = x.Sku,
            OnHand = x.OnHand,
            Reserved = x.Reserved,
            Transferring = x.Transferring,
            Shipping = x.Shipping,
            Holding = x.Holding,
            Damaged = x.Damaged,
            Warranty = x.Warranty,
            WarrantyHolding = x.WarrantyHolding,
            PreOrder = x.PreOrder,
            WaitingInbound = x.WaitingInbound,
            InWarehouse = x.InWarehouse,
            Available = x.Available
        }).ToList();

        return new PagedResultDto<StockBalanceDto>(tong, items);
    }
}
