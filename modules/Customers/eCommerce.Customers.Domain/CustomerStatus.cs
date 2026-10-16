namespace eCommerce.Customers;

/// <summary>
/// **Trạng thái vòng đời khách hàng** (`CUS-001` §3) — **state machine NÉM LỖI** (`ADR-04`).
///
/// 🚨 **VÌ SAO PHẢI LÀ MÁY TRẠNG THÁI CHỨ KHÔNG PHẢI `enum` CÓ `set` CÔNG KHAI:**
/// bài học thật **#3** của tài liệu: *"guard trạng thái no-op im lặng làm sự kiện rơi không để lại dấu vết"*
/// (từ `SetPaidStatus()` của eShop, ghi ở `XC-002` §0 và quy ước #11). Nếu `Customer.Status` có `set`
/// công khai thì **mọi** đường ghi đều có thể đặt trạng thái tuỳ ý, và một lần đặt sai (hoặc gọi lặp)
/// **không** sinh lỗi nào — chỉ để lại dữ liệu sai mà **không ai biết lúc nào**.
/// </summary>
public enum CustomerStatus
{
    /// <summary>
    /// Mới ghi nhận, **chưa** kích hoạt (ví dụ khách đến từ webhook sàn, chưa xác nhận liên hệ).
    /// 📌 **KHÔNG chặn bán**: khách vãng lai là hạng nhất (`ADR-03`) — chặn bán ở đây sẽ làm hỏng
    /// luồng đặt hàng của khách chưa từng tương tác.
    /// </summary>
    Inactive = 0,

    /// <summary>Đang hoạt động.</summary>
    Active = 1,

    /// <summary>
    /// 🚫 **BỊ CHẶN** — khách chây nợ / gian lận COD (`ADR-04`).
    /// 🔒 **Trạng thái HẤP THỤ CÓ ĐIỀU KIỆN**: chỉ ra được bằng `Customer.MoChan` (cần **lý do**),
    /// **không** bằng `Customer.KichHoat` — nếu ra được bằng `KichHoat` thì lệnh chặn **tự mất hiệu lực**
    /// sau một lần gọi "kích hoạt" vô ý (ví dụ retry của một job).
    /// </summary>
    Blocked = 2
}
