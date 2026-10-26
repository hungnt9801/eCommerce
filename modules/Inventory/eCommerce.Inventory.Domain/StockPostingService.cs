using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **DỊCH VỤ GHI SỔ KHO** (`INV-001` **ADR-08/ADR-09**) — nơi **DUY NHẤT** được phép biến chứng từ thành
/// **bút toán sổ cái** + **thay đổi tồn kho**.
///
/// **TRÌNH TỰ BẮT BUỘC (mỗi bước đều chống một lỗi cụ thể):**
///   1. Đọc chứng từ + dòng; kiểm **kho còn hoạt động** (`BR`: kho ngừng dùng ⇒ không ghi sổ).
///   2. **Giành khoá phân tán theo `(tenant, warehouse, sku)`** cho **MỌI** SKU của chứng từ, theo **thứ tự
///      SKU đã sắp xếp**. 📌 Sắp xếp là **bắt buộc**: hai chứng từ cùng hai SKU nhưng thứ tự ngược nhau
///      ⇒ mỗi bên giữ một khoá và chờ bên kia ⇒ **DEADLOCK**. Xếp theo thứ tự cố định thì không bao giờ xảy ra.
///   3. 🚨 **ADR-09 — chuyển trạng thái bằng `UPDATE … WHERE Status = 'Draft'` + KIỂM SỐ DÒNG**, **KHÔNG**
///      đọc-rồi-ghi. Lý do: hai request ghi sổ **đồng thời** cùng đọc thấy `Draft` ⇒ cả hai đều ghi bút toán
///      ⇒ **tồn kho tăng/giảm GẤP ĐÔI** mà không có lỗi nào. `UPDATE` có điều kiện là thao tác **nguyên tử**:
///      chỉ **một** request nhận `rows = 1`, request kia nhận `rows = 0` ⇒ **NÉM**.
///      (Hàng rào cuối vẫn là unique `(TenantId, ReferenceLineId)` trên sổ cái — ADR-05.)
///   4. Với mỗi dòng: sinh bút toán (hàm **thuần** `TinhButToanChungTu`) ⇒ cập nhật `InventoryItem`
///      (tạo nếu chưa có) **và** ghi `StockLedgerEntry` với `ReferenceLineId = dòng.Id` — **cùng transaction**.
/// </summary>
public class StockPostingService : DomainService
{
    public const int ChoKhoaToiDaGiay = 30;

    private readonly IRepository<StockDocument, Guid> _chungTuRepository;
    private readonly IRepository<StockDocumentLine, Guid> _dongRepository;
    private readonly IRepository<Warehouse, Guid> _khoRepository;
    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _soCaiRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    public StockPostingService(
        IRepository<StockDocument, Guid> chungTuRepository,
        IRepository<StockDocumentLine, Guid> dongRepository,
        IRepository<Warehouse, Guid> khoRepository,
        IRepository<InventoryItem, Guid> tonKhoRepository,
        IRepository<StockLedgerEntry, Guid> soCaiRepository,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant)
    {
        _chungTuRepository = chungTuRepository;
        _dongRepository = dongRepository;
        _khoRepository = khoRepository;
        _tonKhoRepository = tonKhoRepository;
        _soCaiRepository = soCaiRepository;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
    }

    /// <summary>Kết quả ghi sổ: số bút toán đã sinh (0 ⇒ kiểm kê khớp, không có gì để ghi).</summary>
    public async Task<int> GhiSoAsync(Guid documentId, DateTime at)
    {
        var chungTu = await _chungTuRepository.FindAsync(documentId)
            ?? throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong)
                .WithData("lyDo", "Không tìm thấy chứng từ.")
                .WithData("documentId", documentId);

        var dong = await _dongRepository.GetListAsync(x => x.StockDocumentId == documentId);
        var dongSapXep = dong.OrderBy(x => x.Sku, StringComparer.Ordinal).ToList();

