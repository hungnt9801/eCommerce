namespace eCommerce.PaymentHub.Payments;

/// <summary>
/// 🚨 **LUẬT GHI NHẬN KẾT QUẢ HOÀN TIỀN** (`S5`) — hàm **THUẦN**, không I/O, không DB.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI TÁCH THÀNH HÀM THUẦN (không viết thẳng `if/else` ở app service):**
/// cùng một quyết định này được dùng ở **hai** chỗ — `PaymentAppService.HoanTienAsync` (người bấm hoàn) và
/// đường xác nhận bằng `querydr` (đối soát) — và nó quyết định **TIỀN CÓ ĐƯỢC GHI VÀO SỔ HAY KHÔNG**.
/// Nếu mỗi chỗ tự viết lại chuỗi `if`, thứ tự kiểm sẽ **lệch** khi một bên thêm nhánh, và lệch ở đây nghĩa là
/// **sổ sách ghi "đã hoàn" trong khi tiền chưa ra** (hoặc ngược lại: tiền ra mà sổ không biết).
/// 📌 Đây đúng lý do `LuatDoiSoatThanhToan.Quyet` tồn tại (`REC-001`) và `LuatCauNoiThanhToan` (`S4`).
///
/// 🔒 **THỨ TỰ KIỂM LÀ MỘT PHẦN CỦA LUẬT — đọc kỹ trước khi đổi:**
/// <list type="number">
///   <item>`DaHoanThat` **TRƯỚC TIÊN** — đây là sự thật duy nhất nói **tiền đã ra**. Nếu cổng vừa báo
///     `DaHoanThat = true` vừa kèm `LoiHaTang` (dữ liệu mâu thuẫn, hiếm nhưng có thể xảy ra khi provider khác
///     được cài), thì *"tiền đã ra"* **thắng**: bỏ qua nó là **giấu một lần chi tiền thật**.</item>
///   <item>`LoiHaTang` **trước** `DaNhanYeuCau` — lỗi hạ tầng nghĩa là *"KHÔNG BIẾT"*, khác hẳn *"đã nhận"*.
///     📌 Nếu kiểm ngược lại, một timeout có `DaNhanYeuCau = false` sẽ rơi vào nhánh `TuChoi` ⇒ người vận hành
///     đọc *"cổng từ chối ⇒ gửi lại được"* ⇒ **hoàn HAI LẦN** (mất tiền thật).</item>
///   <item>`DaNhanYeuCau` ⇒ `ChoXacNhan`; còn lại ⇒ `TuChoi` (fail-closed về phía **không ghi sổ**).</item>
/// </list>
/// </summary>
public static class LuatGhiNhanHoanTien
{
    /// <summary>Quyết định ghi nhận cho một lần gọi cổng hoàn tiền.</summary>
    public enum QuyetDinh
    {
        /// <summary>🚨 Tiền **ĐÃ RA** ⇒ **PHẢI** `PaymentTransaction.RegisterRefund` + ghi dòng `DaHoan`.</summary>
        GhiDaHoan = 0,

        /// <summary>Cổng **đã nhận**, tiền **chưa** ra ⇒ ghi dòng `ChoXacNhan`, **KHÔNG** đụng `RefundedAmount`.</summary>
        ChoXacNhan = 1,

        /// <summary>Cổng **từ chối** ⇒ ghi dòng `TuChoi`, **KHÔNG** đụng `RefundedAmount` (gửi lại được).</summary>
        TuChoi = 2,

        /// <summary>Lỗi **hạ tầng** ⇒ ghi dòng `KhongXacDinh`, **KHÔNG** đụng `RefundedAmount`, **CẤM** retry mù.</summary>
        KhongXacDinh = 3
    }

    /// <summary>Quyết định từ kết quả của cổng (`RefundResult`).</summary>
    public static QuyetDinh Quyet(RefundResult? ketQua)
    {
        // 🔒 Không có kết quả (provider trả `null` — lỗi lập trình) ⇒ **KHÔNG BIẾT**, tuyệt đối không đoán
        // "từ chối": đoán sai theo hướng đó dẫn tới *"gửi lại được"* ⇒ có thể hoàn hai lần.
        if (ketQua is null)
        {
            return QuyetDinh.KhongXacDinh;
        }

        if (ketQua.DaHoanThat)
        {
            return QuyetDinh.GhiDaHoan;
        }

        if (!string.IsNullOrWhiteSpace(ketQua.LoiHaTang))
        {
            return QuyetDinh.KhongXacDinh;
        }

        return ketQua.DaNhanYeuCau ? QuyetDinh.ChoXacNhan : QuyetDinh.TuChoi;
    }

    /// <summary>Quyết định này có **yêu cầu ghi sổ `RefundedAmount`** hay không.</summary>
    /// 🔒 Chỉ **một** giá trị trả `true`. 📌 Viết thành hàm (không để chỗ gọi tự so `==`) để khi thêm quyết định
    /// mới, chỗ nào quên là **lỗi biên dịch** chứ không phải một nhánh **im lặng** bị bỏ qua.
    public static bool PhaiGhiSo(QuyetDinh quyetDinh) => quyetDinh == QuyetDinh.GhiDaHoan;

    /// <summary>Ánh xạ quyết định ⇒ trạng thái lưu trên dòng hoàn tiền.</summary>
    public static TrangThaiDongHoanTien SangTrangThaiDong(QuyetDinh quyetDinh) => quyetDinh switch
    {
        QuyetDinh.GhiDaHoan => TrangThaiDongHoanTien.DaHoan,
        QuyetDinh.ChoXacNhan => TrangThaiDongHoanTien.ChoXacNhan,
        QuyetDinh.TuChoi => TrangThaiDongHoanTien.TuChoi,
        _ => TrangThaiDongHoanTien.KhongXacDinh
    };
}
