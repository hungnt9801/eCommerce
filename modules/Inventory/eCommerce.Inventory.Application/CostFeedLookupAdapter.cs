using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Inventory;

/// <summary>
/// 🆕 **`W17-2` — ADAPTER ĐỌC SỔ CÁI KHO CHO ĐỘNG CƠ GIÁ VỐN** (`PRC-001-arch` §3.3; `D-321`/`D-322`).
///
/// 🔒 **CHỈ ĐỌC** sổ cái (`StockLedgerEntry` — chỉ dòng **đã ghi sổ**) + `join` đơn giá nhập
/// (`StockDocumentLine.UnitPrice` qua `ReferenceLineId`). **Không** ghi sổ cái, **không** sửa chứng từ.
/// 📌 Nằm trong module Inventory nên **được** đọc entity Domain của chính module; Pricing chỉ thấy DTO/port.
///
/// 🚨 **BẢNG ÁNH XẠ LOẠI (`D-321`) — PHẢI khớp `DoiSoatTonKho.Dau()` (trừ nhóm chuyển kho):**
///   • `NhapCoGia`  = `Receipt=0` **VÀ** `UnitPrice != null` (chủ yếu phiếu `GoodsReceipt`).
///   • `NhapKhongGia` = `CustomerReturn=4` · `Adjustment=5` · `StocktakeGain=7` · `ShippingReturn=12` ·
///     `WarrantyRelease=16` · **và** `Receipt=0` có `UnitPrice == null`.
///   • `Xuat`       = `Sale=3` · `StocktakeLoss=8` · `ShippingOut=11` · `DamagedOut=14` · `WarrantyHold=15` ·
///     `RetailSaleOut=19` · `SupplierReturnOut=20` · `OtherOut=21`.
///   • **BỎ** (không đổi `OnHand` cấp SKU) = `Reservation=1` · `ReservationRelease=2` · `DamagedIn=13` ·
///     `Hold=17` · `HoldRelease=18`; **và CẢ CẶP chuyển kho** `Transfer=6`/`TransferOut=9`/`TransferIn=10`
///     (net 0 cấp SKU — `D-318` C9) ⇒ **tuyệt đối KHÔNG** xử lý `TransferIn` như `NhapKhongGia`.
/// 🚨 Loại **lạ** / **lệch dấu** ⇒ **NÉM** `eCommerce:Pricing:0014` (fail-closed) — bỏ qua im lặng làm bình quân
/// các kỳ sau sai mà **không có lỗi nào**.
/// </summary>
public class CostFeedLookupAdapter : ICostFeedLookup, ITransientDependency
{
    /// <summary>
    /// Mã lỗi **của module Pricing** dùng ở biên fail-closed (`PRC-001` `0014`). 📌 Khai bằng chuỗi vì module
    /// Inventory **KHÔNG** tham chiếu `PricingErrorCodes` (ranh giới module — `M2`); đây là hợp đồng mã lỗi đã chốt.
    /// </summary>
    private const string MaCauHinhGiaVonKhongHopLe = "eCommerce:Pricing:0014";

    /// <summary>Múi giờ kinh doanh VN = UTC+7 (`D-177` ③).</summary>
    private const int LechGioVnSoVoiUtc = 7;

    private readonly IRepository<StockLedgerEntry, Guid> _soCai;
    private readonly IRepository<StockDocumentLine, Guid> _dongChungTu;

    public CostFeedLookupAdapter(
        IRepository<StockLedgerEntry, Guid> soCai,
        IRepository<StockDocumentLine, Guid> dongChungTu)
    {
        _soCai = soCai;
        _dongChungTu = dongChungTu;
    }

