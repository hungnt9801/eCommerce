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
