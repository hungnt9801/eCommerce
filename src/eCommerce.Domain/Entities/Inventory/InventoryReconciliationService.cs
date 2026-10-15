using eCommerce.Kernel.Observability;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **ĐỐI SOÁT TỒN KHO** (`INV-001` **ADR-10**) — so **ảnh chụp** `InventoryItem.OnHand` với **nguồn sự thật**
/// `SUM(StockLedgerEntry)` và **phát metric** khi lệch.
///
/// 📌 **VÌ SAO PHẢI CHẠY ĐỊNH KỲ (không phải "chạy khi cần"):** lệch tồn kho là loại lỗi **âm thầm** —
/// báo cáo vẫn ra số, chỉ là số **sai**; phát hiện muộn thì đã bán sai/hết hàng mà không ai biết. Đối soát
/// định kỳ biến *"phát hiện bởi khách hàng/kế toán"* thành *"phát hiện bởi hệ thống"*.
///
/// ⚠️ **ĐỌC HẾT SỔ CÁI LÀ CHẤP NHẬN ĐƯỢC Ở GIAI ĐOẠN NÀY, KHÔNG Ở QUY MÔ LỚN:**
/// hiện nạp toàn bộ `InvStockLedger` để gom theo `(kho, SKU)`. Với sổ cái lớn (hàng triệu dòng) phải chuyển sang
/// **truy vấn gom ở DB** (`GROUP BY` + `SUM`) hoặc đối soát theo **cửa sổ thời gian** + số dư đầu kỳ.
/// 📌 Ghi rõ ở `inv-001` §20.4 — **không** để người sau tưởng cách này dùng được mãi.
/// </summary>
public class InventoryReconciliationService : DomainService
{
    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _soCaiRepository;
    private readonly eCommerceMetrics _metrics;

    public InventoryReconciliationService(
        IRepository<InventoryItem, Guid> tonKhoRepository,
        IRepository<StockLedgerEntry, Guid> soCaiRepository,
        eCommerceMetrics metrics)
    {
        _tonKhoRepository = tonKhoRepository;
        _soCaiRepository = soCaiRepository;
        _metrics = metrics;
    }

    /// <summary>
    /// Chạy đối soát. Trả về **danh sách cặp `(kho, SKU)` bị lệch** (rỗng ⇒ khớp hoàn toàn).
    /// Luôn **ghi metric** `inventory_recon_mismatch_skus` — kể cả khi bằng `0` (số 0 là thông tin: *"đã kiểm và khớp"*).
    /// </summary>
    public async Task<IReadOnlyList<DoiSoatTonKho.Lech>> DoiSoatTonKhoAsync()
    {
        var cacTon = await _tonKhoRepository.GetListAsync();
        var soCai = await _soCaiRepository.GetListAsync();

        // Chỉ lấy bút toán **ảnh hưởng `OnHand`** — `Reservation`/`ReservationRelease` đổi `Reserved`
        // (nếu cộng cả chúng vào thì **báo lệch GIẢ** ngay khi có đơn giữ chỗ ⇒ đội vận hành mất tin vào cảnh báo).
        var theoKhoSku = soCai
            .Where(x => DoiSoatTonKho.AnhHuongOnHand(x.MovementType))
            .GroupBy(x => (x.WarehouseId, x.Sku))
            .ToDictionary(
                g => g.Key,
                g => DoiSoatTonKho.Tong(g.Select(x => (x.MovementType, x.QuantityDelta))));

        var lech = new List<DoiSoatTonKho.Lech>();

        // Mọi bản ghi tồn kho phải khớp tổng sổ cái (kể cả tổng = 0 khi CHƯA có bút toán nào).
        foreach (var ton in cacTon)
        {
            var tongSoCai = theoKhoSku.TryGetValue((ton.WarehouseId, ton.Sku), out var t) ? t : 0;

            if (ton.OnHand != tongSoCai)
            {
                lech.Add(new DoiSoatTonKho.Lech(ton.WarehouseId, ton.Sku, ton.OnHand, tongSoCai));
            }
        }

        // Chiều ngược lại: có bút toán cho `(kho, SKU)` mà **KHÔNG** có bản ghi tồn kho ⇒ tồn kho **MẤT**
        // (dòng `InventoryItem` bị xoá tay?) ⇒ cũng là lệch, và là loại lệch **nguy hiểm hơn** (hệ thống sẽ
        // tạo lại bản ghi từ 0 ⇒ **mất toàn bộ số dư**).
        var tapTon = cacTon.Select(x => (x.WarehouseId, x.Sku)).ToHashSet();

        foreach (var kv in theoKhoSku.Where(kv => !tapTon.Contains(kv.Key)))
        {
            lech.Add(new DoiSoatTonKho.Lech(kv.Key.WarehouseId, kv.Key.Sku, OnHand: 0, TongSoCai: kv.Value));
        }

        _metrics.SetInventoryReconMismatchSkus(lech.Count);

        if (lech.Count > 0)
        {
            // `Warning` (không phải `Error`): lệch tồn **cần người xem**, nhưng KHÔNG làm chết request nào
            // ⇒ để mức `Error` sẽ làm nhiễu tiêu chí "không có dòng `fail:`" của quy trình kiểm chứng.
            Logger.LogWarning(
                "ĐỐI SOÁT KHO: {SoLech} cặp (kho, SKU) LỆCH giữa ảnh chụp và sổ cái. Chi tiết: {ChiTiet}",
                lech.Count,
                string.Join("; ", lech.Take(20).Select(x =>
                    $"{x.Sku}@{x.WarehouseId}: OnHand={x.OnHand} vs sổ cái={x.TongSoCai} (lệch {x.ChenhLech})")));
        }
        else
        {
            Logger.LogInformation("ĐỐI SOÁT KHO: KHỚP — {SoTon} cặp (kho, SKU), không có lệch.", cacTon.Count);
        }

        return lech;
    }
}
