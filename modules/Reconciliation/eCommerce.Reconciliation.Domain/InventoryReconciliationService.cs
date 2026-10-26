using eCommerce.Kernel.Observability;
using eCommerce.Settings;
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
/// 🚨 **`REC-001` (v2) — LƯU LỊCH SỬ LƯỢT QUÉT (`ReconRuns` + `ReconRunMismatches`) VÀ ÁP NGƯỠNG THẬT:**
/// trước v2, kết quả quét **chỉ** đi vào log + metric ⇒ `0` **không phân biệt** được *"đã đo và khớp"* với
/// *"chưa ai đo"*, và **không** trả lời được *"lệch này mới có hay đã lâu"*. Nay **mọi** lượt quét được ghi
/// (kể cả lượt **không** lệch), kèm **ảnh chụp chính sách** (đơn vị/giá trị ngưỡng/hành động) và
/// **phân loại theo ngưỡng** (<see cref="NguongLech"/> — hàm thuần).
///
/// ⚠️ **ĐỌC HẾT SỔ CÁI LÀ CHẤP NHẬN ĐƯỢC Ở GIAI ĐOẠN NÀY, KHÔNG Ở QUY MÔ LỚN:**
/// hiện nạp toàn bộ `InvStockLedger` để gom theo `(kho, SKU)`. Với sổ cái lớn (hàng triệu dòng) phải chuyển sang
/// **truy vấn gom ở DB** (`GROUP BY` + `SUM`) hoặc đối soát theo **cửa sổ thời gian** + số dư đầu kỳ.
/// 📌 Ghi rõ ở `inv-001` §20.4 — **không** để người sau tưởng cách này dùng được mãi.
/// </summary>
public class InventoryReconciliationService : DomainService
{
    /// <summary>
    /// 🚨 **TRẦN SỐ DÒNG LỆCH LƯU MỖI LƯỢT** (10 000).
    /// 📌 **VÌ SAO CÓ TRẦN:** bảng lịch sử **không** được phép phình theo **một** sự cố diện rộng
    /// (một lần sửa SQL tay sai có thể làm **mọi** cặp `(kho, SKU)` lệch). Không có trần thì lượt quét đó
    /// ghi hàng trăm nghìn dòng, làm chậm chính job đang cần chạy để **phát hiện** sự cố.
    /// 🔒 Vượt trần ⇒ **KHÔNG im lặng**: cờ `BiCatBot` + log `Warning` (số lệch **thật** vẫn được lưu ở `ReconRuns.SoLech`).
    /// </summary>
    public const int SoDongLechToiDaMoiLuot = 10_000;

    private readonly IRepository<InventoryItem, Guid> _tonKhoRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _soCaiRepository;
    private readonly IRepository<ReconRun, Guid> _luotDoiSoatRepository;
    private readonly IRepository<ReconRunMismatch, Guid> _dongLechRepository;
    private readonly eCommerceMetrics _metrics;

    public InventoryReconciliationService(
        IRepository<InventoryItem, Guid> tonKhoRepository,
        IRepository<StockLedgerEntry, Guid> soCaiRepository,
        IRepository<ReconRun, Guid> luotDoiSoatRepository,
        IRepository<ReconRunMismatch, Guid> dongLechRepository,
        eCommerceMetrics metrics)
    {
        _tonKhoRepository = tonKhoRepository;
        _soCaiRepository = soCaiRepository;
        _luotDoiSoatRepository = luotDoiSoatRepository;
        _dongLechRepository = dongLechRepository;
        _metrics = metrics;
    }

    /// <summary>Chính sách ngưỡng đang áp cho **một lượt** đối soát (`REC-001`). 📌 Truyền vào (không đọc setting ở đây) để tầng dịch vụ **không** phụ thuộc cấu hình — và để unit test kiểm được **không cần DB**.</summary>
    public sealed record ChinhSachNguong(DonViNguong DonVi, decimal GiaTri, HanhDongLech HanhDong, bool HopLe);

    /// <summary>Chính sách **mặc định** khi chỗ gọi không truyền gì (đường API đọc-nhanh): coi **mọi** lệch là vượt ngưỡng, hành động `Warn` — **không** tự sửa dữ liệu.</summary>
    public static readonly ChinhSachNguong ChinhSachMacDinh = new(DonViNguong.Absolute, 0m, HanhDongLech.Warn, HopLe: true);

