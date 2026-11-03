namespace eCommerce.Inventory;

/// <summary>Loại **chứng từ kho** (`INV-001` §1 — ba nghiệp vụ nền).</summary>
public enum StockDocumentType
{
    /// <summary>Phiếu **nhập kho** (hàng về; cũng dùng cho khách trả hàng — §1 Alternative Flow A1).</summary>
    GoodsReceipt = 0,

    /// <summary>Phiếu **chuyển kho** (giữa hai kho).</summary>
    StockTransfer = 1,

    /// <summary>Phiếu **kiểm kê** (đếm thực tế; ghi phần **lệch**).</summary>
    Stocktake = 2,

    // 🚨 **THÊM Ở CUỐI DẢI — KHÔNG BAO GIỜ ĐỔI SỐ CỦA GIÁ TRỊ CŨ.**
    // `Type` lưu dạng **SỐ** trong `InvStockDocuments.Type` ⇒ đổi số của giá trị cũ sẽ **diễn giải lại
    // toàn bộ lịch sử kho** (phiếu nhập thành phiếu kiểm kê…) mà **không có lỗi nào**.
    // 📌 Trước vòng 39, phiếu **trả hàng** phải "mượn" `GoodsReceipt` ⇒ sổ cái ghi `Receipt` ⇒
    // báo cáo *"nhập kho"* **lẫn** hàng nhập mua với hàng khách trả ⇒ **không tách được** `bán/trả`.

    /// <summary>Phiếu **trả hàng** (khách gửi lại) ⇒ biến động `CustomerReturn` (`SAL-004`).</summary>
    CustomerReturn = 3,

    /// <summary>
    /// 🆕 **`WP-04`** — Phiếu **XUẤT kho** (Nhanh: `bill.type = 2`).
    ///
    /// 🚨 **VÌ SAO PHẢI THÊM (đo được khi chạy probe HTTP, không phải suy đoán):** trước `WP-04` enum chỉ có
    /// `GoodsReceipt`/`StockTransfer`/`Stocktake`/`CustomerReturn` ⇒ **KHÔNG có loại phiếu nào** diễn đạt
    /// *"xuất kho vì bán lẻ / giao hàng / trả NCC"*. Không có nó thì chiều `Out` **không tồn tại** cho các phiếu
    /// đó, và cách duy nhất là **mượn** `StockTransfer` (sai nghĩa: báo cáo chuyển kho đếm cả hàng bán).
    /// 📌 Phiếu `GoodsIssue` **bắt buộc có `Mode`** (không có hành vi cũ để giữ): `mode` quyết định bút toán
    /// (`Retail ⇒ RetailSaleOut`, `Delivery ⇒ ShippingOut`, `Supplier ⇒ SupplierReturnOut`… — D-28).
    /// 🔒 Thêm ở CUỐI dải (D-17).
    /// </summary>
    GoodsIssue = 4
}

/// <summary>Vòng đời chứng từ kho (`INV-001` **ADR-02**: **chứng từ + ghi sổ là HAI bước**).</summary>
public enum StockDocumentStatus
{
    /// <summary>Nháp — **CHƯA** ảnh hưởng tồn kho.</summary>
    Draft = 0,

    /// <summary>Đã ghi sổ — **ĐÃ** sinh bút toán sổ cái; **KHÔNG SỬA ĐƯỢC**.</summary>
    Posted = 1,

    /// <summary>Đã huỷ (chỉ huỷ được khi còn `Draft`).</summary>
    Cancelled = 2
}

/// <summary>Trạng thái một lượt **giữ chỗ** (`INV-002`).</summary>
public enum ReservationStatus
{
    /// <summary>Đang giữ — hàng đã bị trừ khỏi `Available` nhưng **chưa** xuất kho.</summary>
    Held = 0,

    /// <summary>Đã **nhả** (khách huỷ / hết TTL) — hàng trở lại `Available`.</summary>
    Released = 1,

    /// <summary>Đã **chốt** thành xuất kho thật (đơn được xác nhận).</summary>
    Committed = 2,

    /// <summary>Hết hạn (do job nhả) — **phân biệt** với `Released` để biết *vì sao* hàng được nhả.</summary>
    Expired = 3
}

/// <summary>
/// 🆕 **`WP-04`** — **CHIỀU** của một phiếu kho (Nhanh: `bill.type` = **1 nhập / 2 xuất**).
///
/// 📌 **VÌ SAO LÀ CỘT RIÊNG (không suy từ `StockDocumentType`):** tab *"Nhập / Xuất"* của màn DS phiếu kho lọc
/// **theo chiều**, và người dùng Nhanh đọc phiếu theo chiều (nhập NCC và xuất NCC **cùng một mode** `Supplier`).
/// Suy từ `Type` thì không phân biệt được hai phiếu đó.
/// 📌 **NULLABLE có chủ ý** cho **kiểm kê**: một phiếu kiểm kê có thể **vừa thừa vừa thiếu** ở các dòng khác
/// nhau ⇒ **không** có một chiều duy nhất; gán bừa `In`/`Out` sẽ làm lệch báo cáo nhập/xuất.
/// 🔒 **CHỈ THÊM Ở CUỐI DẢI** (D-17) — cột lưu dạng **số**.
/// </summary>
public enum StockDocumentDirection
{
    /// <summary>Phiếu **NHẬP** (hàng vào kho).</summary>
    In = 1,

