using eCommerce.Orders;
using eCommerce.Sales.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Sales;

/// <summary>
/// 🆕 **`S14` — APP SERVICE PHIẾU TRẢ HÀNG** (`SAL_006`). 🚨 **Không** chứa nghiệp vụ: guard + máy trạng thái
/// nằm ở `OrderReturn`/`OrderReturnService` (một định nghĩa cho API và mọi consumer sau này).
/// </summary>
[Authorize(eCommerceConsts.Permissions.Orders.Default)]
public class OrderReturnAppService : ApplicationService, IOrderReturnAppService
{
    private readonly IRepository<OrderReturn, Guid> _phieuTra;
    private readonly IRepository<OrderReturnLine, Guid> _dongTra;
    private readonly OrderReturnService _xuLy;

    public OrderReturnAppService(
        IRepository<OrderReturn, Guid> phieuTra,
        IRepository<OrderReturnLine, Guid> dongTra,
        OrderReturnService xuLy)
    {
        _phieuTra = phieuTra;
        _dongTra = dongTra;
        _xuLy = xuLy;
    }

    public async Task<List<OrderReturnDto>> GetListAsync(Guid? orderId = null)
    {
        var danhSach = orderId is { } id
            ? await _phieuTra.GetListAsync(x => x.OrderId == id)
            : await _phieuTra.GetListAsync();

        var dong = await _dongTra.GetListAsync();
        var theoPhieu = dong.GroupBy(x => x.ReturnId).ToDictionary(g => g.Key, g => g.ToList());

        return danhSach
            .OrderByDescending(x => x.CreationTime)
            .Select(x => ToDto(x, theoPhieu.TryGetValue(x.Id, out var ds) ? ds : []))
            .ToList();
    }

    public async Task<OrderReturnDto> GetAsync(Guid id)
    {
        var phieu = await _phieuTra.GetAsync(id);
        var dong = await _dongTra.GetListAsync(x => x.ReturnId == id);

        return ToDto(phieu, dong);
    }

    [Authorize(eCommerceConsts.Permissions.Orders.ReturnRequest)]
    public async Task<OrderReturnDto> TaoPhieuAsync(CreateOrderReturnInput input)
    {
        var phieu = await _xuLy.TaoPhieuAsync(
            input.OrderId, input.WarehouseId, input.ReasonCode, input.ReasonText,
            input.Lines.Select(l => (l.OrderLineId, l.Quantity)).ToList(),
            input.IdempotencyKey, StatusActorType.Staff, CurrentUser.Id);

        Logger.LogInformation(
            "PHIẾU TRẢ (`S14`): {Phieu} cho đơn {Don} · {SoDong} dòng. [S14-API-TAO]",
            phieu.ReturnNumber, phieu.OrderId, input.Lines.Count);

        return await GetAsync(phieu.Id);
    }

    [Authorize(eCommerceConsts.Permissions.Orders.ReturnApprove)]
    public async Task<OrderReturnDto> DuyetAsync(Guid id)
        => await GetAsync((await _xuLy.DuyetAsync(id, StatusActorType.Staff, CurrentUser.Id)).Id);

    [Authorize(eCommerceConsts.Permissions.Orders.ReturnApprove)]
    public async Task<OrderReturnDto> TuChoiAsync(Guid id, LyDoTraHangInput input)
        => await GetAsync((await _xuLy.TuChoiAsync(id, input.LyDo, StatusActorType.Staff, CurrentUser.Id)).Id);

    [Authorize(eCommerceConsts.Permissions.Orders.ReturnRequest)]
    public async Task<OrderReturnDto> HuyAsync(Guid id, LyDoTraHangInput input)
        => await GetAsync((await _xuLy.HuyAsync(id, input.LyDo, StatusActorType.Staff, CurrentUser.Id)).Id);

    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<OrderReturnDto> NhanHangAsync(Guid id, ReceiveOrderReturnInput input)
    {
        var ketQua = input.Lines.Select(l =>
        {
            if (!Enum.TryParse<ReturnDisposition>(l.Disposition, ignoreCase: true, out var xuLy))
            {
                throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                    .WithData("lyDo", "Cách xử lý hàng không hợp lệ.")
                    .WithData("disposition", l.Disposition);
            }

            return (l.ReturnLineId, l.ReceivedQuantity, xuLy);
        }).ToList();

        return await GetAsync((await _xuLy.NhanHangAsync(id, ketQua, StatusActorType.Staff, CurrentUser.Id)).Id);
    }

    [Authorize(eCommerceConsts.Permissions.Orders.Manage)]
    public async Task<OrderReturnDto> DongAsync(Guid id)
        => await GetAsync((await _xuLy.DongAsync(id, StatusActorType.Staff, CurrentUser.Id)).Id);

    [Authorize(eCommerceConsts.Permissions.Orders.ReturnApprove)]
    public async Task<OrderReturnDto> GhiNhanHoanTienAsync(Guid id, GhiNhanHoanTienInput input)
        => await GetAsync((await _xuLy.GhiNhanHoanTienAsync(id, input.SoTien, StatusActorType.Staff, CurrentUser.Id)).Id);

    private static OrderReturnDto ToDto(OrderReturn phieu, IReadOnlyCollection<OrderReturnLine> dong) => new()
    {
        Id = phieu.Id,
        OrderId = phieu.OrderId,
        ReturnNumber = phieu.ReturnNumber,
        ChannelCode = phieu.ChannelCode,
        Status = phieu.Status.ToString(),
        GoodsStatus = phieu.GoodsStatus.ToString(),
        ReasonCode = phieu.ReasonCode,
        ReasonText = phieu.ReasonText,
        WarehouseId = phieu.WarehouseId,
        RefundMethod = phieu.RefundMethod.ToString(),
        RefundedAmount = phieu.RefundedAmount,
        RefundCurrency = phieu.RefundCurrency,
        Version = phieu.Version,
        ReceivedAtUtc = phieu.ReceivedAtUtc,
        Lines = dong
            .OrderBy(x => x.Sku, StringComparer.Ordinal)
            .Select(x => new OrderReturnLineDto
            {
                Id = x.Id,
                OrderLineId = x.OrderLineId,
                Sku = x.Sku,
                Quantity = x.Quantity,
                ReceivedQuantity = x.ReceivedQuantity,
                Disposition = x.Disposition.ToString(),
                RefundAmount = x.RefundAmount
            })
            .ToList()
    };
}