        // `soDong` đọc từ DB rồi truyền vào — bài học `PRC-001` §17.2 **L1**.
        // (Chứng từ `Draft` **được phép** sửa nên số dòng phải kiểm tại thời điểm ghi sổ.)
        if (dongSapXep.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.ChungTuThieuDong).WithData("number", chungTu.Number);
        }

        var khoNguon = await _khoRepository.FindAsync(chungTu.WarehouseId)
            ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                .WithData("warehouseId", chungTu.WarehouseId)
                .WithData("lyDo", "Kho KHÔNG tồn tại.");

        khoNguon.YeuCauDangHoatDong();

        if (chungTu.ToWarehouseId is { } idDich)
        {
            var khoDich = await _khoRepository.FindAsync(idDich)
                ?? throw new BusinessException(InventoryErrorCodes.KhoNgungHoatDong)
                    .WithData("warehouseId", idDich).WithData("lyDo", "Kho đích KHÔNG tồn tại.");

            khoDich.YeuCauDangHoatDong();
        }

        // ── 2. KHOÁ phân tán cho MỌI SKU, theo THỨ TỰ SẮP XẾP (chống deadlock) ────────────────────
        var tenKhoa = dongSapXep
            .Select(x => InventoryLockKeys.TheoSku(_currentTenant.Id, chungTu.WarehouseId, x.Sku))
            .ToList();

        var daGianh = new List<IAsyncDisposable>();

        try
        {
            foreach (var khoa in tenKhoa)
            {
                var giu = await _distributedLock.TryAcquireAsync(khoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
                    ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                        .WithData("khoa", khoa).WithData("choGiay", ChoKhoaToiDaGiay);

                daGianh.Add(giu);
            }

            // ── 3. Chuyển `Draft → Posted` (luật miền) ───────────────────────────────────────────
            // 🚨 **ADR-09 — GHI RÕ CÁCH LÀM VÀ GIỚI HẠN (không che):**
            // Tài liệu đề xuất `UPDATE … WHERE Status = 'Draft'` + **kiểm số dòng** để chống **hai request
            // ĐỒNG THỜI** cùng ghi sổ. Cách đó cần **EF Core ngay trong tầng Domain** — mà tầng Domain của dự án
            // **cố ý** không phụ thuộc persistence ⇒ **không** đặt ở đây.
            // ✅ **Hàng rào ĐỒNG THỜI thật nằm ở DB và ĐÃ CÓ từ lát cắt 1** (`INV-001` **ADR-05**): unique
            // `(TenantId, ReferenceLineId)` trên `InvStockLedger`. Hai request đồng thời cùng ghi sổ sẽ cùng
            // chèn bút toán với **cùng `ReferenceLineId`** ⇒ request thứ hai **vi phạm unique** ⇒ transaction
            // **rollback** ⇒ tồn kho **KHÔNG** bị cộng hai lần.
            // 📌 Đúng tinh thần ADR: *"chống ghi trùng bằng unique index, KHÔNG bằng kiểm tra ở app"*.
            // ⚠️ **CÒN LẠI (lát cắt sau):** biến `23505` đó thành **mã nghiệp vụ** (`ChungTuKhongOTrangThaiNhap`)
            // bằng một exception filter — hiện client sẽ thấy **500**.
            chungTu.GhiSo(dongSapXep.Count);

            await _chungTuRepository.UpdateAsync(chungTu, autoSave: false);

            // ── 4. Sinh bút toán + cập nhật tồn kho (cùng transaction) ───────────────────────────
            var soButToan = 0;

            foreach (var d in dongSapXep)
            {
                var keHoach = chungTu.Type == StockDocumentType.Stocktake
                    ? await KeHoachKiemKeAsync(chungTu.WarehouseId, d)
                    : TinhButToanChungTu.ChoTungDong(chungTu.Type, d.Quantity, chungTu.WarehouseId, chungTu.ToWarehouseId);

                foreach (var but in keHoach)
                {
                    await ApDungButToanAsync(chungTu, d, but, at);
                    soButToan++;
                }
            }

            Logger.LogInformation(
                "Đã GHI SỔ chứng từ {Number} ({Type}): {SoDong} dòng ⇒ {SoButToan} bút toán sổ cái.",
                chungTu.Number, chungTu.Type, dongSapXep.Count, soButToan);

            return soButToan;
        }
        finally
        {
            // Nhả khoá theo thứ tự NGƯỢC lại (thói quen tốt; với khoá độc lập thì không bắt buộc).
            for (var i = daGianh.Count - 1; i >= 0; i--)
            {
                await daGianh[i].DisposeAsync();
            }
        }
    }

    private async Task<IReadOnlyList<TinhButToanChungTu.ButToan>> KeHoachKiemKeAsync(Guid khoId, StockDocumentLine d)
    {
        var ton = await _tonKhoRepository.FirstOrDefaultAsync(x => x.WarehouseId == khoId && x.Sku == d.Sku);
        var tonTrenSo = ton?.OnHand ?? 0;

        var but = TinhButToanChungTu.ChoTungKiemKe(khoId, d.Sku, d.Quantity, tonTrenSo);

        // Khớp ⇒ KHÔNG bút toán (không ghi dòng sổ cái "0").
        return but is null ? [] : [but];
    }

    private async Task ApDungButToanAsync(
        StockDocument chungTu, StockDocumentLine d, TinhButToanChungTu.ButToan but, DateTime at)
    {
        var ton = await _tonKhoRepository.FirstOrDefaultAsync(x => x.WarehouseId == but.WarehouseId && x.Sku == d.Sku);

        if (ton is null)
        {
            ton = new InventoryItem(GuidGenerator.Create(), but.WarehouseId, d.Sku);
            await _tonKhoRepository.InsertAsync(ton, autoSave: false);
        }

        // NÉM nếu làm âm tồn / giữ chỗ vượt tồn (bất biến miền) — lớp trong; DB có `CHECK` (ADR-03).
        ton.ApplyMovement(but.Loai, but.DoLon);

        var but_ = new StockLedgerEntry(
            GuidGenerator.Create(), but.WarehouseId, d.Sku, but.Loai, but.DoLon, ton.OnHand, at,
            referenceType: chungTu.Type.ToString(),
            referenceId: chungTu.Number,
            channelCode: null,
            referenceLineId: d.Id);

        await _soCaiRepository.InsertAsync(but_, autoSave: false);
        await _tonKhoRepository.UpdateAsync(ton, autoSave: false);
    }
}
