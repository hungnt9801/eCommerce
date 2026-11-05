using eCommerce.Application.Contracts.Inventory;
using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Inventory;

/// <summary>
/// **API ĐỐI SOÁT KHO** (`INV-001` ADR-10) — chỉ đọc + phát metric.
///
/// 📌 **VÌ SAO LỚP NÀY VẪN Ở HOST (`S17`, 29/10/2026):** nó resolve `InventoryReconciliationService`
/// (Domain của module `Reconciliation` — hiện đang giữ namespace `eCommerce.Inventory`) và đọc
/// `IRepository<InventoryItem>`; đây là **đường nối giữa hai module** (Inventory + Reconciliation) nên
/// host là nơi duy nhất được phép biết cả hai. Hợp đồng `IInventoryReconciliationAppService` đã nằm ở
/// `Inventory.Application.Contracts` từ đợt tách module (24/10/2026); `WarehouseAppService` (cùng tệp cũ)
/// đã chuyển vào `modules/Inventory/eCommerce.Inventory.Application` ở `S17`.
/// 🔒 Không đổi hành vi: cùng tên lớp, cùng route, cùng `[Authorize]`, cùng DTO.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class InventoryReconciliationAppService : ApplicationService, IInventoryReconciliationAppService
{
    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly InventoryReconciliationService _doiSoat;

    public InventoryReconciliationAppService(
        IRepository<InventoryItem, Guid> tonKhoRepository,
        InventoryReconciliationService doiSoat)
    {
        _tonKhoRepository = tonKhoRepository;
        _doiSoat = doiSoat;
    }

    public async Task<KetQuaDoiSoatDto> GetDoiSoatAsync()
    {
        var lech = await _doiSoat.DoiSoatTonKhoAsync();

        return new KetQuaDoiSoatDto
        {
            SoCapDaKiem = (int)await _tonKhoRepository.GetCountAsync(),
            SoCapLech = lech.Count,
            CacLech = lech.Select(x => new LechTonKhoDto
            {
                WarehouseId = x.WarehouseId,
                Sku = x.Sku,
                OnHand = x.OnHand,
                TongSoCai = x.TongSoCai,
                ChenhLech = x.ChenhLech
            }).ToList()
        };
    }
}
