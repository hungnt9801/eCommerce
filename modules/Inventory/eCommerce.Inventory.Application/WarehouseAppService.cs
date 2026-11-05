using eCommerce.Application.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Inventory;

/// <summary>
/// **API KHO** (`INV-001` ADR-01) — tạo/liệt kê/ngừng dùng kho.
///
/// 📌 **`S17` (29/10/2026) — TẠI SAO LỚP NÀY NAY NẰM TRONG MODULE:** nó chỉ cần
/// `IRepository<Warehouse>` (Domain của chính module), DTO ở `Inventory.Application.Contracts` và
/// `eCommerceConsts` (Domain.Shared) ⇒ **không** có lý do gì để ở host. Trước `S17` nó nằm trong
/// `src/eCommerce.Application/Inventory/` **cùng tệp** với `InventoryReconciliationAppService` (lớp đó
/// đọc `ReconRun`/`InventoryReconciliationService` nên **cố ý** còn ở host).
/// 🔒 **Không đổi hành vi:** cùng tên lớp ⇒ cùng route `/api/app/warehouse`; cùng `[Authorize]` cấp class
/// + cấp method; cùng DTO; cùng mã lỗi. Hợp đồng `IWarehouseAppService` ở `Application.Contracts` của module
/// (đã có từ đợt tách module 24/10/2026) nay do **chính module** hiện thực.
/// </summary>
// 🚨 **`WP-04` — SỬA LỖI 500 ĐÃ ĐO ĐƯỢC BẰNG PROBE HTTP:** trước đó thuộc tính này chỉ khai **interface**
// ⇒ ABP chỉ đăng ký interface, còn controller quy ước lại resolve **lớp cụ thể** ⇒
// `The requested service 'eCommerce.Inventory.WarehouseAppService' has not been registered` ⇒ **POST
// `/api/app/warehouse` trả 500** (không tạo được kho ⇒ phiếu kho/POS bị chặn ngay từ bước đầu).
// ✅ Khai **CẢ HAI** (`IWarehouseAppService` + chính lớp) như `TonKhoAppService`/`StockDocumentAppService`:
// giữ đăng ký tường minh theo hợp đồng (test `HopDongHostTests` khoá điều này) **và** cho controller resolve được.
[ExposeServices(typeof(IWarehouseAppService), typeof(WarehouseAppService))]
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