    /// <summary>
    /// Chạy đối soát (API đọc-nhanh). Trả về **danh sách cặp `(kho, SKU)` bị lệch** (rỗng ⇒ khớp hoàn toàn).
    /// Luôn **ghi metric** `inventory_recon_mismatch_skus` — kể cả khi bằng `0` (số 0 là thông tin: *"đã kiểm và khớp"*).
    /// 📌 Nay **cũng ghi một lượt `ReconRuns`** (với chính sách mặc định) ⇒ gọi API cũng để lại **vết đã đo**.
    /// </summary>
    public Task<IReadOnlyList<DoiSoatTonKho.Lech>> DoiSoatTonKhoAsync()
        => DoiSoatTonKhoAsync(ChinhSachMacDinh);

    /// <summary>
    /// Chạy đối soát **theo một chính sách ngưỡng** (job định kỳ dùng bản này) và **ghi lịch sử lượt quét**.
    /// 🔒 Danh sách lệch trả về **KHÔNG** bị lọc theo ngưỡng: ngưỡng chỉ dùng để **phân loại/kết luận**.
    /// Cố ý như vậy — lọc bớt sẽ khiến người vận hành **không thấy** các lệch nhỏ đang tích tụ,
    /// mà đó chính là cách một lệch nhỏ trở thành lệch lớn.
    /// </summary>
    public async Task<IReadOnlyList<DoiSoatTonKho.Lech>> DoiSoatTonKhoAsync(ChinhSachNguong chinhSach)
    {
        ArgumentNullException.ThrowIfNull(chinhSach);

        var batDauUtc = DateTime.UtcNow;

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

        // 🔒 Sắp xếp TẤT ĐỊNH trước khi ghi: không sắp thì thứ tự dòng trong `ReconRunMismatches` phụ thuộc
        // thứ tự DB trả về ⇒ hai lượt quét trên CÙNG dữ liệu cho hai bảng lịch sử khác nhau (không đối soát được).
        lech = lech
            .OrderBy(x => x.WarehouseId)
            .ThenBy(x => x.Sku, StringComparer.Ordinal)
            .ToList();

        _metrics.SetInventoryReconMismatchSkus(lech.Count);

        var (soVuotNguong, soDongGhi) = await GhiLichSuLuotQuetAsync(batDauUtc, cacTon.Count, lech, chinhSach);

        if (lech.Count > 0)
        {
            // `Warning` (không phải `Error`): lệch tồn **cần người xem**, nhưng KHÔNG làm chết request nào
            // ⇒ để mức `Error` sẽ làm nhiễu tiêu chí "không có dòng `fail:`" của quy trình kiểm chứng.
            Logger.LogWarning(
                "ĐỐI SOÁT KHO: {SoLech} cặp (kho, SKU) LỆCH giữa ảnh chụp và sổ cái — trong đó {SoVuot} VƯỢT ngưỡng " +
                "{DonVi}({GiaTri}) ⇒ kết luận {KetLuan}. Chi tiết: {ChiTiet}",
                lech.Count,
                soVuotNguong,
                chinhSach.DonVi,
                chinhSach.GiaTri,
                KetLuan(soVuotNguong, chinhSach),
                string.Join("; ", lech.Take(20).Select(x =>
                    $"{x.Sku}@{x.WarehouseId}: OnHand={x.OnHand} vs sổ cái={x.TongSoCai} (lệch {x.ChenhLech})")));
        }
        else
        {
            Logger.LogInformation("ĐỐI SOÁT KHO: KHỚP — {SoTon} cặp (kho, SKU), không có lệch.", cacTon.Count);
        }

        if (soDongGhi < lech.Count)
        {
            Logger.LogWarning(
                "ĐỐI SOÁT KHO: chỉ ghi được {SoGhi}/{Tong} dòng lệch vào lịch sử (trần {Tran}/lượt). " +
                "Số lệch THẬT vẫn được lưu ở `ReconRuns.SoLech` — xem bảng `ReconRuns` để biết quy mô.",
                soDongGhi, lech.Count, SoDongLechToiDaMoiLuot);
        }

        return lech;
    }

