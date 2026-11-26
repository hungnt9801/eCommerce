using System.Text.RegularExpressions;

namespace eCommerce.Notification;

/// <summary>
/// Che PII/secret trong nội dung render + lý do — **HÀM THUẦN**, **một cửa** (`BR-010`, `D-269`).
/// Không DB, không I/O, không phụ thuộc thời gian ⇒ chạy 2 lần cho **cùng** kết quả (idempotent).
///
/// Thứ tự luật (`D-269` ①(2)): (i) email → `kýTựĐầu***@domain`; (ii) SĐT **di động** VN
/// (`0[35789]xxxxxxxx` / `+84[35789]xxxxxxxx`, cho dấu cách/`.`/`-` giữa nhóm) → giữ 2 đầu + 3 cuối;
/// (iii) 13–19 chữ số liền (PAN) → `***` + 4 cuối; (iv) tham số query `token`/`sig`/`signature`/`code`/
/// `key`/`password`/`otp` → `***`; (v) **KHÔNG** đụng dãy 6–8 chữ số (tiền/ngày/`MaDon`); (vi) `null`/rỗng
/// ⇒ `string.Empty`.
/// </summary>
public static class NotificationMasker
{
    // Regex tuyến tính đơn giản + timeout: không dùng lookaround lồng/backreference ⇒ không có
    // catastrophic backtracking (`D-269` ràng buộc 0 gói NuGet).
    private static readonly TimeSpan ThoiGianToiDa = TimeSpan.FromMilliseconds(250);

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.CultureInvariant,
        ThoiGianToiDa);

    private static readonly Regex DiDongRegex = new(
        @"(?<!\d)(?:\+84|0)[\s.\-]?[35789](?:[\s.\-]?\d){8}(?!\d)",
        RegexOptions.CultureInvariant,
        ThoiGianToiDa);

    private static readonly Regex PanRegex = new(
        @"(?<!\d)\d{13,19}(?!\d)",
        RegexOptions.CultureInvariant,
        ThoiGianToiDa);

    private static readonly Regex ThamSoBiMatRegex = new(
        @"(?i)\b(token|sig|signature|code|key|password|otp)=([^&\s""'<>]*)",
        RegexOptions.CultureInvariant,
        ThoiGianToiDa);

    /// <summary>Che người nhận để hiển thị — **CẤM** hiển thị đầy đủ (`D-191`).</summary>
    public static string CheNguoiNhan(string? recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return string.Empty;
        }

        var value = recipient.Trim();
        var at = value.IndexOf('@');
        if (at > 0)
        {
            // email: giữ ký tự đầu + domain.
            var ten = value[..at];
            var domain = value[at..];
            var dau = ten.Length > 0 ? ten[..1] : "*";
            return $"{dau}{new string('*', Math.Min(4, Math.Max(1, ten.Length - 1)))}{domain}";
        }

        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        // điện thoại: giữ 2 đầu + 3 cuối.
        return $"{value[..2]}{new string('*', Math.Min(6, value.Length - 5))}{value[^3..]}";
    }

    /// <summary>Che secret/PII lọt vào nội dung render (OTP/token/email/SĐT/PAN) — một cửa, fail-closed.</summary>
    public static string CheBiMat(string? noiDung)
    {
        if (string.IsNullOrEmpty(noiDung))
        {
            return string.Empty;
        }

        var ketQua = EmailRegex.Replace(noiDung, m => CheEmail(m.Value));
        ketQua = DiDongRegex.Replace(ketQua, m => CheDiDong(m.Value));
        ketQua = PanRegex.Replace(ketQua, m => "***" + m.Value[^4..]);
        return ThamSoBiMatRegex.Replace(ketQua, m => m.Groups[1].Value + "=***");
    }

    /// <summary>Email: giữ **ký tự đầu** của phần tên + `***` + `@domain` (i).</summary>
    private static string CheEmail(string email)
    {
        var at = email.IndexOf('@');
        // Regex bảo đảm `at > 0`; nhánh dưới chỉ để phòng thủ (không thể xảy ra).
        return at <= 0 ? "***" : email[..1] + "***" + email[at..];
    }

    /// <summary>SĐT di động: giữ 2 đầu (hoặc `+84`) + `***` + 3 cuối (ii).</summary>
    private static string CheDiDong(string so)
    {
        if (so.StartsWith("+84", StringComparison.Ordinal))
        {
            return "+84***" + so[^3..];
        }

        // "0 912 345 678" ⇒ 2 **chữ số** đầu vẫn là "09" (không lấy dấu phân cách).
        var haiDau = string.Concat(so.Where(char.IsDigit).Take(2));
        return haiDau + "***" + so[^3..];
    }
}
