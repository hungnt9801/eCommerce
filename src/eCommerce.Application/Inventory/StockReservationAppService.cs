using eCommerce.Application.Contracts.Inventory;
using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Inventory;

/// <summary>**API GIỮ CHỖ** (`INV-002`). 🔐 Đọc = `Inventory.Default`; ghi = `Inventory.Adjust`.</summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class StockReservationAppService : ApplicationService, IStockReservationAppService
{
    private readonly IRepository<StockReservation, Guid> _giuChoRepository;
    private readonly StockReservationService _giuChoService;
    private readonly ChotGiuChoService _chotService;

    public StockReservationAppService(
        IRepository<StockReservation, Guid> giuChoRepository,
        StockReservationService giuChoService,
        ChotGiuChoService chotService)
    {
        _giuChoRepository = giuChoRepository;
        _giuChoService = giuChoService;
        _chotService = chotService;
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<StockReservationDto> GiuChoAsync(GiuChoInput input)
    {
        var at = input.At ?? Clock.Now;

        var ttl = input.TtlPhut is { } phut ? TimeSpan.FromMinutes(phut) : (TimeSpan?)null;

        var gc = await _giuChoService.GiuChoAsync(
            input.WarehouseId, input.Sku, input.Quantity, at, ttl, input.ReferenceType, input.ReferenceId);

        return ToDto(gc);
    }

    public async Task<StockReservationDto> GetAsync(Guid id) => ToDto(await _giuChoRepository.GetAsync(id));

    public async Task<List<StockReservationDto>> GetDanhSachAsync(ReservationStatus? status = null)
    {
        var ds = await _giuChoRepository.GetListAsync();

        return ds
            .Where(x => status is null || x.Status == status.Value)
            .OrderByDescending(x => x.ExpiresAtUtc)
            .Select(ToDto)
            .ToList();
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<StockReservationDto> NhaChoAsync(Guid id, DateTime? at = null)
        => ToDto(await _giuChoService.NhaChoAsync(id, at ?? Clock.Now, ReservationStatus.Released));

    /// <summary>
    /// 📌 Đây là **thao tác của JOB**, không phải của người dùng. Ở giai đoạn này chưa có background job worker
    /// (bài học `CHN-002`: job phải có `Timer.RunOnStart` để chạy ngay sau restart) ⇒ để **gọi được** và
    /// **kiểm chứng được** thì mở thành endpoint; khi có worker thì chuyển sang job và endpoint chỉ giữ cho vận hành.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<KetQuaNhaChoHetHanDto> NhaChoHetHanAsync(DateTime? at = null)
    {
        var so = await _giuChoService.NhaChoHetHanAsync(at ?? Clock.Now);

        return new KetQuaNhaChoHetHanDto { SoLuotDaNha = so };
    }

    /// <summary>
    /// 🚨 **CHỐT GIỮ CHỖ → XUẤT KHO** (`SAL-003`). 📌 Đây là thao tác **của luồng xác nhận đơn** (`SAL-001`);
    /// mở thành endpoint để **kiểm chứng được** cho tới khi có đường đơn hàng đầy đủ.
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<KetQuaChotDto> ChotTheoNguonAsync(string referenceId, DateTime? at = null)
    {
        var so = await _chotService.ChotTheoNguonAsync(referenceId, at ?? Clock.Now);

        return new KetQuaChotDto { ReferenceId = referenceId, SoLuotDaChot = so };
    }

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
