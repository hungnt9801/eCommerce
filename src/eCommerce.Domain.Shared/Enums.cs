namespace eCommerce;

/// <summary>
/// Kênh bán. Giá trị lưu dạng chuỗi trong DB (xem <c>Channel.Code</c>), enum này chỉ để
/// phân loại hành vi đồng bộ.
/// </summary>
public enum ChannelType
{
    /// <summary>Website bán hàng của chính mình (nguồn sự thật về tồn kho).</summary>
    OwnStorefront = 0,

    /// <summary>Sàn TMĐT (Shopee, Lazada, TikTok Shop...).</summary>
    Marketplace = 1,

    /// <summary>Kênh bán offline / POS.</summary>
    Offline = 2,

    /// <summary>Kênh khác (Zalo, Facebook...).</summary>
    Other = 99
}

/// <summary>
/// Loại biến động kho. <c>StockLedgerEntry</c> là append-only: mọi thay đổi tồn kho
/// đều phải sinh một bản ghi ở đây, không được UPDATE tồn kho trực tiếp.
/// </summary>
public enum StockMovementType
{
    /// <summary>Nhập kho.</summary>
    Receipt = 0,

    /// <summary>Giữ chỗ cho đơn (chưa xuất kho).</summary>
    Reservation = 1,

    /// <summary>Nhả giữ chỗ (đơn hủy / hết TTL).</summary>
    ReservationRelease = 2,

    /// <summary>Xuất kho bán hàng.</summary>
    Sale = 3,

    /// <summary>Khách trả hàng, nhập lại kho.</summary>
    CustomerReturn = 4,

    /// <summary>Điều chỉnh kiểm kê.</summary>
    Adjustment = 5,

    /// <summary>Chuyển kho.</summary>
    Transfer = 6,

    // 🚨🚨 **`INV-001` ADR-04 — CHỈ ĐƯỢC THÊM Ở CUỐI, KHÔNG BAO GIỜ ĐỔI SỐ CỦA GIÁ TRỊ CŨ.**
    // `StockMovementType` được lưu **dạng số** trong `InvStockLedger.MovementType` ⇒ đổi số của giá trị cũ
    // sẽ **diễn giải lại toàn bộ lịch sử kho**: một bút toán "Nhập kho" thành "Giữ chỗ" — **sai sổ cái mà
    // không có lỗi nào**. Test `StockMovementTypeTests` khoá bất biến này.
    // 📌 Trước `INV-001`, enum **THIẾU** giá trị cho **kiểm kê** và **chuyển kho đến/đi** ⇒ không ghi được
    // nghiệp vụ thật (phải "mượn" `Adjustment`/`Transfer` ⇒ **mất khả năng truy vết**).

    /// <summary>Kiểm kê **thừa** (đếm được nhiều hơn sổ) — `INV-001`.</summary>
    StocktakeGain = 7,

    /// <summary>Kiểm kê **thiếu** (đếm được ít hơn sổ) — `INV-001`.</summary>
    StocktakeLoss = 8,

    /// <summary>Chuyển kho — phần **ĐI** (kho nguồn) — `INV-001`.</summary>
    TransferOut = 9,

    /// <summary>Chuyển kho — phần **ĐẾN** (kho đích) — `INV-001`.</summary>
    TransferIn = 10
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Completed = 4,
    Cancelled = 5,
    Returned = 6
}

public enum PaymentStatus
{
    Unpaid = 0,
    Pending = 1,
    Paid = 2,
    PartiallyRefunded = 3,
    Refunded = 4,
    Failed = 5
}

/// <summary>
/// **TRỤC 1 — THỰC HIỆN** của đơn (`SAL-003` D1/D3): *"hàng đang ở đâu trên đường tới khách"*.
///
/// 🚨 **VÌ SAO KHÔNG DÙNG LẠI `OrderStatus`:** enum cũ trộn **3 nghĩa** — `Shipped` (thực hiện),
/// `Cancelled` (kết thúc), `Returned` (kết thúc) — nên không trả lời được câu hỏi vận hành
/// *"đơn nào đã giao mà chưa thu tiền (COD)?"*. `OrderStatus` nay chỉ còn là **trạng thái SUY RA**
/// (`Order.Status`) để hiển thị/tương thích ngược.
/// ⚠️ **CHỈ THÊM GIÁ TRỊ Ở CUỐI, KHÔNG ĐỔI SỐ** — cột lưu dạng số.
/// </summary>
public enum FulfillmentStatus
{
    /// <summary>Mới tạo, chưa xác nhận.</summary>
    Pending = 0,

    /// <summary>Đã xác nhận (đã chốt giữ chỗ/xuất kho).</summary>
    Confirmed = 1,

