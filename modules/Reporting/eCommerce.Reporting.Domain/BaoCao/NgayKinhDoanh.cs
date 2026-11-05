using Volo.Abp;

namespace eCommerce.Reporting;

/// <summary>
/// **NGÀY KINH DOANH** — quy đổi giữa mốc UTC (mọi cột `CreationTime` của ABP) và **ngày theo giờ Việt Nam**.
///
/// 🚨 **VÌ SAO PHẢI CÓ (không dùng `CreationTime.Date`):** `CreationTime` là **UTC**; `GMT+7` nghĩa là mọi
/// chứng từ phát sinh từ **17:00 UTC đến 23:59 UTC** thuộc **NGÀY HÔM SAU** ở Việt Nam. Nếu gán theo UTC:
/// <list type="bullet">
///   <item>doanh thu buổi tối (18h–23h giờ VN) bị đẩy sang **hôm trước** ⇒ *"doanh thu hôm nay"* của
///     nhân viên **sai** ở đúng khoảng thời gian họ đang nhìn màn hình;</item>
///   <item>job tổng hợp theo ngày sẽ **cắt đôi** một ngày kinh doanh ⇒ chạy lại buổi tối ghi vào ngày khác
///     ngày mà lượt sáng đã ghi ⇒ báo cáo **nhảy số** khi không có chứng từ mới.</item>
/// </list>
/// 📌 **Hàm THUẦN, không đọc cấu hình** ⇒ test được bằng tay; giờ lệch truyền vào từ
/// `ReportingOptions.GioLechUtc` (mặc định **7**, đúng `Asia/Bangkok`/`Asia/Ho_Chi_Minh`).
/// </summary>
public static class NgayKinhDoanh
{
    /// <summary>`+7` giờ — giờ Việt Nam không có DST nên một hằng số là đủ và **tất định**.</summary>
    public const int GioLechMacDinh = 7;

    /// <summary>Mốc UTC → **ngày kinh doanh** theo giờ lệch.</summary>
    public static DateOnly TuUtc(DateTime utc, int gioLech = GioLechMacDinh)
    {
        KiemGioLech(gioLech);

        // 🚨 `DateTime.Kind` của dữ liệu đọc từ DB là `Unspecified` (Npgsql `timestamptz` → UTC, nhưng
        // `DateTime` mất `Kind`). Chuẩn hoá về UTC **trước** khi cộng giờ lệch, nếu không một giá trị
        // `Local` sẽ bị cộng thêm 7 giờ lần nữa.
        var moc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };

        return DateOnly.FromDateTime(moc.AddHours(gioLech));
    }

    /// <summary>
    /// **Mốc BẮT ĐẦU (UTC)** của một ngày kinh doanh — dùng làm biên dưới khi truy vấn chứng từ
    /// (`CreationTime >= BatDauUtc(ngay)`).
    /// </summary>
    public static DateTime BatDauUtc(DateOnly ngay, int gioLech = GioLechMacDinh)
    {
        KiemGioLech(gioLech);

        return DateTime.SpecifyKind(ngay.ToDateTime(TimeOnly.MinValue).AddHours(-gioLech), DateTimeKind.Utc);
    }

    /// <summary>
    /// **Mốc KẾT THÚC (UTC, KHÔNG bao gồm)** của một ngày kinh doanh — biên trên khi truy vấn
    /// (`CreationTime &lt; KetThucUtc(ngay)`).
    /// 📌 Dùng **nửa khoảng** `[từ, đến)` chứ không phải `BETWEEN`: `BETWEEN` với mốc cuối `23:59:59` làm
    /// **mất** chứng từ phát sinh ở `23:59:59.500` (PostgreSQL lưu tới microsecond) — mất doanh thu mà
    /// không có lỗi nào.
    /// </summary>
    public static DateTime KetThucUtc(DateOnly ngay, int gioLech = GioLechMacDinh)
        => BatDauUtc(ngay.AddDays(1), gioLech);

    private static void KiemGioLech(int gioLech)
    {
        // 📌 Chặn giá trị vô lý: giờ lệch ngoài [-12, 14] nghĩa là cấu hình sai, và một ngày kinh doanh
        // sẽ rơi vào chỗ không ai ngờ — báo cáo vẫn chạy, chỉ sai.
        if (gioLech is < -12 or > 14)
        {
            throw new BusinessException(ReportingErrorCodes.KhoangKhongHopLe)
                .WithData("gioLech", gioLech)
                .WithData("lyDo", "Giờ lệch của ngày kinh doanh phải trong khoảng [-12, 14].");
        }
    }
}