    /// <summary>Kết luận của một lượt (dùng cho log — **cùng** luật với <see cref="ReconRun.ChotKetQua"/>).</summary>
    private static KetLuanDoiSoat KetLuan(int soVuotNguong, ChinhSachNguong chinhSach)
        => soVuotNguong <= 0
            ? KetLuanDoiSoat.KhongVuot
            : chinhSach.HanhDong == HanhDongLech.RequireConfirmation
                ? KetLuanDoiSoat.VuotCanXacNhan
                : KetLuanDoiSoat.VuotCanhBao;

    /// <summary>
    /// Ghi **một lượt đối soát** + các dòng lệch (tối đa <see cref="SoDongLechToiDaMoiLuot"/>).
    /// Trả về `(số cặp vượt ngưỡng, số dòng lệch đã ghi)`.
    ///
    /// 🚨 **VÌ SAO BỌC `try` Ở ĐÂY LÀ CỐ Ý, KHÔNG PHẢI NUỐT LỖI:**
    /// giá trị **nghiệp vụ** của lượt gọi là **danh sách lệch** (nó vẫn trả về được); việc ghi lịch sử là
    /// **bổ trợ**. Nếu bảng lịch sử có sự cố (ví dụ chưa kịp migrate) mà ta để lỗi nổi lên, thì **chính job
    /// phát hiện lệch bị chết** ⇒ mất cả hai: không có lịch sử **và** không có cảnh báo. 📌 Nhưng **KHÔNG im lặng**:
    /// ghi `Error` kèm lý do, và **xoá dấu vết entity** khỏi `ChangeTracker` — nếu để lại, một `SaveChanges`
    /// **sau đó** (bởi việc khác trong cùng UoW) sẽ **ghi lại đúng cái dòng hỏng** và nổ ở một chỗ **không liên quan**.
    /// </summary>
    private async Task<(int SoVuotNguong, int SoDongGhi)> GhiLichSuLuotQuetAsync(
        DateTime batDauUtc,
        int soCapDaDo,
        IReadOnlyList<DoiSoatTonKho.Lech> lech,
        ChinhSachNguong chinhSach)
    {
        var soVuotNguong = lech.Count(x => NguongLech.VuotNguong(chinhSach.DonVi, chinhSach.GiaTri, x.TongSoCai, x.ChenhLech));
        var soDongGhi = 0;

        try
        {
            var luot = new ReconRun(
                GuidGenerator.Create(),
                batDauUtc,
                DateTime.UtcNow,
                soCapDaDo,
                chinhSach.DonVi,
                chinhSach.GiaTri,
                chinhSach.HanhDong,
                chinhSach.HopLe);

            luot.ChotKetQua(lech.Count, soVuotNguong);

            foreach (var x in lech.Take(SoDongLechToiDaMoiLuot))
            {
                var dong = new ReconRunMismatch(
                    GuidGenerator.Create(),
                    luot.Id,
                    x.WarehouseId,
                    x.Sku,
                    x.OnHand,
                    x.TongSoCai,
                    NguongLech.VuotNguong(chinhSach.DonVi, chinhSach.GiaTri, x.TongSoCai, x.ChenhLech));

                await _dongLechRepository.InsertAsync(dong, autoSave: false);
                soDongGhi++;
            }

            // 🔒 CHỈ MỘT `SaveChanges` cho cả lượt + các dòng lệch (đặt ở lượt cuối cùng): hoặc cả lượt có
            // lịch sử đầy đủ, hoặc **không** có gì. Nếu ghi lượt trước rồi ghi dòng sau, một lỗi giữa đường để lại
            // **lượt có số lệch nhưng KHÔNG có dòng nào** ⇒ người đọc tưởng "lệch mà không biết ở đâu".
            await _luotDoiSoatRepository.InsertAsync(luot, autoSave: true);

            return (soVuotNguong, soDongGhi);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "ĐỐI SOÁT KHO: KHÔNG ghi được lịch sử lượt quét vào `ReconRuns` (số lệch thật vẫn được TRẢ VỀ và ghi log ở trên). " +
                "Kiểm migration/bảng `ReconRuns`/`ReconRunMismatches`.");

            return (soVuotNguong, soDongGhi);
        }
    }
}
