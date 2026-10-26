using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Application.Contracts.Sales;

/// <summary>
/// **API CHUYỂN TRẠNG THÁI ĐƠN** (`SAL-003` §6) — mỗi method là **một trục + một quyền riêng**.
///
/// 🚨 **CẤM** có `ChangePaymentStatusAsync`: trục tài chính **chỉ** PaymentHub/IPN đã xác thực chữ ký ghi
/// (BR-013 / quyết định `D11`). Mở một API đặt tay "đã thanh toán" là mở đường **thu tiền hai lần**.
/// </summary>
public interface IOrderStatusAppService : IApplicationService
{
    Task<OrderStatusDto> ChangeFulfillmentStatusAsync(ChangeFulfillmentStatusDto input);

    Task<OrderStatusDto> ChangeReconciliationStatusAsync(ChangeReconciliationStatusDto input);

    Task<OrderStatusDto> RequestReturnAsync(RequestReturnDto input);

    Task<OrderStatusDto> ApproveReturnAsync(ApproveReturnDto input);

    Task<OrderStatusDto> CancelOrderAsync(CancelOrderDto input);

    /// <summary>Lịch sử trạng thái — **mới nhất trước** (`NFR-SAL-003-010`).</summary>
    Task<PagedResultDto<OrderStatusHistoryDto>> GetStatusHistoryAsync(Guid orderId, PagedResultRequestDto input);
}
