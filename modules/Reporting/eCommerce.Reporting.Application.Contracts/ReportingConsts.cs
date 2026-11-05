namespace eCommerce.Reporting;

/// <summary>Hằng dùng chung của miền báo cáo.</summary>
public static class ReportingConsts
{
    /// <summary>
    /// **ĐỊNH NGHĨA TIỀN của báo cáo** — trả về trong mọi DTO doanh thu và **in thẳng lên UI**.
    ///
    /// 🔑 VÌ SAO LÀ HẰNG Ở SERVER (không viết lại ở FE): báo cáo có ba nơi hiển thị (API, Dashboard, màn
    /// doanh thu). Nếu mỗi nơi tự viết một câu, sớm muộn chúng nói **ba định nghĩa khác nhau** — và người
    /// dùng sẽ tin con số nào tuỳ màn hình họ mở (`D-45` đã trả giá đúng kiểu này: hai công thức, lệch 3.000).
    /// </summary>
    public const string DinhNghiaDoanhThu =
        "Doanh thu = Σ thành tiền dòng (CHƯA gồm VAT) − chiết khấu chứng từ. " +
        "CHƯA trừ hàng trả lại; hoá đơn đã huỷ và đơn đã huỷ KHÔNG được tính.";
}
