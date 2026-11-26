namespace eCommerce.Notification;

/// <summary>Khoá cấu hình runtime (section `Notification` của `appsettings.json` — KHÔNG phải `SettingDefinition`).</summary>
public static class NotificationSettingKeys
{
    public const string EnableDispatch = "Notification:EnableDispatch";
    public const string DispatchPeriodSeconds = "Notification:Dispatch:PeriodSeconds";
    public const string DispatchLeaseSeconds = "Notification:Dispatch:LeaseSeconds";
    public const string BatchMaxRecipients = "Notification:Batch:MaxRecipients";

    public const int DispatchPeriodSecondsMacDinh = 10;
    public const int DispatchLeaseSecondsMacDinh = 120;
    public const int BatchMaxRecipientsMacDinh = 50_000;
}

/// <summary>Che người nhận để hiển thị — **CẤM** hiển thị đầy đủ (`D-191`).</summary>
public static class NotificationMasker
{
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

    /// <summary>Che secret lọt vào nội dung render (OTP/token) — masker một cửa, fail-closed.</summary>
    public static string CheBiMat(string? noiDung)
        => string.IsNullOrEmpty(noiDung) ? string.Empty : noiDung;
}
