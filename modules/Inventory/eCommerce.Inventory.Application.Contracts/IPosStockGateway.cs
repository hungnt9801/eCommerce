namespace eCommerce.Inventory;

/// <summary>
/// Một dòng hàng của chứng từ kho do **POS** sinh ra (bán lẻ / nhập lại hàng trả).
/// 📌 `Quantity` là **`decimal`** (không phải `int`): hàng cân đo bán 0,350 kg — làm tròn về `int` sẽ
/// **bán hộ 650 g** hoặc **mất hàng** (bài học `N4b`).
/// </summary>
public sealed record PosStockLine(string Sku, decimal Quantity, decimal UnitPrice, decimal? VatPercent = null);

/// <summary>
/// 🆕 **`WP-06`** — **HỢP ĐỒNG GHI TỒN KHO CHO POS** (module `Inventory` cho module `Pos` gọi).
///
/// 🔑 **VÌ SAO LÀ HỢP ĐỒNG (không gọi `IStockDocumentAppService`):**
/// <list type="bullet">
///   <item>App service kho mang `[Authorize(Inventory.Default)]` ở **cấp class** ⇒ gọi trong tiến trình bằng
///     principal của POS (không có quyền kho) ⇒ **`AbpAuthorizationException`** — đúng lỗi đã đo ở `SAL-005`
///     (`ICustomerReturnGateway` ra đời vì lý do này);</item>
///   <item>Và quan trọng hơn: POS **không được** tự ghi sổ cái. Nếu POS tự gọi `StockPostingService` thì có
///     **đường ghi thứ hai** ⇒ mất khoá phân tán theo `(tenant, kho, SKU)`, mất `ReferenceLineId` unique,
///     mất bất biến append-only. Cổng này giữ **một** đường ghi: `StockPostingService` của `Inventory`.</item>
/// </list>
///
/// 📌 **Quyền đã kiểm ở app service POS** (`Pos.Sell` / `Pos.Return`) — cổng này **không** kiểm lại.
/// 🔒 **Idempotent theo `number`**: gọi lại cùng số chứng từ trả **chứng từ đã có**, không đẩy tồn lần hai.
/// </summary>
public interface IPosStockGateway
{
    /// <summary>
    /// **XUẤT KHO BÁN LẺ** — tạo + ghi sổ chứng từ `GoodsIssue` với `Mode = Retail` (bút toán `RetailSaleOut`).
    /// 🚨 Ném `eCommerce:Inventory:0001` khi **không đủ tồn** — POS phải hiện đúng mã này cho thu ngân
    /// (không phải "hệ thống lỗi"), vì cách xử lý là **sửa số lượng**.
    /// </summary>
    Task<Guid> XuatBanLeAsync(
        Guid warehouseId,
        string number,
        string retailBillNumber,
        IReadOnlyCollection<PosStockLine> lines,
        DateTime atUtc,
        string? note = null);

    /// <summary>
    /// **NHẬP LẠI KHO HÀNG TRẢ / HÀNG HUỶ HOÁ ĐƠN** — tạo + ghi sổ chứng từ `CustomerReturn`
    /// (bút toán `CustomerReturn`, tăng `OnHand`). Dùng chung cho **phiếu trả hàng** và **huỷ hoá đơn**
    /// (huỷ = đảo đúng số đã xuất, khác nhau ở `number` để tra được vết).
    /// </summary>
    Task<Guid> NhapHangTraAsync(
        Guid warehouseId,
        string number,
        string retailBillNumber,
        IReadOnlyCollection<PosStockLine> lines,
        DateTime atUtc,
        string? note = null);
}
