namespace eCommerce.Reporting;

/// <summary>
/// **CẤU HÌNH CỦA MIỀN BÁO CÁO** (`WP-08`) — đọc từ section `Reporting` trong `appsettings.json`.
/// 📌 Mọi giá trị có **mặc định đúng** để chạy được mà không cần cấu hình; cấu hình chỉ để đổi hành vi
/// (múi giờ, nhịp job, ngưỡng "sắp hết").
/// </summary>
public class ReportingOptions
{
    /// <summary>
    /// Giờ lệch của **ngày kinh doanh** so với UTC. Mặc định **7** (Việt Nam — không có DST).
    /// 🚨 Đổi giá trị này sau khi đã có dữ liệu sẽ làm **ngày đã tổng hợp** và **ngày kinh doanh mới** lệch
    /// nhau ⇒ phải chạy lại (backfill) khoảng bị ảnh hưởng.
    /// </summary>
    public int GioLechUtc { get; set; } = NgayKinhDoanh.GioLechMacDinh;

    /// <summary>Nhịp job tổng hợp (phút). Mặc định **15** — đủ tươi cho Dashboard, không tốn kém vì chỉ đọc phần mới.</summary>
    public int NhipJobPhut { get; set; } = 15;

    /// <summary>Ngưỡng "sắp hết" theo **khả dụng** (mặc định 5) — dùng cho Dashboard + báo cáo tồn kho.</summary>
    public decimal NguongSapHet { get; set; } = 5m;

    /// <summary>Số mặt hàng sắp hết trả về tối đa (chống trả cả nghìn dòng làm nặng trang).</summary>
    public int SoMatHangSapHetToiDa { get; set; } = 20;
}
