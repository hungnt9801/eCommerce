using eCommerce.Application.Contracts.Inventory;
using eCommerce.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
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

        var nhomDong = tatCaDong.GroupBy(x => x.StockDocumentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return ds
            .Where(x => type is null || x.Type == type)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenBy(x => x.Number, StringComparer.Ordinal)
            .Select(x =>
            {
                var dong = nhomDong.TryGetValue(x.Id, out var dsDong) ? dsDong : [];

                var (tienHang, vat) = TinhTong(dong);

                return ToDto(x, dong.Count, tienHang, vat);
            })
            .ToList();
    }

    public async Task<StockDocumentDto> GetAsync(Guid id)
    {
        var ct = await _chungTuRepository.GetAsync(id);
        var dong = await _dongRepository.GetListAsync(x => x.StockDocumentId == id);

        var (tienHang, vat) = TinhTong(dong);

        return ToDto(ct, dong.Count, tienHang, vat);
    }

    public async Task<List<StockDocumentLineDto>> GetLinesAsync(Guid id)
    {
        _ = await _chungTuRepository.GetAsync(id);

        var dong = await _dongRepository.GetListAsync(x => x.StockDocumentId == id);

        return dong.OrderBy(x => x.Sku, StringComparer.Ordinal)
            .Select(ToLineDto)
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

        if (input.ToWarehouseId is { } khoDichId)
        {
            var khoDich = await _khoRepository.FindAsync(khoDichId)
                ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                    .WithData("warehouseId", khoDichId)
                    .WithData("lyDo", "Kho đích KHÔNG tồn tại.");

            // 📌 Kho ĐÍCH cũng phải đang hoạt động: hàng chuyển vào kho đã đóng sẽ **kẹt vĩnh viễn** (không ai
            // bán được, báo cáo vẫn thấy tồn). Kiểm ở đây vì đây là **đường tạo** duy nhất.
            khoDich.YeuCauDangHoatDong();
        }

        var ct = new StockDocument(
            GuidGenerator.Create(), input.Type, input.Number, input.WarehouseId,
            input.OccurredAtUtc ?? Clock.Now, input.ToWarehouseId, input.Mode)
        {
            Note = input.Note,
            // 🆕 `WP-04` — khoá chống tạo trùng (DB có unique index; đây là dữ liệu để index so).
            IdempotencyKey = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey.Trim()
        };

        // 🆕 `WP-04` — các trường kiểu Nhanh đều đi qua **hàm kiểm của entity** (không set thuộc tính trần):
        // nhờ vậy luật nằm một chỗ và test được không cần DB.
        ct.DatDoiTac(input.CounterpartyType, input.CounterpartyId);
        ct.DatNguon(input.RefType, input.RefId);
        ct.DatTien(input.DiscountAmount, input.PaidAmount);

        await _chungTuRepository.InsertAsync(ct, autoSave: true);

        var dong = input.Lines.Select(x =>
        {
            var d = new StockDocumentLine(GuidGenerator.Create(), ct.Id, x.Sku, x.Quantity);

            d.DatChiTiet(x.UnitId, x.VatPercent, x.BatchId, x.ImeiSerials, x.UnitPrice);

            return d;
        }).ToList();

        await _dongRepository.InsertManyAsync(dong, autoSave: true);

        Logger.LogInformation("Đã tạo chứng từ kho {Number} ({Type}/{Mode}/{Direction}) ở trạng thái {Status} với {SoDong} dòng.",
            ct.Number, ct.Type, ct.Mode?.ToString() ?? "(khong)", ct.Direction?.ToString() ?? "(khong)", ct.Status, dong.Count);

        var (tienHangTao, vatTao) = TinhTong(dong);

        return ToDto(ct, dong.Count, tienHangTao, vatTao);
    }

    /// <summary>Huỷ phiếu còn `Draft` (phiếu đã ghi sổ **KHÔNG** huỷ được — sổ cái append-only).</summary>
    [Authorize(eCommerceConsts.Permissions.Inventory.Adjust)]
    public async Task<StockDocumentDto> CancelAsync(Guid id)
    {
        var ct = await _chungTuRepository.GetAsync(id);

        ct.Huy();

        await _chungTuRepository.UpdateAsync(ct, autoSave: true);

        Logger.LogInformation("Đã HUỶ chứng từ kho {Number} (trước đó {Status}).", ct.Number, ct.Status);

        var dongHuy = await _dongRepository.GetListAsync(x => x.StockDocumentId == id);

        var (tienHangHuy, vatHuy) = TinhTong(dongHuy);

        return ToDto(ct, dongHuy.Count, tienHangHuy, vatHuy);
    }

    /// <summary>
    /// 🆕 **`WP-04`** — DS phiếu **có phân trang + lọc** cho màn #7.
    /// 📌 Lọc ở **server**: bảng 20.000 phiếu không thể tải hết rồi lọc ở client (đúng lý do `04 §5` chọn DataTable
    /// server-side). 📌 Sắp xếp **tất định** (ngày giảm dần, số phiếu tăng dần) để phân trang **không lặp/mất dòng**.
    /// </summary>
    public async Task<PagedResultDto<StockDocumentDto>> GetPagedAsync(
        StockDocumentDirection? direction = null,
        StockDocumentStatus? status = null,
        StockDocumentMode? mode = null,
        Guid? warehouseId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skipCount = 0,
        int maxResultCount = 50)
    {
        var q = await _chungTuRepository.GetQueryableAsync();

        if (direction is { } huong)
        {
            q = q.Where(x => x.Direction == huong);
        }

        if (status is { } trangThai)
        {
            q = q.Where(x => x.Status == trangThai);
        }

        if (mode is { } kieu)
        {
            q = q.Where(x => x.Mode == kieu);
        }

        if (warehouseId is { } khoId)
        {
            q = q.Where(x => x.WarehouseId == khoId);
        }

        if (fromUtc is { } tu)
        {
            q = q.Where(x => x.OccurredAtUtc >= tu);
        }

        if (toUtc is { } den)
        {
            q = q.Where(x => x.OccurredAtUtc <= den);
        }

        var tong = await AsyncExecuter.CountAsync(q);

        var trang = await AsyncExecuter.ToListAsync(
            // ⚠️ **KHÔNG truyền `StringComparer.Ordinal` vào `ThenBy` trên `IQueryable`**: EF **không dịch được**
            // comparer ra SQL ⇒ `500` (*"could not be translated"*). Đã đo thật bằng probe HTTP (`WP-04`).
            q.OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.Number)
                .Skip(skipCount).Take(maxResultCount));

        // Đếm dòng + tính tiền của **đúng trang này** (một truy vấn) — không nạp cả bảng dòng như `GetListAsync` cũ.
        var ids = trang.Select(x => x.Id).ToList();
        var nhomDong = ids.Count == 0
            ? new Dictionary<Guid, List<StockDocumentLine>>()
            : (await _dongRepository.GetListAsync(x => ids.Contains(x.StockDocumentId)))
                .GroupBy(x => x.StockDocumentId)
                .ToDictionary(g => g.Key, g => g.ToList());

        return new PagedResultDto<StockDocumentDto>(
            tong,
            trang.Select(x =>
            {
                var dong = nhomDong.TryGetValue(x.Id, out var dsDong) ? dsDong : [];

                var (tienHang, vat) = TinhTong(dong);

                return ToDto(x, dong.Count, tienHang, vat);
            }).ToList());
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

    /// <summary>Map dòng + **tính tiền bằng hàm thuần** (một nguồn duy nhất — `TinhTienChungTu`).</summary>
    private static StockDocumentLineDto ToLineDto(StockDocumentLine x)
    {
        var thanhTien = TinhTienChungTu.ThanhTien(x.Quantity, x.UnitPrice);

        return new StockDocumentLineDto
        {
            Id = x.Id,
            Sku = x.Sku,
            Quantity = x.Quantity,
            UnitId = x.UnitId,
            VatPercent = x.VatPercent,
            BatchId = x.BatchId,
            ImeiSerials = x.ImeiSerials,
            UnitPrice = x.UnitPrice,
            Amount = thanhTien,
            VatAmount = TinhTienChungTu.TienVat(thanhTien, x.VatPercent)
        };
    }

    /// <summary>
    /// Tổng tiền của một phiếu từ **đúng các dòng của nó**.
    /// 📌 Dòng **thiếu đơn giá** ⇒ tổng là `null` (D-37), **không** cộng như `0`.
    /// </summary>
    private static (decimal? TienHang, decimal? Vat) TinhTong(IReadOnlyCollection<StockDocumentLine> dong)
        => (TinhTienChungTu.TongTienHang(dong.Select(x => (x.Quantity, x.UnitPrice))),
            TinhTienChungTu.TongVat(dong.Select(x => (x.Quantity, x.UnitPrice, x.VatPercent))));

    private static StockDocumentDto ToDto(StockDocument ct, int soDong, decimal? tienHang = null, decimal? vat = null) => new()
    {
        Id = ct.Id,
        Number = ct.Number,
        Type = ct.Type.ToString(),
        Status = ct.Status.ToString(),
        WarehouseId = ct.WarehouseId,
        ToWarehouseId = ct.ToWarehouseId,
        OccurredAtUtc = ct.OccurredAtUtc,
        Note = ct.Note,
        LineCount = soDong,

        // 🆕 `WP-04` — trả **chuỗi** cho enum để client không phải biết số (và để thêm giá trị enum không breaking).
        Direction = ct.Direction?.ToString(),
        Mode = ct.Mode?.ToString(),
        CounterpartyType = ct.CounterpartyType?.ToString(),
        CounterpartyId = ct.CounterpartyId,
        RefType = ct.RefType,
        RefId = ct.RefId,
        DiscountAmount = ct.DiscountAmount,
        PaidAmount = ct.PaidAmount,
        IdempotencyKey = ct.IdempotencyKey,

        // 🆕 `WP-04` (việc 8) — tiền của phiếu (suy ra từ dòng; `null` nếu có dòng chưa khai giá — D-37).
        TotalAmount = tienHang,
        TotalVat = vat,
        TotalPayment = TinhTienChungTu.TongThanhToan(tienHang, vat, ct.DiscountAmount)
    };
}
