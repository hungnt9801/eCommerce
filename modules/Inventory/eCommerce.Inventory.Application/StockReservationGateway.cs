using eCommerce.Application.Contracts.Inventory;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Inventory;

/// <summary>
/// Hiện thực `IStockReservationGateway` **trong module Inventory** (dùng repository + hai domain service).
/// 📌 Không `[Authorize]`: đây là hợp đồng nội bộ, không phải endpoint; người gọi (Sales) đã kiểm quyền của nó.
/// </summary>
public class StockReservationGateway : IStockReservationGateway, ITransientDependency
{
    private readonly IRepository<StockReservation, Guid> _giuCho;
    private readonly StockReservationService _nha;
    private readonly ChotGiuChoService _chot;

    public StockReservationGateway(
        IRepository<StockReservation, Guid> giuCho,
        StockReservationService nha,
        ChotGiuChoService chot)
    {
        _giuCho = giuCho;
        _nha = nha;
        _chot = chot;
    }

    /// <inheritdoc />
    public async Task<List<StockReservationDto>> LayTheoNguonAsync(string referenceId, bool chiDangGiu = true)
    {
        var ds = await _giuCho.GetListAsync(x => x.ReferenceId == referenceId);

        return ds
            .Where(x => !chiDangGiu || x.Status == ReservationStatus.Held)
            .OrderBy(x => x.Sku, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .Select(ToDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<StockReservationDto>> LayDangGiuAsync()
    {
        var ds = await _giuCho.GetListAsync(x => x.Status == ReservationStatus.Held);

        return ds
            .OrderBy(x => x.ReferenceId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(x => x.Sku, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .Select(ToDto)
            .ToList();
    }

    /// <inheritdoc />
    public Task<int> NhaTheoNguonAsync(string referenceId, DateTime at, ReservationStatus lyDo)
        => _nha.NhaTheoNguonAsync(referenceId, at, lyDo);

    /// <inheritdoc />
    public Task<int> ChotTheoNguonAsync(string referenceId, DateTime at)
        => _chot.ChotTheoNguonAsync(referenceId, at);

    private static StockReservationDto ToDto(StockReservation gc) => new()
    {
        Id = gc.Id,
        WarehouseId = gc.WarehouseId,
        Sku = gc.Sku,
        Quantity = gc.Quantity,
        Status = gc.Status.ToString(),
        ExpiresAtUtc = gc.ExpiresAtUtc,
        ReferenceType = gc.ReferenceType,
        ReferenceId = gc.ReferenceId,
        ReleasedAtUtc = gc.ReleasedAtUtc
    };
}
