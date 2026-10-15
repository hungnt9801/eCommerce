using eCommerce.Application.Contracts.Inventory;
using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Inventory;

/// <summary>**API KHO** (`INV-001` ADR-01).</summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class WarehouseAppService : ApplicationService, IWarehouseAppService
{
    private readonly IRepository<Warehouse, Guid> _khoRepository;

    public WarehouseAppService(IRepository<Warehouse, Guid> khoRepository)
    {
        _khoRepository = khoRepository;
    }

    public async Task<List<WarehouseDto>> GetListAsync(bool? chiHoatDong = null)
    {
        var ds = await _khoRepository.GetListAsync();

        return ds
            .Where(x => chiHoatDong is null || x.IsActive == chiHoatDong.Value)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<WarehouseDto> CreateAsync(CreateWarehouseDto input)
    {
        var ma = input.Code.Trim();

        // 🚨 Kiểm TRƯỚC để lỗi ra dưới **mã nghiệp vụ** thay vì `23505` khó hiểu. Hàng rào cuối là unique index
        // `(TenantId, Code)` trên DB (đã sửa tay `NULLS NOT DISTINCT` — nếu thiếu thì hai kho CÙNG MÃ lọt qua
        // ⇒ chứng từ trỏ `WarehouseId` không biết kho nào ⇒ tồn kho tách hai nơi mà báo cáo cộng gộp).
        var daCo = await _khoRepository.FirstOrDefaultAsync(x => x.Code == ma);

        if (daCo is not null)
        {
            throw new BusinessException(InventoryErrorCodes.MaKhoDaTonTai)
                .WithData("code", ma)
                .WithData("warehouseId", daCo.Id);
        }

        var kho = new Warehouse(GuidGenerator.Create(), ma, input.Name, input.Address);

        await _khoRepository.InsertAsync(kho, autoSave: true);

        Logger.LogInformation("Đã tạo kho {Code} ({Name}).", kho.Code, kho.Name);

        return ToDto(kho);
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<WarehouseDto> NgungSuDungAsync(Guid id)
    {
        var kho = await _khoRepository.GetAsync(id);

        kho.NgungSuDung();

        await _khoRepository.UpdateAsync(kho, autoSave: true);

        // ⚠️ Ghi rõ hệ quả để vận hành không bất ngờ: kho ngừng dùng ⇒ **CẤM** tạo/ghi sổ chứng từ mới,
        // nhưng **chứng từ cũ + sổ cái vẫn nguyên** (append-only) ⇒ lịch sử vẫn tra được.
        Logger.LogWarning("Kho {Code} đã NGỪNG dùng — mọi chứng từ MỚI trên kho này sẽ bị từ chối.", kho.Code);

        return ToDto(kho);
    }

    private static WarehouseDto ToDto(Warehouse kho) => new()
    {
        Id = kho.Id,
        Code = kho.Code,
        Name = kho.Name,
        Address = kho.Address,
        IsActive = kho.IsActive
    };
}

/// <summary>**API ĐỐI SOÁT KHO** (`INV-001` ADR-10) — chỉ đọc + phát metric.</summary>
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
