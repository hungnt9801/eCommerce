using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`WP-06`** — hiện thực <see cref="IPosStockGateway"/> bằng **chính** `StockPostingService`
/// (cùng đường với màn hình kho và hàng khách trả) ⇒ **không** có đường ghi thứ hai vào sổ cái.
/// </summary>
public class PosStockGateway : ApplicationService, IPosStockGateway
{
    private readonly IRepository<StockDocument, Guid> _chungTu;
    private readonly IRepository<StockDocumentLine, Guid> _dong;
    private readonly IRepository<Warehouse, Guid> _kho;
    private readonly StockPostingService _posting;

    public PosStockGateway(
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

    /// <inheritdoc />
    public Task<Guid> XuatBanLeAsync(
        Guid warehouseId,
        string number,
        string retailBillNumber,
        IReadOnlyCollection<PosStockLine> lines,
        DateTime atUtc,
        string? note = null)
        => GhiChungTuAsync(
            StockDocumentType.GoodsIssue, StockDocumentMode.Retail, warehouseId, number, retailBillNumber, lines, atUtc,
            note ?? $"Xuất kho bán lẻ cho hoá đơn {retailBillNumber}.",
            "[WP06-XUAT-BAN-LE]");

    /// <inheritdoc />
    public Task<Guid> NhapHangTraAsync(
        Guid warehouseId,
        string number,
        string retailBillNumber,
        IReadOnlyCollection<PosStockLine> lines,
        DateTime atUtc,
        string? note = null)
        => GhiChungTuAsync(
            StockDocumentType.CustomerReturn, StockDocumentMode.Retail, warehouseId, number, retailBillNumber, lines, atUtc,
            note ?? $"Nhập lại kho hàng trả của hoá đơn {retailBillNumber}.",
            "[WP06-NHAP-HANG-TRA]");

    private async Task<Guid> GhiChungTuAsync(
        StockDocumentType type,
        StockDocumentMode mode,
        Guid warehouseId,
        string number,
        string retailBillNumber,
        IReadOnlyCollection<PosStockLine> lines,
        DateTime atUtc,
        string note,
        string nhan)
    {
        if (lines.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong)
                .WithData("lyDo", "Chứng từ kho do POS sinh KHÔNG có dòng nào.");
        }

        // ── IDEMPOTENT theo SỐ CHỨNG TỪ (chống đẩy tồn hai lần khi client retry) ─────────────────
        var daCo = await _chungTu.FirstOrDefaultAsync(x => x.Number == number);

        if (daCo is not null)
        {
            Logger.LogInformation(
                "POS: chứng từ {Number} ĐÃ tồn tại ({Status}) ⇒ không ghi sổ lần hai (Idempotent). {Nhan}",
                number, daCo.Status, nhan);

            return daCo.Id;
        }

        var kho = await _kho.FindAsync(warehouseId)
            ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                .WithData("warehouseId", warehouseId)
                .WithData("lyDo", "Kho KHÔNG tồn tại.");

        kho.YeuCauDangHoatDong();

        var ct = new StockDocument(GuidGenerator.Create(), type, number, warehouseId, atUtc, mode: mode);

        // 🔒 `RefType`/`RefId` trỏ về **hoá đơn bán lẻ** ⇒ tra *"vì sao tồn đổi"* ra đúng chứng từ gốc.
        ct.DatNguon("RetailBill", retailBillNumber);
        ct.Note = note;

        await _chungTu.InsertAsync(ct, autoSave: true);

        var dong = lines.Select(x =>
        {
            var d = new StockDocumentLine(GuidGenerator.Create(), ct.Id, x.Sku, x.Quantity);
            d.DatChiTiet(vatPercent: x.VatPercent, unitPrice: x.UnitPrice);
            return d;
        }).ToList();

        await _dong.InsertManyAsync(dong, autoSave: true);

        // 🚨 GHI SỔ NGAY — khách đã đi với hàng khi hoá đơn được lưu (không để `Draft`: tồn kho phải phản ánh
        // thực tế ngay; một bước "ghi sổ" nữa ở quầy là bước **không ai nhớ bấm**).
        var soButToan = await _posting.GhiSoAsync(ct.Id, atUtc);

        Logger.LogInformation(
            "POS: đã ghi sổ chứng từ {Number} ({Type}/{Mode}) cho {SoDong} dòng ⇒ {SoButToan} bút toán. {Nhan}",
            number, type, mode, dong.Count, soButToan, nhan);

        return ct.Id;
    }
}
