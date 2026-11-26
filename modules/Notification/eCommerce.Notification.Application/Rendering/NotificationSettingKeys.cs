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
