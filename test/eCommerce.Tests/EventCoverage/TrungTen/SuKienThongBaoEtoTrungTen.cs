namespace eCommerce.Tests.EventCoverage.Khac;

/// <summary>
/// ⚠️ Cố ý **TRÙNG TÊN** với `eCommerce.Tests.EventCoverage.SuKienThongBaoEto` nhưng ở **namespace khác**
/// và **KHÔNG** gắn marker `INotificationEvent`.
///
/// 🎯 MỤC ĐÍCH: khoá luật *"trùng tên ⇒ chọn hướng AN TOÀN (coi là mang dữ liệu)"* của
/// `EventCoverageChecker` — vì lõi so khớp theo **TÊN KIỂU**, hai kiểu cùng tên bị gộp làm một.
/// Nếu gộp theo hướng "thông báo" thì một sự kiện **mang dữ liệu** có thể bị **giảm mức cảnh báo**
/// xuống Warning ⇒ **im lặng** đúng chỗ nguy hiểm. Đây là ca chống điều đó.
///
/// 📌 Đặt ở file riêng vì C# **không** cho phép namespace dạng file-scoped đi kèm namespace dạng khối
/// trong cùng một file.
/// </summary>
public class SuKienThongBaoEto
{
}
