using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`S14`** — hiện thực <see cref="ICustomerReturnGateway"/>: tạo chứng từ `CustomerReturn` **và ghi sổ ngay**
/// bằng chính `StockPostingService` (cùng đường với màn hình kho) ⇒ **không** có đường ghi thứ hai vào sổ cái.
/// </summary>
public class CustomerReturnGateway : ApplicationService, ICustomerReturnGateway
{
    private readonly IRepository<StockDocument, Guid> _chungTu;
    private readonly IRepository<StockDocumentLine, Guid> _dong;
    private readonly IRepository<Warehouse, Guid> _kho;
    private readonly StockPostingService _posting;

    public CustomerReturnGateway(
        IRepository<StockDocument, Guid> chungTu,
        IRepository<StockDocumentLine, Guid> dong,
        IRepository<Warehouse, Guid> kho,
        StockPostingService posting)
    {
        _chungTu = chungTu;
        _dong = dong;
        _kho = kho;
        _posting = posting;
    }

    public async Task<Guid> NhapHangKhachTraAsync(
        Guid warehouseId,
        string number,
        IReadOnlyCollection<(string Sku, int Quantity)> lines,
        DateTime atUtc,
        string? note = null)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong)
                .WithData("lyDo", "Không có dòng hàng hoàn nào để nhập kho.");
        }

        // ── IDEMPOTENT theo SỐ CHỨNG TỪ (chống nhập kho hai lần) ────────────────────────────────
        var daCo = await _chungTu.FirstOrDefaultAsync(x => x.Number == number);

        if (daCo is not null)
        {
            Logger.LogInformation(
                "HÀNG KHÁCH TRẢ: chứng từ {Number} ĐÃ tồn tại ⇒ không nhập kho lần hai (Idempotent). [S14-NHAP-KHO-CU]",
                number);

            return daCo.Id;
        }

        var kho = await _kho.FindAsync(warehouseId)
            ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                .WithData("warehouseId", warehouseId).WithData("lyDo", "Kho KHÔNG tồn tại.");

        kho.YeuCauDangHoatDong();

        var ct = new StockDocument(
            GuidGenerator.Create(), StockDocumentType.CustomerReturn, number, warehouseId, atUtc)
        {
            Note = note
        };

        await _chungTu.InsertAsync(ct, autoSave: true);

        await _dong.InsertManyAsync(
            lines.Select(x => new StockDocumentLine(GuidGenerator.Create(), ct.Id, x.Sku.Trim(), x.Quantity)).ToList(),
            autoSave: true);

        // 🚨 GHI SỔ NGAY — hàng đã nằm trong kho thật khi nhân viên đã đếm.
        var soButToan = await _posting.GhiSoAsync(ct.Id, atUtc);

        Logger.LogInformation(
            "HÀNG KHÁCH TRẢ: đã nhập kho {Number} ({SoDong} dòng · {SoButToan} bút toán) cho kho {Kho}. [S14-NHAP-KHO]",
            number, lines.Count, soButToan, warehouseId);

        return ct.Id;
    }
}