    /// <summary>Đang đóng gói.</summary>
    Packing = 2,

    /// <summary>Chờ hãng vận chuyển lấy hàng.</summary>
    AwaitingPickup = 3,

    /// <summary>Đang giao.</summary>
    OutForDelivery = 4,

    /// <summary>Đã giao thành công.</summary>
    Delivered = 5,

    /// <summary>Giao thất bại (sẽ giao lại hoặc chuyển hoàn).</summary>
    DeliveryFailed = 6,

    /// <summary>Đang trên đường hoàn về kho.</summary>
    Returning = 7,

    /// <summary>Đã về kho — **hấp thụ**.</summary>
    Returned = 8
}

/// <summary>
/// **TRỤC 3 — ĐỐI SOÁT** (`SAL-003`): tiền từ sàn/COD về **sau** khi giao và **có thể lệch**.
/// `NotRequired` dùng cho đơn không có tiền cần đối soát (0 đồng, huỷ trước khi thu).
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>Không cần đối soát — **hấp thụ**.</summary>
    NotRequired = 0,

    /// <summary>Chờ đối soát kỳ này.</summary>
    AwaitingReconcile = 1,

    /// <summary>Đã khớp — **hấp thụ**.</summary>
    Matched = 2,

    /// <summary>Có lệch, chưa xử lý.</summary>
    Mismatched = 3,

    /// <summary>Đã xử lý lệch — **hấp thụ**.</summary>
    Resolved = 4
}

/// <summary>
/// **TRỤC 4 — KẾT THÚC** (`SAL-003`): trả hàng và huỷ. `Cancelled` là trạng thái **hấp thụ**;
/// muốn bán lại phải **tạo đơn mới** (D10).
/// 📌 `ReturnStatus.Cancelled` là giá trị của **trục kết thúc**, không phải trục thực hiện — nhờ vậy
/// một đơn vừa "đã giao" vừa "đã huỷ" không thể tồn tại (mâu thuẫn bị chặn ngay ở dữ liệu).
/// </summary>
public enum ReturnStatus
{
    /// <summary>Chưa có yêu cầu trả.</summary>
    Open = 0,

    /// <summary>Khách/sàn yêu cầu trả.</summary>
    ReturnRequested = 1,

    /// <summary>Đã duyệt trả.</summary>
    ReturnApproved = 2,

    /// <summary>Từ chối trả — **hấp thụ**.</summary>
    ReturnRejected = 3,

    /// <summary>Đã trả xong — **hấp thụ**.</summary>
    Returned = 4,

    /// <summary>Đã huỷ đơn — **hấp thụ**.</summary>
    Cancelled = 5
}

/// <summary>Bốn trục trạng thái của đơn (`SAL-003` BR-003). Cấm thêm trục thứ 5 khi chưa sửa tài liệu.</summary>
public enum OrderAxis
{
    /// <summary>Trục 1 — thực hiện.</summary>
    Fulfillment = 1,

    /// <summary>Trục 2 — tài chính.</summary>
    Payment = 2,

    /// <summary>Trục 3 — đối soát.</summary>
    Reconciliation = 3,

    /// <summary>Trục 4 — kết thúc (trả/huỷ).</summary>
    Closure = 4
}

/// <summary>
/// **AI** đổi trạng thái (`SAL-003` BR-006). Ghi vào lịch sử dưới dạng chuỗi để đọc log/SQL dễ.
/// </summary>
public enum StatusActorType
{
    /// <summary>Khách hàng (tự huỷ/yêu cầu trả).</summary>
    Customer = 1,

    /// <summary>Nhân viên vận hành (qua UI/API có xác thực).</summary>
    Staff = 2,

    /// <summary>Job hệ thống (quá hạn, backfill, quét).</summary>
    SystemJob = 3,

    /// <summary>Webhook sàn TMĐT.</summary>
    MarketplaceWebhook = 4,

    /// <summary>IPN/`querydr` của cổng thanh toán.</summary>
    PaymentGateway = 5,

    /// <summary>Job đối soát.</summary>
    Reconciliation = 6
}

/// <summary>Trạng thái đồng bộ của một listing trên sàn.</summary>
public enum ChannelListingStatus
{
    Draft = 0,
    PendingSync = 1,
    Synced = 2,
    Error = 3,
    Delisted = 4
}

