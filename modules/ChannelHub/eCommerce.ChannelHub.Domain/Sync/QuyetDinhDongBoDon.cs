namespace eCommerce.ChannelHub.Sync;

/// <summary>Lý do một lô đơn kéo từ sàn **KHÔNG được bàn giao** cho nghiệp vụ.</summary>
public enum LyDoChanDongBoDon
{
    /// <summary>Không chặn — lô đơn được bàn giao bình thường.</summary>
    KhongChan = 0,

    /// <summary>
    /// 🚨 Chưa có nơi nhận đơn. Đây là **lỗi #22**: publish sự kiện cho không ai ⇒ đơn mất vĩnh viễn.
    /// </summary>
    ChuaCoNoiNhanDon = 1
}

/// <summary>Quyết định cho MỘT lô đơn kéo từ sàn: có được publish sự kiện và có được tiến con trỏ không.</summary>
/// <param name="ChoPhepPhatSuKien">Có được publish <c>ChannelOrderPulledEto</c> cho lô này không.</param>
/// <param name="ChoPhepTienConTro">
/// Có được cập nhật `LastSyncedAtUtc` (con trỏ đồng bộ) không. **Tiến con trỏ khi đơn chưa được bàn giao
/// nghĩa là sàn sẽ KHÔNG BAO GIỜ gửi lại ⇒ mất vĩnh viễn.**
/// </param>
/// <param name="LyDo">Lý do chặn (nếu có) — dùng để ghi log/metric/DTO.</param>
public sealed record QuyetDinhDongBoDon(
    bool ChoPhepPhatSuKien,
    bool ChoPhepTienConTro,
    LyDoChanDongBoDon LyDo)
{
    /// <summary>Lô đơn bị chặn bàn giao.</summary>
    public bool BiChan => LyDo != LyDoChanDongBoDon.KhongChan;
}

/// <summary>
/// **LÕI THUẦN** quyết định số phận một lô đơn kéo từ sàn.
///
/// 🚨 BÀI HỌC (lỗi #22 — mức nghiêm trọng nhất dự án):
/// `ChannelShopSyncer` publish `ChannelOrderPulledEto` cho **từng đơn** nhưng **không ai nhận**, rồi vẫn
/// **tiến con trỏ đồng bộ**. Sàn coi như đã giao xong nên **không gửi lại** ⇒ đơn **mất vĩnh viễn**, và
/// **không có lỗi nào được ghi** (đồng bộ báo thành công, `OrdersPulled = N`, log mức Information).
///
/// Biến thể thứ hai của **cùng lỗi** nằm ở đường *"đồng bộ ngay"* thủ công: nó kéo đơn nhưng
/// **không hề phát sự kiện** mà vẫn tiến con trỏ.
///
/// 🎯 VÌ SAO LÀ LÕI THUẦN: quy tắc *"được tiến con trỏ hay không"* là **quy tắc nghiệp vụ sống còn**,
/// nhưng nằm lẫn trong một lớp đầy phụ thuộc I/O (repository, HTTP, event bus) nên **không test được
/// nếu không dựng cả ABP**. Tách ra đây ⇒ test được **mọi nhánh**, kể cả nhánh không có nơi nhận.
///
/// ⚠️ Không có tham số cấu hình nào để "bỏ qua" quy tắc này. Muốn tiến con trỏ khi chưa có nơi nhận
/// thì phải **sửa chính lớp này** — tức là phải đối diện với quyết định, không thể lỡ tay bật một cờ.
/// </summary>
public static class QuyetDinhDongBoDonPolicy
{
    /// <summary>
    /// Tính quyết định cho một lô đơn.
    /// </summary>
    /// <param name="soDon">Số đơn kéo được trong lô.</param>
    /// <param name="coNoiNhanDon">
    /// Có ít nhất một người nhận `ChannelOrderPulledEto` hay không
    /// (xem <c>IEventCoverageProbe</c> — **một nguồn sự thật** với cảnh báo lúc khởi động).
    /// </param>
    public static QuyetDinhDongBoDon Tinh(int soDon, bool coNoiNhanDon)
    {
        // Lô RỖNG: không có gì để bàn giao ⇒ KHÔNG được coi là "chưa có nơi nhận".
        // Nếu chặn ở đây, con trỏ sẽ kẹt vĩnh viễn chỉ vì chưa có nơi nhận, và mọi lần đồng bộ sau đều
        // quét lại cùng một khoảng rỗng ⇒ log đầy mà không bảo vệ thêm được gì.
        if (soDon <= 0)
        {
            return new QuyetDinhDongBoDon(
                ChoPhepPhatSuKien: false,
                ChoPhepTienConTro: true,
                LyDo: LyDoChanDongBoDon.KhongChan);
        }

        if (coNoiNhanDon)
        {
            return new QuyetDinhDongBoDon(
                ChoPhepPhatSuKien: true,
                ChoPhepTienConTro: true,
                LyDo: LyDoChanDongBoDon.KhongChan);
        }

        // 🚨 FAIL-CLOSED: có đơn nhưng không có nơi nhận ⇒ giữ nguyên con trỏ để sàn gửi lại.
        // Đánh đổi đã được người quyết định chấp nhận: log sẽ đầy và đồng bộ coi như "kẹt",
        // nhưng **đơn KHÔNG mất** — và đây là đánh đổi đúng, vì đơn mất là không thể phục hồi.
        return new QuyetDinhDongBoDon(
            ChoPhepPhatSuKien: false,
            ChoPhepTienConTro: false,
            LyDo: LyDoChanDongBoDon.ChuaCoNoiNhanDon);
    }
}
