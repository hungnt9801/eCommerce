namespace eCommerce.Inventory;

/// <summary>
/// **HÀM THUẦN: tổng có DẤU của sổ cái** ⇒ dùng để **đối soát** với `InventoryItem.OnHand`
/// (`INV-001` **ADR-10**).
///
/// 🚨 **VÌ SAO PHẢI CÓ PHÉP ĐỐI SOÁT NÀY:** `InventoryItem.OnHand` là **ảnh chụp để đọc nhanh**; **nguồn sự thật
/// là sổ cái** (chính tài liệu này nói vậy ngay ở phần mô tả entity). Ảnh chụp **sẽ** lệch khi có bug, khi có
/// người sửa SQL tay, khi transaction đứt giữa đường — và lệch **âm thầm**: báo cáo vẫn ra số, chỉ là số **sai**.
/// ⇒ Job đối soát + **metric** là cách **duy nhất** phát hiện điều đó **trước khi** khách/kế toán phát hiện.
///
/// ⚠️ **QUY TẮC DẤU (chỗ dễ sai nhất — `Reservation` KHÔNG thuộc `OnHand`):**
///
/// | Loại biến động | Ảnh hưởng |
/// |---|---|
/// | `Receipt` · `CustomerReturn` · `Adjustment` · `Transfer` · `StocktakeGain` · `TransferIn` | **+** vào `OnHand` |
/// | `Sale` · `StocktakeLoss` · `TransferOut` | **−** khỏi `OnHand` |
/// | `Reservation` · `ReservationRelease` | 🚫 **KHÔNG** ảnh hưởng `OnHand` — chúng đổi **`Reserved`** |
///
/// 📌 Nếu cộng **cả** `Reservation` vào tổng thì phép đối soát **báo lệch GIẢ** ngay khi có đơn giữ chỗ
/// ⇒ đội vận hành **mất tin** vào cảnh báo ⇒ cảnh báo thật sau này **bị bỏ qua**. Đây là cái chết điển hình
/// của hệ thống giám sát (*"alert fatigue"*).
/// </summary>
public static class DoiSoatTonKho
{
    /// <summary>Bút toán có ảnh hưởng `OnHand` hay không.</summary>
    public static bool AnhHuongOnHand(StockMovementType loai)
        => loai is not (StockMovementType.Reservation or StockMovementType.ReservationRelease);

    /// <summary>Dấu của bút toán đối với `OnHand`: `+1`, `-1`, hoặc `0` (không ảnh hưởng).</summary>
    public static int Dau(StockMovementType loai) => loai switch
    {
        StockMovementType.Receipt => +1,
        StockMovementType.CustomerReturn => +1,
        StockMovementType.Adjustment => +1,
        StockMovementType.Transfer => +1,
        StockMovementType.StocktakeGain => +1,
        StockMovementType.TransferIn => +1,

        StockMovementType.Sale => -1,
        StockMovementType.StocktakeLoss => -1,
        StockMovementType.TransferOut => -1,

        // Giữ chỗ đổi `Reserved`, KHÔNG đổi `OnHand`.
        StockMovementType.Reservation => 0,
        StockMovementType.ReservationRelease => 0,

        _ => throw new ArgumentOutOfRangeException(nameof(loai), loai, "Loại biến động kho không hỗ trợ.")
    };

    /// <summary>Tổng có dấu của **mọi** bút toán (bỏ qua loại không ảnh hưởng `OnHand`).</summary>
    public static int Tong(IEnumerable<(StockMovementType Loai, int DoLon)> butToan)
    {
        ArgumentNullException.ThrowIfNull(butToan);

        var tong = 0;

        foreach (var (loai, doLon) in butToan)
        {
            tong += Dau(loai) * doLon;
        }

        return tong;
    }

    /// <summary>Một cặp `(kho, SKU)` bị lệch giữa ảnh chụp và sổ cái.</summary>
    public sealed record Lech(Guid WarehouseId, string Sku, int OnHand, int TongSoCai)
    {
        public int ChenhLech => OnHand - TongSoCai;
    }
}
