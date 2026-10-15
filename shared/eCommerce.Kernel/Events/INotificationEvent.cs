namespace eCommerce.Kernel.Events;

/// <summary>
/// Đánh dấu một hợp đồng sự kiện (`*Eto`) là **sự kiện THÔNG BÁO** — **mất nó KHÔNG mất dữ liệu**.
///
/// 🚨 VÌ SAO CẦN PHÂN BIỆT (bài học thật, 15/10/2026):
/// `EventCoverageChecker` báo **lỗi** khi một `*Eto` được publish mà **không có người nhận**, kèm thông điệp
/// *"DỮ LIỆU SẼ MẤT IM LẶNG"*. Thông điệp đó **đúng** với sự kiện **mang dữ liệu** — ví dụ
/// `ChannelOrderPulledEto`: đơn hàng **chỉ** tồn tại trong sự kiện, không ai nhận ⇒ **đơn mất vĩnh viễn**
/// (lỗi #22). Nhưng nó **SAI** với sự kiện **thông báo**: dữ liệu đã nằm trong DB
/// (ví dụ `ChannelShopTokenRefreshedEto` — token mới đã được ghi vào `ChnChannelShops`), mất sự kiện chỉ
/// khiến việc *thông báo* không xảy ra.
///
/// Nếu hai loại bị gộp làm một thì **cảnh báo sẽ có dương tính giả** ⇒ người đọc bắt đầu **bỏ qua** cảnh
/// báo ⇒ và **lỗi thật** (như lỗi #22) sẽ lọt qua. Đó chính là thứ cần tránh.
///
/// ✅ HỆ QUẢ KHI TRIỂN KHAI MARKER NÀY:
///   • `*Eto` **mang dữ liệu** (KHÔNG có marker) + không người nhận ⇒ log **Error** (như cũ — **giữ nguyên**).
///   • `*Eto` **thông báo** (CÓ marker) + không người nhận ⇒ log **Warning** (không còn là lỗi).
///
/// ⚠️ **CÁCH NHẬN BIẾT — THEO TÊN INTERFACE**, do lõi `EventCoverageChecker` cố ý **không phụ thuộc ABP**:
/// nó so khớp tên `"INotificationEvent"`. ⇒ **KHÔNG đổi tên interface này.** Nếu đổi tên, phân loại sẽ
/// **suy giảm** về "mang dữ liệu" (tức là **ồn ào hơn**, không phải im lặng hơn) — đây là **hướng suy giảm
/// an toàn** đã chọn có chủ ý.
///
/// 📌 **CÂU HỎI TỰ VẤN TRƯỚC KHI GẮN MARKER (quan trọng nhất):**
/// *"Nếu sự kiện này biến mất hoàn toàn, tôi có mất thông tin mà DB **không** có không?"*
///   • **CÓ** ⇒ **ĐỪNG** gắn marker (đây là sự kiện mang dữ liệu ⇒ phải fail-closed).
///   • **KHÔNG** ⇒ gắn marker được.
/// </summary>
public interface INotificationEvent
{
}
