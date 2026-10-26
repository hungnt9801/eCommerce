using eCommerce.Application.Contracts.Inventory;
using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Inventory;

/// <summary>
/// **API KHO** (`INV-001`) — tạo chứng từ + **ghi sổ**.
///
/// 🔐 **QUYỀN:** đọc = `eCommerce.Inventory`; ghi = `eCommerce.Inventory.Adjust` (**đã có sẵn**).
/// 📌 **VÌ SAO CHƯA tách quyền chi tiết** (`Receipt.Create`/`Transfer.Create`… theo tài liệu §1): thêm permission
/// mới **phải khai ở CẢ HAI NƠI** (hằng số + provider) **và chạy lại `DbMigrator`**, nếu không ⇒ **`403` im lặng
/// cho MỌI người kể cả admin** (bài học lượt 5). Lượt này ưu tiên **đường chạy được + kiểm chứng được**; việc tách
/// quyền chi tiết là **lát cắt riêng có kiểm chứng riêng** — ghi ở `inv-001` §19.4.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Inventory.Default)]
public class StockDocumentAppService : ApplicationService, IStockDocumentAppService
{
    private readonly IRepository<StockDocument, Guid> _chungTuRepository;
    private readonly IRepository<StockDocumentLine, Guid> _dongRepository;
    private readonly IRepository<Warehouse, Guid> _khoRepository;
    private readonly StockPostingService _postingService;

    public StockDocumentAppService(
        IRepository<StockDocument, Guid> chungTuRepository,
        IRepository<StockDocumentLine, Guid> dongRepository,
        IRepository<Warehouse, Guid> khoRepository,
        StockPostingService postingService)
    {
        _chungTuRepository = chungTuRepository;
        _dongRepository = dongRepository;
        _khoRepository = khoRepository;
        _postingService = postingService;
    }

    public async Task<List<StockDocumentDto>> GetListAsync(StockDocumentType? type = null)
    {
        var ds = await _chungTuRepository.GetListAsync();
        var tatCaDong = await _dongRepository.GetListAsync();

        var dem = tatCaDong.GroupBy(x => x.StockDocumentId).ToDictionary(g => g.Key, g => g.Count());

        return ds
            .Where(x => type is null || x.Type == type)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenBy(x => x.Number, StringComparer.Ordinal)
            .Select(x => ToDto(x, dem.TryGetValue(x.Id, out var n) ? n : 0))
            .ToList();
    }

    public async Task<StockDocumentDto> GetAsync(Guid id)
    {
        var ct = await _chungTuRepository.GetAsync(id);

        return ToDto(ct, (int)await _dongRepository.CountAsync(x => x.StockDocumentId == id));
    }

    public async Task<List<StockDocumentLineDto>> GetLinesAsync(Guid id)
    {
        _ = await _chungTuRepository.GetAsync(id);

        var dong = await _dongRepository.GetListAsync(x => x.StockDocumentId == id);

        return dong.OrderBy(x => x.Sku, StringComparer.Ordinal)
            .Select(x => new StockDocumentLineDto { Id = x.Id, Sku = x.Sku, Quantity = x.Quantity })
            .ToList();
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<StockDocumentDto> CreateAsync(CreateStockDocumentDto input)
    {
        var kho = await _khoRepository.FindAsync(input.WarehouseId)
            ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                .WithData("warehouseId", input.WarehouseId)
                .WithData("lyDo", "Kho KHÔNG tồn tại.");

        // 🚨 Kho ngừng dùng ⇒ **CẤM** tạo chứng từ (nhập hàng vào kho đã đóng ⇒ **tồn kho "ảo"** mà báo cáo vẫn tin).
        kho.YeuCauDangHoatDong();

        var ct = new StockDocument(
            GuidGenerator.Create(), input.Type, input.Number, input.WarehouseId,
            input.OccurredAtUtc ?? Clock.Now, input.ToWarehouseId)
        {
            Note = input.Note
        };

        await _chungTuRepository.InsertAsync(ct, autoSave: true);

        var dong = input.Lines
            .Select(x => new StockDocumentLine(GuidGenerator.Create(), ct.Id, x.Sku, x.Quantity))
            .ToList();

        await _dongRepository.InsertManyAsync(dong, autoSave: true);

        Logger.LogInformation("Đã tạo chứng từ kho {Number} ({Type}) ở trạng thái {Status} với {SoDong} dòng.",
            ct.Number, ct.Type, ct.Status, dong.Count);

        return ToDto(ct, dong.Count);
    }

    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<PostStockDocumentResultDto> GhiSoAsync(Guid id, DateTime? at = null)
    {
        var soButToan = await _postingService.GhiSoAsync(id, at ?? Clock.Now);

        var ct = await _chungTuRepository.GetAsync(id);

        return new PostStockDocumentResultDto
        {
            Id = ct.Id,
            Number = ct.Number,
            Status = ct.Status.ToString(),
            PostedEntries = soButToan
        };
    }

    private static StockDocumentDto ToDto(StockDocument ct, int soDong) => new()
    {
        Id = ct.Id,
        Number = ct.Number,
        Type = ct.Type.ToString(),
        Status = ct.Status.ToString(),
        WarehouseId = ct.WarehouseId,
        ToWarehouseId = ct.ToWarehouseId,
        OccurredAtUtc = ct.OccurredAtUtc,
        Note = ct.Note,
        LineCount = soDong
    };
}