    /// <inheritdoc />
    public async Task<List<CostMovementDto>> LayChuyenDongAsync(
        DateOnly from, DateOnly to, IReadOnlyCollection<string>? skus = null)
    {
        if (to < from)
        {
            throw new BusinessException(MaCauHinhGiaVonKhongHopLe)
                .WithData("lyDo", "`to` phải ≥ `from`.")
                .WithData("from", from)
                .WithData("to", to);
        }

        // Ngày kinh doanh `d` (UTC+7) ⇔ UTC [d 00:00 − 7h, (d+1) 00:00 − 7h).
        var tuUtc = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddHours(-LechGioVnSoVoiUtc);
        var denUtc = new DateTime(to.Year, to.Month, to.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(1)
            .AddHours(-LechGioVnSoVoiUtc);

        var danhSachSku = skus is null ? null : skus.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

        var butToan = danhSachSku is { Count: > 0 }
            ? await _soCai.GetListAsync(x => x.OccurredAtUtc >= tuUtc && x.OccurredAtUtc < denUtc
                                             && danhSachSku.Contains(x.Sku))
            : await _soCai.GetListAsync(x => x.OccurredAtUtc >= tuUtc && x.OccurredAtUtc < denUtc);

        // Chỉ `Receipt` cần đơn giá nhập; nạp MỘT lần theo `ReferenceLineId` (tránh N+1).
        var idDongCanTraCuu = butToan
            .Where(x => x.MovementType == StockMovementType.Receipt && x.ReferenceLineId is not null)
            .Select(x => x.ReferenceLineId!.Value)
            .Distinct()
            .ToList();

        var donGiaTheoDong = idDongCanTraCuu.Count == 0
            ? new Dictionary<Guid, decimal?>()
            : (await _dongChungTu.GetListAsync(x => idDongCanTraCuu.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x.UnitPrice);

        var ketQua = new List<CostMovementDto>(butToan.Count);

        foreach (var but in butToan)
        {
            var ngayKinhDoanh = DateOnly.FromDateTime(but.OccurredAtUtc.AddHours(LechGioVnSoVoiUtc));

            // `OccurredAtUtc` là `timestamptz` nhưng cửa sổ UTC chỉ là xấp xỉ theo ngày — lọc lại chính xác
            // theo NGÀY KINH DOANH (UTC+7), vì ranh giới ngày lệch 7 giờ.
            if (ngayKinhDoanh < from || ngayKinhDoanh > to)
            {
                continue;
            }

            var chuyenDong = AnhXa(but, ngayKinhDoanh, donGiaTheoDong);

            if (chuyenDong is not null)
            {
                ketQua.Add(chuyenDong);
            }
        }

        return ketQua
            .OrderBy(x => x.NgayKinhDoanh)
            .ThenBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.ReferenceLineId)
            .ToList();
    }

    /// <summary>
    /// Ánh xạ một bút toán → chuyển động định giá. Trả `null` khi loại **được bỏ có chủ đích** (`D-321`);
    /// **NÉM `0014`** khi loại **lạ** hoặc **lệch dấu**. `QuantityDelta` của sổ cái là **độ lớn dương** (dấu do
    /// `MovementType` quyết định — `TinhButToanChungTu`/`ApplyMovement`) ⇒ đầu ra phải áp dấu tường minh.
    /// </summary>
    private static CostMovementDto? AnhXa(
        StockLedgerEntry but, DateOnly ngayKinhDoanh, IReadOnlyDictionary<Guid, decimal?> donGiaTheoDong)
    {
        var loai = but.MovementType;

        // 🚨 Độ lớn phải DƯƠNG: âm/không là lệch quy ước dấu ⇒ NÉM (không đoán).
        if (but.QuantityDelta <= 0m)
        {
            throw LoiLechDau(but);
        }

        var referenceLineId = but.ReferenceLineId ?? but.Id;

        switch (loai)
        {
            case StockMovementType.Receipt:
            {
                var coDonGia = but.ReferenceLineId is not null
                    && donGiaTheoDong.TryGetValue(but.ReferenceLineId.Value, out var donGia)
                    && donGia is not null;

                return new CostMovementDto
                {
                    Sku = but.Sku,
                    NgayKinhDoanh = ngayKinhDoanh,
                    OccurredAtUtc = but.OccurredAtUtc,
                    Loai = coDonGia ? LoaiChuyenDongGiaVon.NhapCoGia : LoaiChuyenDongGiaVon.NhapKhongGia,
                    SoLuongDelta = but.QuantityDelta,
                    DonGiaNhap = coDonGia ? donGiaTheoDong[but.ReferenceLineId!.Value] : null,
                    TienTeNhap = null, // VND của tenant (D-321)
                    ReferenceLineId = referenceLineId
                };
            }

            case StockMovementType.CustomerReturn:
            case StockMovementType.Adjustment:
            case StockMovementType.StocktakeGain:
            case StockMovementType.ShippingReturn:
            case StockMovementType.WarrantyRelease:
                return new CostMovementDto
                {
                    Sku = but.Sku,
                    NgayKinhDoanh = ngayKinhDoanh,
                    OccurredAtUtc = but.OccurredAtUtc,
                    Loai = LoaiChuyenDongGiaVon.NhapKhongGia,
                    SoLuongDelta = but.QuantityDelta,
                    DonGiaNhap = null,
                    TienTeNhap = null,
                    ReferenceLineId = referenceLineId
                };

            case StockMovementType.Sale:
            case StockMovementType.StocktakeLoss:
            case StockMovementType.ShippingOut:
            case StockMovementType.DamagedOut:
            case StockMovementType.WarrantyHold:
            case StockMovementType.RetailSaleOut:
            case StockMovementType.SupplierReturnOut:
            case StockMovementType.OtherOut:
                return new CostMovementDto
                {
                    Sku = but.Sku,
                    NgayKinhDoanh = ngayKinhDoanh,
                    OccurredAtUtc = but.OccurredAtUtc,
                    Loai = LoaiChuyenDongGiaVon.Xuat,
                    SoLuongDelta = -but.QuantityDelta,
                    DonGiaNhap = null,
                    TienTeNhap = null,
                    ReferenceLineId = referenceLineId
                };

            // ── BỎ có chủ đích (không đổi `OnHand` cấp SKU — D-321/D-318 C9) ─────────────────────────
            case StockMovementType.Reservation:
            case StockMovementType.ReservationRelease:
            case StockMovementType.DamagedIn:
            case StockMovementType.Hold:
            case StockMovementType.HoldRelease:
            case StockMovementType.Transfer:
            case StockMovementType.TransferOut:
            case StockMovementType.TransferIn:
                return null;

            default:
                throw new BusinessException(MaCauHinhGiaVonKhongHopLe)
                    .WithData("sku", but.Sku)
                    .WithData("movementType", (int)loai)
                    .WithData("lyDo", "Loại biến động kho LẠ — không có trong bảng ánh xạ định giá (D-321).");
        }
    }

    private static BusinessException LoiLechDau(StockLedgerEntry but)
        => new BusinessException(MaCauHinhGiaVonKhongHopLe)
            .WithData("sku", but.Sku)
            .WithData("movementType", (int)but.MovementType)
            .WithData("quantityDelta", but.QuantityDelta)
            .WithData("lyDo", "`QuantityDelta` phải DƯƠNG (độ lớn); dấu do `MovementType` quyết định (D-321).");
}
