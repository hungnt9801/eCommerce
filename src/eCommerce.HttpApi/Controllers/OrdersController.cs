using eCommerce.Application.Contracts.Sales;
using eCommerce.HttpApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// 🆕 **`WP-05`** — **ĐƠN HÀNG, bề mặt `/api/v1/orders`** cho Web Admin màn #10/#11 (`04 §5`).
///
/// 🔐 **Quyền tách theo VIỆC, không theo controller:**
/// <list type="bullet">
///   <item>`GET` (DS/chi tiết/lịch sử) ⇒ `eCommerce.Orders` — người chỉ xem **không** cần quyền ghi;</item>
///   <item>`POST …/transitions` ⇒ `eCommerce.Orders.StatusChange` (đổi trục **thực hiện**);</item>
///   <item>`POST …/cancel` ⇒ `eCommerce.Orders.Cancel` (**huỷ** — quyền riêng, xem `CancelOrderByStatusDto`).</item>
/// </list>
/// 🚨 **KHÔNG có đường ghi TRỤC TÀI CHÍNH** (`BR-013`): tiền chỉ do `PaymentHub`/IPN đã xác thực chữ ký ghi.
///
/// 📌 **VÌ SAO MỌI ĐƯỜNG GHI ĐI QUA APP SERVICE:** state machine + `RowVersion` + tác động kho nằm ở
/// `OrderStatusAppService`; controller ghi tắt (đụng repository) sẽ **bỏ qua** cả ba.
/// </summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize(eCommerceConsts.Permissions.Orders.Default)]
public class OrdersController : AbpControllerBase
{
    private readonly IOrderAppService _donHang;
    private readonly IOrderStatusAppService _trangThai;

    public OrdersController(IOrderAppService donHang, IOrderStatusAppService trangThai)
    {
        _donHang = donHang;
        _trangThai = trangThai;
    }

    /// <summary>
    /// **DS đơn** có phân trang + lọc server-side (`tab`, `q`, `fulfillmentStatus`, `type`, `source`,
    /// `warehouseId`, `fromUtc`, `toUtc`).
    /// 📌 Enum nhận **tên** hoặc **số** (ASP.NET tự bind) — UI gửi **số** (hợp đồng dữ liệu với Nhanh, `D-33`).
    /// </summary>
    [HttpGet]
    public Task<PagedResultDto<OrderListItemDto>> GetListAsync([FromQuery] OrderListInput input)
        => _donHang.GetListAsync(input);

    /// <summary>
    /// **Bộ đếm 14 tab** (tab có badge số — pattern Nhanh). Hứng **cùng** bộ lọc với DS ⇒ hai bên không lệch.
    /// 📌 Đặt **trước** route `{id:guid}` không quan trọng (khác kiểu tham số), nhưng để rõ nghĩa.
    /// </summary>
    [HttpGet("tabs")]
    public Task<OrderTabCountsDto> GetTabCountsAsync([FromQuery] OrderListInput input)
        => _donHang.GetTabCountsAsync(input);

    /// <summary>Chi tiết đơn + dòng hàng + **các bước đi tiếp được** (`allowedTransitions`) + `canCancel`.</summary>
    [HttpGet("{id:guid}")]
    public Task<OrderDetailDto> GetAsync(Guid id) => _donHang.GetAsync(id);

    /// <summary>
    /// **Lịch sử trạng thái** (append-only, mới nhất trước) — tab *"Lịch trình"* của màn chi tiết.
    /// 🔐 `eCommerce.Orders.History.View`.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [Authorize(eCommerceConsts.Permissions.Orders.HistoryView)]
    public Task<PagedResultDto<OrderStatusHistoryDto>> GetHistoryAsync(Guid id, [FromQuery] int skipCount = 0,
        [FromQuery] int maxResultCount = 50)
        => _trangThai.GetStatusHistoryAsync(id, new PagedResultRequestDto
        {
            SkipCount = skipCount < 0 ? 0 : skipCount,
            MaxResultCount = maxResultCount is < 1 or > 200 ? 50 : maxResultCount
        });

