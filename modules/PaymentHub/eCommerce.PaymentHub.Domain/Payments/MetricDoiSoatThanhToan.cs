using eCommerce.Kernel.Observability;

namespace eCommerce.PaymentHub.Payments;

/// <summary>
/// 📊 **PHÁT METRIC CHO MỘT QUYẾT ĐỊNH ĐỐI SOÁT THANH TOÁN** (`REC-001` mục 7).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **VÌ SAO LÀ MỘT LỚP RIÊNG Ở TẦNG DOMAIN — ba quyết định, mỗi cái đều có lý do:**
///
/// **① VÌ SAO CẦN HÀM DÙNG CHUNG (không chép hai chỗ):** hàm này được gọi từ **CẢ**
/// `PaymentAppService` (`PaymentHub.Application`) **VÀ** `PaymentReconciliationWorker`
/// (`eCommerce.Application`). ⚠️ Nếu **chép** thì hai bản sẽ **lệch** khi ai đó sửa một chỗ —
/// 🔒 và lệch ở **metric** thì **ÂM THẦM**: biểu đồ vẫn vẽ, chỉ **sai số**, và **không có lỗi nào**.
///
/// **② VÌ SAO Ở TẦNG DOMAIN (không phải `PaymentHub.Application`):** `eCommerce.Application` **KHÔNG**
/// tham chiếu `PaymentHub.Application` (và **không nên** — đó là tham chiếu ngược tầng). 🔑 Còn
/// `PaymentHub.Domain` thì **cả hai** đều thấy. 📌 Cùng lý do đã buộc luật
/// <see cref="LuatDoiSoatThanhToan.Quyet"/> phải nằm ở Domain.
///
/// **③ VÌ SAO KHÔNG ĐẶT TRONG `LuatDoiSoatThanhToan`:** lớp đó là **hàm THUẦN** — không I/O, không side
/// effect. 🔒 Nhét metric vào sẽ **phá** đúng tính chất làm cho nó **test được** và **gọi được từ mọi nơi**
/// (kể cả test không có `Meter`). 📌 Tách ra giữ được **cả hai**: luật thuần **và** metric dùng chung.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// </summary>
public static class MetricDoiSoatThanhToan
{
    /// <summary>
    /// Phát **counter** <c>payment_recon_changed_total</c> khi quyết định **ĐỔI** trạng thái giao dịch.
    ///
    /// 🔒 **`KhongDoi` KHÔNG phát counter** — ⚠️ counter chỉ đếm **THAY ĐỔI THẬT**.
    /// 📌 Ca *"cổng không xác nhận được"* là **GAUGE** (`payment_recon_unresolved`) và nó được ghi ở
    /// **cuối lượt**, khi đã biết **TỔNG** số không xác định — không phải ở đây.
    ///
    /// 🔑 **VÌ SAO TÁCH HAI LOẠI ĐO (counter vs gauge):** counter trả lời *"đã **phát hiện và sửa** bao nhiêu
    /// ca"*, gauge trả lời *"còn bao nhiêu ca **chưa ngã ngũ**"*. 📌 Hai câu hỏi **khác nhau**; gộp lại thì
    /// *"KHÔNG KIỂM ĐƯỢC"* và *"đã kiểm và sửa"* trông **giống hệt nhau** — đúng loại nói dối mà dự án đang chống.
    /// </summary>
    /// <param name="metrics">⚠️ **TUỲ CHỌN** (`null` = không phát) — 🔒 nếu bắt buộc thì thiếu đăng ký metric
    /// sẽ **NỔ** ở một chỗ **không liên quan** (và với job nền thì nó **NỔ lúc khởi động** ⇒ job **không bao
    /// giờ chạy** mà lỗi lại trỏ vào chỗ khác).</param>
    /// <param name="quyetDinh">Quyết định từ <see cref="LuatDoiSoatThanhToan.Quyet"/>.</param>
    public static void Ghi(eCommerceMetrics? metrics, LuatDoiSoatThanhToan.QuyetDinh quyetDinh)
    {
        if (metrics is null)
        {
            return;
        }

        switch (quyetDinh)
        {
            case LuatDoiSoatThanhToan.QuyetDinh.GhiDaThuTien:
                // 🔑 `succeeded` = **tiền ĐÃ VÀO mà sổ chưa biết** ⇒ đây là nhãn **đáng lo nhất**
                // (lưới an toàn cho IPN mất). 📌 Tách nhãn để biểu đồ phân biệt được *"phát hiện tiền"*
                // với *"phát hiện thất bại"* — hai việc **rất khác nhau** về vận hành.
                metrics.PaymentReconChanged.Add(1, new KeyValuePair<string, object?>(
                    eCommerceMetricTags.Result, "succeeded"));
                break;

            case LuatDoiSoatThanhToan.QuyetDinh.GhiThatBai:
                metrics.PaymentReconChanged.Add(1, new KeyValuePair<string, object?>(
                    eCommerceMetricTags.Result, "failed"));
                break;

            default:
                // `KhongDoi` — xem chú thích lớp.
                break;
        }
    }
}
