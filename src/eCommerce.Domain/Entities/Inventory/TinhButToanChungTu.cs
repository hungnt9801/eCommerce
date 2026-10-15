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
    public sealed record ButToan(Guid WarehouseId, StockMovementType Loai, int DoLon, string MoTa);

    /// <summary>
    /// Bút toán cho **một dòng** chứng từ (trừ kiểm kê — xem <see cref="ChoTungKiemKe"/>).
    /// </summary>
    public static IReadOnlyList<ButToan> ChoTungDong(
        StockDocumentType loai, int soLuong, Guid khoNguon, Guid? khoDich)
    {
        if (soLuong < 1)
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
    /// Bút toán **kiểm kê**: so **số đếm được** với **tồn trên sổ**, ghi phần **LỆCH**.
    /// Trả `null` khi **khớp** (không lệch) — **KHÔNG** ghi bút toán 0 (DB có `CHECK QuantityDelta &lt;&gt; 0`
    /// và một dòng sổ cái "0" chỉ làm nhiễu đối soát).
    /// </summary>
    public static ButToan? ChoTungKiemKe(Guid khoId, string sku, int soDemDuoc, int tonTrenSo)
    {
        var lech = soDemDuoc - tonTrenSo;

        if (lech == 0)
        {
            return null;
        }

        // Lệch DƯƠNG ⇒ đếm được NHIỀU hơn sổ ⇒ `StocktakeGain` (tăng `OnHand`).
        // Lệch ÂM ⇒ thiếu ⇒ `StocktakeLoss` (giảm `OnHand`). Truyền **độ lớn dương** (xem ghi chú quy ước dấu).
        return lech > 0
            ? new ButToan(khoId, StockMovementType.StocktakeGain, lech, $"Kiểm kê {sku}: thừa {lech}")
            : new ButToan(khoId, StockMovementType.StocktakeLoss, -lech, $"Kiểm kê {sku}: thiếu {-lech}");
    }
}
