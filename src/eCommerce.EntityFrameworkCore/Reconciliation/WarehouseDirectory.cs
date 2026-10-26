using eCommerce.Inventory;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace eCommerce.Reconciliation;

/// <summary>Hiện thực `IWarehouseDirectory` ở host (được phép thấy `Inventory.Domain`); chỉ **đọc** `Warehouse.Code`.</summary>
public class WarehouseDirectory : IWarehouseDirectory, ITransientDependency
{
    private readonly IRepository<Warehouse, Guid> _kho;
    private readonly IAsyncQueryableExecuter _thucThi;

    public WarehouseDirectory(IRepository<Warehouse, Guid> kho, IAsyncQueryableExecuter thucThi)
    {
        _kho = kho;
        _thucThi = thucThi;
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, string>> LayTenKhoAsync(IReadOnlyCollection<Guid> idKho)
    {
        if (idKho.Count == 0)
        {
            return [];
        }

        var kho = await _thucThi.ToListAsync(
            (await _kho.GetQueryableAsync()).Where(x => idKho.Contains(x.Id)));

        return kho
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Code);
    }
}
