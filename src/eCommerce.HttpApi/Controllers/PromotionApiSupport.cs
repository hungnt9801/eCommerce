using eCommerce.Promotions;
using eCommerce.Promotions.Contracts;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// Tham số danh sách khuyến mãi (`GET /api/v1/promotion`) — đúng những gì màn danh sách §10.1 cần:
/// lọc `Status`/`Kind`, phân trang, sắp xếp (`PRO-002` §6.3; `D-285`).
/// </summary>
public sealed class PromotionListQuery
{
    /// <summary>`Draft` | `Approved` | `Active` | `Paused` | `Expired` | `Cancelled` (bỏ trống = tất cả).</summary>
    public PromotionStatus? Status { get; set; }

    /// <summary>`Discount` | `Voucher` | … (bỏ trống = tất cả).</summary>
    public PromotionKind? Kind { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = PromotionApiPaging.PageSizeMacDinh;

    /// <summary>Ví dụ `CreationTime desc` — chuyển nguyên văn cho app service.</summary>
    public string? Sort { get; set; }
}

/// <summary>Chuẩn hoá phân trang của lớp `/api/v1` — cùng mặc định/trần với `NotificationApiPaging`/`ProductsController`.</summary>
internal static class PromotionApiPaging
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
