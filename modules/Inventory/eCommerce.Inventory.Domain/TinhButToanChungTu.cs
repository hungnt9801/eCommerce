namespace eCommerce.Inventory;

/// <summary>
/// **HÀM THUẦN: quyết định bút toán sổ cái cho một dòng chứng từ** (`INV-001`).
///
/// 📌 **VÌ SAO TÁCH RA HÀM THUẦN:** đây là chỗ **quyết định tồn kho tăng hay giảm** — sai một dấu là
/// **tồn kho sai toàn hệ thống** (bán thứ không có, hoặc không bán được thứ đang có) mà **không có lỗi nào**.
/// Tách ra thì test được **không cần DB**, và luật nằm **một chỗ** (không rải trong service).
///
/// ⚠️ **QUY ƯỚC DẤU (dễ sai nhất):** `StockMovementType` đã **tự quy định chiều** trong `ApplyMovement`
/// (`TransferOut`/`StocktakeLoss`/`Sale` ⇒ **giảm**), nên tham số truyền vào luôn là **độ lớn DƯƠNG**;
/// **KHÔNG** truyền số âm (truyền âm sẽ **đảo chiều** nghiệp vụ: "xuất kho −5" thành **nhập 5**).
/// ⇒ Hàm này trả về **độ lớn dương** kèm **loại biến động** đã đúng chiều.
/// </summary>
public static class TinhButToanChungTu
{
    /// <summary>Một bút toán dự kiến: kho nào · loại biến động nào · độ lớn bao nhiêu.</summary>
    public sealed record ButToan(Guid WarehouseId, StockMovementType Loai, decimal DoLon, string MoTa);

    /// <summary>
    /// Bút toán cho **một dòng** chứng từ (trừ kiểm kê — xem <see cref="ChoTungKiemKe"/>).
    /// </summary>
    public static IReadOnlyList<ButToan> ChoTungDong(
        StockDocumentType loai, decimal soLuong, Guid khoNguon, Guid? khoDich)
    {
        if (soLuong <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(soLuong), soLuong, "Số lượng dòng phải ≥ 1.");
        }

