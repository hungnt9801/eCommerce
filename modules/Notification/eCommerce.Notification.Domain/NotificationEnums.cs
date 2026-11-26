namespace eCommerce.Notification;

/// <summary>
/// Kênh gửi — **MỘT** enum dùng chung với `NOT-002` (`D-197` ⑥). CẤM khai enum thứ hai
/// (`NotificationChannelKind`).
/// </summary>
public enum ChannelKind
{
    Email = 0,
    Sms = 1,
    ZaloOa = 2
}

/// <summary>Mục đích template — quyết định khử trùng cửa sổ (cửa sổ chỉ bật cho `Marketing`).</summary>
public enum NotificationPurpose
{
    Transactional = 0,
    Marketing = 1,
    OpsAlert = 2
}

/// <summary>Trạng thái template — chỉ đổi qua hàm của entity, không có setter công khai.</summary>
public enum NotificationTemplateStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>Định dạng thân tin. GĐ3 **chỉ** `Text`; `Html` giữ ở cuối enum để mở sau (`D-17`/`D-199`).</summary>
public enum NotificationBodyFormat
{
    Text = 0,
    Html = 1
}

/// <summary>
/// Trạng thái mục hàng đợi (§3 SRS). Lưu DB dạng `int` — **CHỈ THÊM Ở CUỐI**.
/// `DaBanGiao`/`DaHuy`/`HetHan`/`Loi` là trạng thái **hấp thụ**.
/// </summary>
public enum NotificationQueueStatus
{
    Moi = 0,
    ChoDuyet = 1,
    ChoGui = 2,
    DangBanGiao = 3,
    DaBanGiao = 4,
    DaHuy = 5,
    HetHan = 6,
    Loi = 7
}

/// <summary>Kiểu placeholder. `Money` nhận **chuỗi đã định dạng** qua `INotificationMoneyFormatter` (ADR-07).</summary>
public enum NotificationPlaceholderKind
{
    Text = 0,
    Money = 1,
    DateTime = 2,
    Number = 3,
    Url = 4
}

/// <summary>Trạng thái lô gửi — hấp thụ `DaBanGiao`/`DaHuy`.</summary>
public enum NotificationBatchStatus
{
    ChoDuyet = 0,
    DaDuyet = 1,
    DaHuy = 2,
    DaBanGiao = 3
}

/// <summary>Loại sự kiện trong sổ append-only `NotificationQueueEvents` — **CHỈ THÊM Ở CUỐI**.</summary>
public enum NotificationQueueEventType
{
    Enqueued = 0,
    ReadyToDispatch = 1,
    Dispatching = 2,
    HandedOff = 3,
    LeaseExpired = 4,
    Failed = 5,
    Expired = 6,
    Cancelled = 7,
    Suppressed = 8,
    Replayed = 9,
    BatchApproved = 10,
    QueuedForApproval = 11,
    RetryScheduled = 12
}

/// <summary>Mức ưu tiên — khớp CHECK `Priority IN (0,1,2,3)`.</summary>
public static class NotificationPriorities
{
    public const int Low = 0;
    public const int Normal = 1;
    public const int High = 2;
    public const int Critical = 3;
}
