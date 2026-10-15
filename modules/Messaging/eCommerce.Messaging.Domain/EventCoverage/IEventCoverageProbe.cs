using System.Reflection;

namespace eCommerce.Messaging.EventCoverage;

/// <summary>
/// Trả lời câu hỏi *"sự kiện này có NGƯỜI NHẬN chưa?"* **ở thời điểm chạy** — dùng CHUNG một nguồn
/// sự thật với cảnh báo lúc khởi động (xem <see cref="EventCoverageScan"/>).
///
/// 🚨 VÌ SAO CẦN Ở THỜI ĐIỂM CHẠY (không chỉ lúc khởi động):
/// Lỗi #22 — `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn** kéo từ sàn nhưng
/// **không ai nhận**; đồng bộ vẫn báo thành công và **tiến con trỏ đồng bộ** ⇒ sàn không gửi lại
/// ⇒ **đơn mất vĩnh viễn, không có lỗi nào được ghi**. Cảnh báo lúc khởi động chỉ *nhìn thấy* lỗi;
/// muốn **đơn không mất** thì bên publish phải **tự hỏi** câu này rồi **fail-closed**.
///
/// ⚠️ Kết quả được **cache** sau lần quét đầu (assembly không được nạp thêm sau khi ứng dụng khởi động).
/// </summary>
public interface IEventCoverageProbe
{
    /// <summary>Kết quả quét đầy đủ (kèm số assembly đã quét — để phạm vi kiểm tra nhìn thấy được).</summary>
    KetQuaQuetPhuSongSuKien KetQua { get; }

    /// <summary>
    /// <c>true</c> nếu có ít nhất một kiểu đã nạp triển khai `IDistributedEventHandler&lt;T&gt;` /
    /// `ILocalEventHandler&lt;T&gt;` / `IEventHandler&lt;T&gt;` cho sự kiện này.
    /// </summary>
    bool CoNguoiNhan(Type kieuSuKien);
}