    /// <summary>Phiếu **XUẤT** (hàng ra khỏi kho).</summary>
    Out = 2
}

/// <summary>
/// 🆕 **`WP-04`** — **KIỂU PHIẾU** kiểu Nhanh (`bill.mode`).
///
/// 🚨 **SỐ PHẢI TRÙNG DANH MỤC NHANH** (`nhanh-pos-v3-api-inventory.md` §bill / Model Constant):
/// `1 Giao hàng · 2 Bán lẻ · 3 Chuyển kho · 4 Quà tặng HĐ bán lẻ · 5 Nhà cung cấp · 6 Bán sỉ ·
/// 8 Kiểm kho · 10 Khác · 13 Phiếu bảo hành · 14 Trung tâm bảo hành · 15 Phiếu sửa chữa ·
/// 16 Linh kiện bảo hành · 17 Quà tặng HĐ bán sỉ · 18 Quà tặng đơn hàng · 19 Combo`.
/// 📌 **KHÔNG có 7, 9, 11, 12** (Nhanh không dùng). **KHÔNG** đánh số lại: số là **hợp đồng dữ liệu** với dữ
/// liệu nhập từ Nhanh/Open API — đánh lại số sẽ **diễn giải sai** mọi phiếu đã có.
/// ⚠️ Tài liệu [07](../../docs/nhanh/07-ke-hoach-trien-khai.md) ghi *"Mode (19 giá trị)"*; **19 là giá trị LỚN
/// NHẤT** trong danh mục, còn danh mục **đang dùng** là **15** giá trị dưới đây. Ghi rõ để phiên sau không đi
/// tìm 4 giá trị không tồn tại.
/// 🔒 **CHỈ THÊM Ở CUỐI DẢI** (D-17) khi Nhanh công bố thêm.
/// </summary>
public enum StockDocumentMode
{
    /// <summary>1 — Giao hàng (xuất cho đơn giao vận).</summary>
    Delivery = 1,

    /// <summary>2 — Bán lẻ (POS).</summary>
    Retail = 2,

    /// <summary>3 — Chuyển kho.</summary>
    Transfer = 3,

    /// <summary>4 — Quà tặng ở hoá đơn bán lẻ.</summary>
    RetailGift = 4,

    /// <summary>5 — Nhà cung cấp (nhập mua / xuất trả NCC).</summary>
    Supplier = 5,

    /// <summary>6 — Bán sỉ.</summary>
    Wholesale = 6,

    /// <summary>8 — Kiểm kho.</summary>
    Stocktake = 8,

    /// <summary>10 — Khác (nhập/xuất điều chỉnh không thuộc loại nào).</summary>
    Other = 10,

    /// <summary>13 — Phiếu bảo hành.</summary>
    WarrantyBill = 13,

    /// <summary>14 — Trung tâm bảo hành.</summary>
    WarrantyCenter = 14,

    /// <summary>15 — Phiếu sửa chữa.</summary>
    RepairBill = 15,

    /// <summary>16 — Linh kiện bảo hành.</summary>
    WarrantyPart = 16,

    /// <summary>17 — Quà tặng ở hoá đơn bán sỉ.</summary>
    WholesaleGift = 17,

    /// <summary>18 — Quà tặng ở đơn hàng.</summary>
    OrderGift = 18,

    /// <summary>19 — Combo.</summary>
    Combo = 19
}

/// <summary>
/// 🆕 **`WP-04`** — **LOẠI ĐỐI TƯỢNG** của phiếu kho (Nhanh: `contactType`).
/// 📌 Số trùng Nhanh (`1 KH · 2 NCC · 3 Dịch vụ trả góp · 4 Nhân viên · 20 Khác · 21 Sàn TMĐT`) vì cùng lý do
/// với <see cref="StockDocumentMode"/>: số là **hợp đồng dữ liệu**, không phải thứ tự hiển thị.
/// 🔒 **CHỈ THÊM Ở CUỐI DẢI** (D-17).
/// </summary>
public enum StockCounterpartyType
{
    /// <summary>1 — Khách hàng.</summary>
    Customer = 1,

    /// <summary>2 — Nhà cung cấp.</summary>
    Supplier = 2,

    /// <summary>3 — Dịch vụ trả góp.</summary>
    InstallmentService = 3,

    /// <summary>4 — Nhân viên.</summary>
    Staff = 4,

    /// <summary>20 — Khác.</summary>
    Other = 20,

    /// <summary>21 — Sàn TMĐT.</summary>
    Marketplace = 21
}