    /// <summary>
    /// **ĐỔI TRẠNG THÁI THỰC HIỆN** (`POST …/transitions` — `03 §5.2`): body `{ toStatus, expectedVersion,
    /// reasonCode, reasonText }`.
    ///
    /// 🚨 **Ba hàng rào, không thay thế nhau:** ① `[Authorize(StatusChange)]`; ② **cạnh hợp lệ** của state machine
    /// (`0001` nếu nhảy cóc); ③ `ExpectedVersion` lệch ⇒ `0003` (không ghi đè im lặng).
    /// 📌 **Bút toán kho đi kèm** (`D-39`): `→ Confirmed` = **chốt giữ chỗ** (xuất kho); `→ Returned` = **nhập hàng
    /// hoàn**. Kết quả trả về nói rõ `stockEffect`/`stockAffectedLines`/`warning` — UI **không** phải đoán.
    /// 📌 Ba trạng thái **huỷ** bị chặn ở đây (`0010`) ⇒ phải gọi `POST …/cancel` (quyền khác).
    /// </summary>
    [HttpPost("{id:guid}/transitions")]
    [Authorize(eCommerceConsts.Permissions.Orders.StatusChange)]
    public Task<OrderStatusDto> TransitionAsync(Guid id, [FromBody] ChangeOrderStatusBody body)
        => _trangThai.ChangeFulfillmentStatusAsync(new ChangeFulfillmentStatusDto
        {
            OrderId = id,
            ToStatus = body.ToStatus,
            ExpectedVersion = body.ExpectedVersion,
            ReasonCode = body.ReasonCode,
            ReasonText = body.ReasonText
        });

    /// <summary>
    /// **HUỶ ĐƠN** — nêu **AI huỷ** + **lý do** (24 giá trị Nhanh). 🔐 `eCommerce.Orders.Cancel`.
    /// 📌 Hàng **đã rời kho** (`Confirmed` trở đi) ⇒ **403 `0001`**: phải TRẢ HÀNG (chứng từ nhập), cấm xoá
    /// bút toán (sổ cái append-only). 📌 Đơn **đã huỷ** ⇒ trả trạng thái hiện tại (**idempotent**), không nhả
    /// giữ chỗ lần hai.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(eCommerceConsts.Permissions.Orders.Cancel)]
    public Task<OrderStatusDto> CancelAsync(Guid id, [FromBody] CancelOrderBody body)
        => _trangThai.CancelOrderByStatusAsync(new CancelOrderByStatusDto
        {
            OrderId = id,
            CancelledBy = body.CancelledBy,
            ExpectedVersion = body.ExpectedVersion,
            ReasonCode = body.ReasonCode,
            BusinessReason = body.BusinessReason,
            ReasonText = body.ReasonText
        });
}

/// <summary>
/// Body của `POST /orders/{id}/transitions`.
/// 📌 **DTO riêng (không bind thẳng `ChangeFulfillmentStatusDto`)**: `OrderId` lấy từ **route**, không từ body —
/// để client không thể gửi body trỏ đơn khác route (một dạng vượt quyền ngầm).
/// </summary>
public class ChangeOrderStatusBody
{
    /// <summary>Trạng thái đích (`FulfillmentStatus` — nhận số hoặc tên).</summary>
    public FulfillmentStatus ToStatus { get; set; }

    /// <summary>`FulfillmentVersion` UI đã đọc (`BR-009`).</summary>
    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public string? ReasonText { get; set; }
}

/// <summary>Body của `POST /orders/{id}/cancel` — cùng lý do tách `OrderId` khỏi body như trên.</summary>
public class CancelOrderBody
{
    /// <summary>`CustomerCancelled` (63) · `SystemCancelled` (64) · `CarrierCancelled` (58).</summary>
    public FulfillmentStatus CancelledBy { get; set; }

    public long ExpectedVersion { get; set; }

    public string ReasonCode { get; set; } = default!;

    public OrderReasonCode? BusinessReason { get; set; }

    public string? ReasonText { get; set; }
}