        switch (loai)
        {
            case StockDocumentType.CustomerReturn:
                // 📌 **TÁCH RIÊNG khỏi `GoodsReceipt`** dù cùng làm TĂNG `OnHand`: báo cáo *"đã bán bao nhiêu"*
                // và *"khách trả bao nhiêu"* là hai câu hỏi khác nhau (`inv-001` §30.4 #4).
                return [new ButToan(khoNguon, StockMovementType.CustomerReturn, soLuong, "Khách trả hàng — nhập lại kho")];

            case StockDocumentType.GoodsReceipt:
                // Nhập kho (+OnHand). Khách trả hàng dùng CÙNG loại chứng từ này theo §1 Alternative Flow A1;
                // `CustomerReturn` là **loại biến động** chi tiết hơn về mặt báo cáo.
                return [new ButToan(khoNguon, StockMovementType.Receipt, soLuong, "Nhập kho")];

            case StockDocumentType.StockTransfer:
                if (khoDich is null)
                {
                    throw new ArgumentException("Chuyển kho phải có kho đích.", nameof(khoDich));
                }

                // HAI bút toán: **ĐI** ở kho nguồn và **ĐẾN** ở kho đích. Thiếu một vế ⇒ hàng **biến mất**
                // (chỉ trừ nguồn) hoặc **sinh ra từ không khí** (chỉ cộng đích) — cả hai đều là **mất/hư dữ liệu**.
                return
                [
                    new ButToan(khoNguon, StockMovementType.TransferOut, soLuong, "Chuyển kho — phần ĐI"),
                    new ButToan(khoDich.Value, StockMovementType.TransferIn, soLuong, "Chuyển kho — phần ĐẾN")
                ];

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(loai), loai, "Loại chứng từ này cần dữ liệu thực tế (kiểm kê) — dùng ChoTungKiemKe.");
        }
    }

    /// <summary>
    /// Bút toán cho **một dòng** chứng từ **có kiểu phiếu Nhanh** (`WP-04`): chọn **loại biến động** theo
    /// `(mode, direction)`, rồi trả **độ lớn dương** như <see cref="ChoTungDong(StockDocumentType,int,Guid,Guid?)"/>.
    ///
    /// 🚨 **VÌ SAO PHẢI TRA THEO `mode`:** cùng là *"phiếu xuất"* nhưng `mode = 1 Giao hàng` phải ghi `ShippingOut`
    /// (hàng còn trên đường), `mode = 2 Bán lẻ` ghi `RetailSaleOut` (đã bán), `mode = 5 Nhà cung cấp` ghi
    /// `SupplierReturnOut` (trả NCC), `mode = 13 Phiếu bảo hành` ghi `WarrantyHold` (hàng đi bảo hành).
    /// Ghi tất cả thành `Sale` thì **báo cáo doanh thu và tồn theo trạng thái sai toàn bộ** — đúng họ lỗi đã trả giá
    /// với kiểm kê trước `INV-001`.
    ///
    /// 📌 **KHÔNG có `mode` ⇒ giữ nguyên hành vi cũ** (suy từ `loai`) — phiếu cũ và mọi đường gọi hiện tại không đổi.
    /// </summary>
    public static IReadOnlyList<ButToan> ChoTungDongTheoMode(
        StockDocumentType loai, StockDocumentMode? mode, StockDocumentDirection? huong,
        decimal soLuong, Guid khoNguon, Guid? khoDich)
    {
        if (soLuong <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(soLuong), soLuong, "Số lượng dòng phải ≥ 1.");
        }

        // Kiểm kê: số đếm thực tế ⇒ phần lệch do `ChoTungKiemKe` quyết định (không dùng mode).
        if (loai == StockDocumentType.Stocktake)
        {
            throw new ArgumentOutOfRangeException(
                nameof(loai), loai, "Kiểm kê cần dữ liệu thực tế — dùng ChoTungKiemKe.");
        }

        // Chuyển kho: HAI vế (đi/đến) — mode/direction không đổi được điều này.
        if (loai == StockDocumentType.StockTransfer)
        {
            return ChoTungDong(loai, soLuong, khoNguon, khoDich);
        }

        // Khách trả hàng: LOẠI CHỨNG TỪ đã nói rõ nghĩa ⇒ mode chỉ để phân loại báo cáo, không đổi bút toán.
        if (loai == StockDocumentType.CustomerReturn)
        {
            return ChoTungDong(loai, soLuong, khoNguon, khoDich);
        }

        if (mode is null || huong is null)
        {
            return ChoTungDong(loai, soLuong, khoNguon, khoDich);
        }

        var bienDong = ChonBienDongTheoMode(mode.Value, huong.Value);

        return [new ButToan(khoNguon, bienDong, soLuong, $"Phiếu {mode}/{huong}")];
    }

    /// <summary>
    /// **HÀM THUẦN: `(mode, direction) → StockMovementType`** — bảng ánh xạ duy nhất của `WP-04`.
    ///
    /// 📌 **Quà tặng / combo** đi theo bút toán của **hoá đơn mẹ** (`RetailGift → RetailSaleOut`,
    /// `WholesaleGift`/`OrderGift`/`Combo → Sale`): Nhanh dùng `mode` để **tính tiền/báo cáo**, còn **chiều tồn**
    /// là chiều của hoá đơn. Tạo thêm bút toán riêng cho quà tặng sẽ làm **tổng xuất kho bị đếm hai lần**.
    /// </summary>
    public static StockMovementType ChonBienDongTheoMode(StockDocumentMode mode, StockDocumentDirection huong)
    {
        if (huong == StockDocumentDirection.In)
        {
            return mode switch
            {
                StockDocumentMode.WarrantyBill => StockMovementType.WarrantyRelease,
                StockDocumentMode.WarrantyCenter => StockMovementType.WarrantyRelease,
                StockDocumentMode.RepairBill => StockMovementType.WarrantyRelease,
                StockDocumentMode.Other => StockMovementType.Adjustment,
                _ => StockMovementType.Receipt
            };
        }

        return mode switch
        {
            StockDocumentMode.Delivery => StockMovementType.ShippingOut,
            StockDocumentMode.Retail => StockMovementType.RetailSaleOut,
            StockDocumentMode.RetailGift => StockMovementType.RetailSaleOut,
            StockDocumentMode.Wholesale => StockMovementType.Sale,
            StockDocumentMode.WholesaleGift => StockMovementType.Sale,
            StockDocumentMode.OrderGift => StockMovementType.Sale,
            StockDocumentMode.Combo => StockMovementType.Sale,
            StockDocumentMode.Supplier => StockMovementType.SupplierReturnOut,
            StockDocumentMode.Other => StockMovementType.OtherOut,
            StockDocumentMode.WarrantyBill => StockMovementType.WarrantyHold,
            StockDocumentMode.WarrantyCenter => StockMovementType.WarrantyHold,
            StockDocumentMode.RepairBill => StockMovementType.WarrantyHold,
            StockDocumentMode.WarrantyPart => StockMovementType.WarrantyHold,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Kiểu phiếu không có bút toán tương ứng.")
        };
    }

    /// <summary>
    /// Bút toán **kiểm kê**: so **số đếm được** với **tồn trên sổ**, ghi phần **LỆCH**.
    /// Trả `null` khi **khớp** (không lệch) — **KHÔNG** ghi bút toán 0 (DB có `CHECK QuantityDelta &lt;&gt; 0`
    /// và một dòng sổ cái "0" chỉ làm nhiễu đối soát).
    /// </summary>
    public static ButToan? ChoTungKiemKe(Guid khoId, string sku, decimal soDemDuoc, decimal tonTrenSo)
    {
        var lech = soDemDuoc - tonTrenSo;

        if (lech == 0m)
        {
            return null;
        }

        // Lệch DƯƠNG ⇒ đếm được NHIỀU hơn sổ ⇒ `StocktakeGain` (tăng `OnHand`).
        // Lệch ÂM ⇒ thiếu ⇒ `StocktakeLoss` (giảm `OnHand`). Truyền **độ lớn dương** (xem ghi chú quy ước dấu).
        return lech > 0m
            ? new ButToan(khoId, StockMovementType.StocktakeGain, lech, $"Kiểm kê {sku}: thừa {lech}")
            : new ButToan(khoId, StockMovementType.StocktakeLoss, -lech, $"Kiểm kê {sku}: thiếu {-lech}");
    }
}
