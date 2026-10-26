using eCommerce.Inventory;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Reconciliation;

/// <summary>
/// **HIỆN THỰC `IStockBalanceReader` Ở HOST** (`P0 #1`, 26/10/2026) — host được phép thấy cả
/// `Inventory.Domain` lẫn hợp đồng của `Reconciliation`; module thì **không**.
///
/// 🔒 Chỉ **ĐỌC**: `GetListAsync()` trên hai repository, không `Update/Insert/Delete`, không gọi
/// `ApplyMovement` ⇒ không mở đường ghi tồn từ module đối soát.
/// 📌 Đăng ký tự động bằng `ITransientDependency` (assembly này nằm trong đồ thị module).
/// </summary>
public class StockBalanceReader : IStockBalanceReader, ITransientDependency
{
    private readonly IRepository<InventoryItem, Guid> _tonKho;
    private readonly IRepository<StockLedgerEntry, Guid> _soCai;

    public StockBalanceReader(
        IRepository<InventoryItem, Guid> tonKho,
        IRepository<StockLedgerEntry, Guid> soCai)
    {
        _tonKho = tonKho;
        _soCai = soCai;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TonKhoDoc>> LayTonKhoAsync()
    {
        var danhSach = await _tonKho.GetListAsync();

        return danhSach
            .Select(x => new TonKhoDoc(x.WarehouseId, x.Sku, x.OnHand))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ButToanDoc>> LayButToanAsync()
    {
        var danhSach = await _soCai.GetListAsync();

        return danhSach
            .Select(x => new ButToanDoc(x.WarehouseId, x.Sku, x.MovementType, x.QuantityDelta))
            .ToList();
    }
}
