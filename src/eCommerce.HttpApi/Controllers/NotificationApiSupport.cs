using eCommerce.Notification;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// Tham số danh sách template (`GET /api/v1/notifications/templates`) — đúng những gì màn
/// "Danh sách template" cần: phân trang + sắp xếp. **Không** có bộ lọc trạng thái: SRS §10 buộc
/// bản ghi `Archived` **vẫn hiển thị** (tin cũ còn ghim version).
/// </summary>
public sealed class NotificationTemplateListQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = NotificationApiPaging.PageSizeMacDinh;

    /// <summary>Ví dụ `Code` — app service hiện sắp theo `Code` cố định.</summary>
    public string? Sort { get; set; }
}

/// <summary>
/// Tham số danh sách hàng đợi (`GET /api/v1/notifications/queue`) — lọc theo trạng thái/kênh/khoảng
/// thời gian như SRS §10 (cột "Hàng đợi").
/// </summary>
public sealed class NotificationQueueListQuery
{
    public NotificationQueueStatus? Status { get; set; }

    public ChannelKind? ChannelKind { get; set; }

    /// <summary>Lọc theo `ScheduledAtUtc >= TuNgayUtc` (UTC).</summary>
    public DateTime? TuNgayUtc { get; set; }

    /// <summary>Lọc theo `ScheduledAtUtc <= DenNgayUtc` (UTC).</summary>
    public DateTime? DenNgayUtc { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = NotificationApiPaging.PageSizeMacDinh;

    /// <summary>Ví dụ `ScheduledAtUtc desc` — app service hiện sắp giảm dần theo `ScheduledAtUtc`.</summary>
    public string? Sort { get; set; }
}

/// <summary>Chuẩn hoá phân trang của lớp `/api/v1` — cùng mặc định/trần với `ProductsController`.</summary>
internal static class NotificationApiPaging
{
    internal const int PageSizeMacDinh = 50;
    internal const int PageSizeToiDa = 200;

    internal static (int Page, int PageSize, int SkipCount, int MaxResultCount) ChuanHoa(int page, int pageSize)
    {
        var trang = page < 1 ? 1 : page;
        var coTrang = pageSize is < 1 or > PageSizeToiDa ? PageSizeMacDinh : pageSize;
        return (trang, coTrang, (trang - 1) * coTrang, coTrang);
    }
}