/// <summary>
/// **Loại sử dụng của thuộc tính** (`CAT-002` **D4**/BR-008) — tách **2 loại tường minh**, **không** dùng cờ `bool`.
///
/// ⚠️ VÌ SAO TÁCH HẲN 2 GIÁ TRỊ THAY VÌ MỘT CỜ `IsVariant`: hai loại có **luật khác hẳn nhau** —
/// `Variant` **không được xoá** khi đã có biến thể dùng nó (`Catalog:0023`), còn `Specification` xoá được tự do.
/// Nếu chỉ có một cờ `bool` thì guard rất dễ viết **sai nhánh** và **không có lỗi biên dịch** nào chỉ ra.
///
/// 📌 Đặt ở `Domain.Shared` (không phải trong entity) vì **DTO ở `Application.Contracts` cũng cần** —
/// tầng đó **không** được tham chiếu `Domain` (layering). Đây đúng quy ước: mọi enum nằm ở `Enums.cs`.
/// </summary>
public enum CatalogAttributeUsage
{
    /// <summary>Chỉ **mô tả** (chất liệu, bảo hành…). **Không** tham gia sinh tổ hợp.</summary>
    Specification = 0,

    /// <summary>Sinh **biến thể** (màu, size…). Là trục của tích Descartes (BR-009).</summary>
    Variant = 1
}

/// <summary>
/// Vòng đời HIỂN THỊ của sản phẩm (`CAT-001` **D5** — state machine ở tầng DOMAIN).
///
/// 🚨 VÌ SAO PHẢI CÓ ENUM + METHOD CÓ GUARD (thay vì `bool IsPublished` có `set` công khai):
/// `IsPublished` là `bool` công khai ⇒ **không có chỗ nào** để chặn chuyển trạng thái sai
/// (ví dụ publish một sản phẩm **không có biến thể** nào — bán không được, mà vẫn hiện trên storefront).
/// Quy ước dự án: **máy trạng thái PHẢI NÉM LỖI**, không im lặng bỏ qua (bài học `SetPaidStatus()` của eShop).
/// </summary>
public enum ProductStatus
{
    /// <summary>Nháp — chưa từng hiện ra ngoài.</summary>
    Draft = 0,

    /// <summary>Đang bán (hiện trên storefront/kênh).</summary>
    Published = 1,

    /// <summary>Đã ngừng bán nhưng **giữ nguyên** dữ liệu (có thể publish lại).</summary>
    Unpublished = 2,

    /// <summary>Lưu trữ — kết thúc vòng đời; **không** publish lại.</summary>
    Archived = 3
}

// ══════════════════════════════════════════════════════════════════════════════════════════════════
// 🆕 `REC-001` — ĐỐI SOÁT THANH TOÁN (`PaymentReconciliation*`, 27/10/2026)
// 📌 Đặt ở `Domain.Shared` vì entity (module Reconciliation), DTO (Contracts) và worker đều dùng;
//    Contracts **không** được tham chiếu Domain.
// ══════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Vòng đời **một lượt** đối soát thanh toán (`REC-001` §3.1).</summary>
public enum PaymentReconciliationRunStatus
{
    DangChay = 0,
    HoanTat = 1,
    HoanTatCoLech = 2,
    ThatBai = 3
}

/// <summary>Ai/kênh kích hoạt lượt chạy (`REC-001` §5.2.1).</summary>
public enum PaymentReconciliationTrigger
{
    Schedule = 0,
    Manual = 1
}

/// <summary>Vòng đời **một dòng** (một giao dịch) trong lượt (`REC-001` §3).</summary>
public enum PaymentReconciliationLineStatus
{
    ChoDoiSoat = 0,
    DangTruyVan = 1,
    Khop = 2,
    Lech = 3,
    LoiNhaCungCap = 4,
    ThieuDuLieu = 5,
    BoQua = 6
}

/// <summary>Danh sách **ĐÓNG** loại sai lệch (`REC-001` §5.4 / BR-12).</summary>
public enum PaymentDiscrepancyKind
{
    MoneyReceivedButNotRecorded = 0,
    RecordedButMoneyNotReceived = 1,
    AmountMismatch = 2,
    ProviderFlagged = 3,
    ProviderTransactionNotFound = 4,
    ProviderTxnIdMismatch = 5,
    RefundNotRecorded = 6,
    ProviderUnreachable = 7,
    MissingProviderCreateDate = 8,
    StalePendingTransaction = 9
}

/// <summary>Mức nghiêm trọng của sai lệch (`REC-001` §5.4).</summary>
public enum PaymentDiscrepancySeverity
{
    Cao = 0,
    TrungBinh = 1,
    Thap = 2
}

/// <summary>Vòng đời xử lý sai lệch (`REC-001` §3.2).</summary>
public enum PaymentDiscrepancyStatus
{
    Moi = 0,
    DaXacNhan = 1,
    DaXuLy = 2,
    BoQua = 3
}