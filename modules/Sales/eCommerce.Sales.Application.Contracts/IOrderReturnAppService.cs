using eCommerce.Sales.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Sales;

/// <summary>
/// 🆕 **`S14` — HỢP ĐỒNG PHIẾU TRẢ HÀNG (RMA)** (`SAL-006`).
///
/// 🔐 Quyền ở **lớp hiện thực**: yêu cầu trả = `Orders.ReturnRequest` · duyệt/từ chối/ghi nhận hoàn tiền =
/// `Orders.ReturnApprove` · nhận hàng/đóng = `Orders.Manage` (tách **người yêu cầu ≠ người duyệt** — `BR-013`).
/// 📌 Chưa có UI: API trước, màn hình thuộc `S18`.
/// </summary>
public interface IOrderReturnAppService : IApplicationService
{
    /// <summary>Danh sách phiếu trả, lọc theo đơn (tuỳ chọn).</summary>
    Task<List<OrderReturnDto>> GetListAsync(Guid? orderId = null);

    Task<OrderReturnDto> GetAsync(Guid id);

    /// <summary>
    /// Tạo phiếu trả. **NÉM** `Sales:0010`/`Sales:0011` khi dữ liệu sai hoặc **trả vượt** số đã mua.
    /// 🔒 Idempotent theo `IdempotencyKey`.
    /// </summary>
    Task<OrderReturnDto> TaoPhieuAsync(CreateOrderReturnInput input);

    Task<OrderReturnDto> DuyetAsync(Guid id);

    Task<OrderReturnDto> TuChoiAsync(Guid id, LyDoTraHangInput input);

    Task<OrderReturnDto> HuyAsync(Guid id, LyDoTraHangInput input);

    /// <summary>Ghi nhận hàng hoàn đã về (từng dòng + cách xử lý).</summary>
    Task<OrderReturnDto> NhanHangAsync(Guid id, ReceiveOrderReturnInput input);

    Task<OrderReturnDto> DongAsync(Guid id);

    /// <summary>Ghi nhận **đã hoàn tiền** (thủ công, `Q4`).</summary>
    Task<OrderReturnDto> GhiNhanHoanTienAsync(Guid id, GhiNhanHoanTienInput input);
}
