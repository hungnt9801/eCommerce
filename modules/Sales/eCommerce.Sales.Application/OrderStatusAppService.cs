using eCommerce.Application.Contracts.Sales;
using eCommerce.Inventory;
using eCommerce.Orders;
using eCommerce.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace eCommerce.Application.Sales;

/// <summary>
/// **API CHUYỂN TRẠNG THÁI ĐƠN** (`SAL-003` §6.1) — mọi đường ghi đi qua **state machine của domain**
/// (`Order.Chuyen*`), không có setter nào lộ ra ngoài.
///
/// 🔒 **HAI LỚP BẢO VỆ (không thay thế nhau):**
///   ① `[Authorize(permission)]` theo **từng trục** — người giao hàng không có quyền đối soát, người đối soát
///      không có quyền duyệt trả;
///   ② **guard của domain** (`Order.Chuyen*`) — kể cả người có quyền cũng **không** nhảy cóc được
///      (`Pending → Delivered` ⇒ `eCommerce:Order:0001`).
///
/// ⚠️ **`ExpectedVersion`** (BR-009): UI đọc `FulfillmentVersion`/`ClosureVersion`… rồi gửi lại; lệch ⇒
/// `eCommerce:Order:0003` (**không** ghi đè im lặng — đó là *lost update*).
///
/// 📌 **Vì sao `CancelOrderAsync` nhả giữ chỗ tại đây:** nghiệp vụ bù trừ là của `SAL-004`; app service này
/// gọi **cùng** `StockReservationService.NhaTheoNguonAsync` như `DonHangAppService.HuyDonAsync` — nếu chỉ
/// đổi trạng thái mà không nhả chỗ thì hàng **nằm chết** tới hết TTL dù đơn đã huỷ.
/// </summary>
[Authorize]
public class OrderStatusAppService : ApplicationService, IOrderStatusAppService
{
    private readonly IRepository<Order, Guid> _donHang;
    private readonly IRepository<OrderLine, Guid> _dongDon;
    private readonly IRepository<OrderStatusHistory, Guid> _lichSu;
    private readonly IStockReservationGateway _giuCho;
    private readonly ICustomerReturnGateway _nhapHangHoan;
    private readonly ISettingProvider _settings;

    public OrderStatusAppService(
        IRepository<Order, Guid> donHang,
        IRepository<OrderLine, Guid> dongDon,
        IRepository<OrderStatusHistory, Guid> lichSu,
        IStockReservationGateway giuCho,
        ICustomerReturnGateway nhapHangHoan,
        ISettingProvider settings)
    {
        _donHang = donHang;
        _dongDon = dongDon;
        _lichSu = lichSu;
        _giuCho = giuCho;
        _nhapHangHoan = nhapHangHoan;
        _settings = settings;
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.StatusChange)]
    public async Task<OrderStatusDto> ChangeFulfillmentStatusAsync(ChangeFulfillmentStatusDto input)
    {
        // 🚨 Huỷ KHÔNG đi cửa này: huỷ cần quyền `Orders.Cancel` (xem `CancelOrderByStatusAsync`).
        // 📌 Chặn **trước** khi đọc đơn để thông điệp nói đúng *"gọi đường khác"*, không phải *"cạnh không tồn tại"*.
        if (OrderStateMachine.LaTrangThaiHuy(input.ToStatus))
        {
            throw new BusinessException(OrderErrorCodes.TrangThaiCanDuongKhac)
                .WithData("to", input.ToStatus.ToString())
                .WithData("lyDo", "Huỷ đơn phải gọi POST /orders/{id}/cancel (quyền eCommerce.Orders.Cancel).");
        }

        var don = await _donHang.GetAsync(input.OrderId);

        KiemPhienBan(don.FulfillmentVersion, input.ExpectedVersion, OrderAxis.Fulfillment);

        var lichSu = don.ChuyenThucHien(
            input.ToStatus,
            StatusActorType.Staff,
            CurrentUser.Id,
            input.ReasonCode,
            correlationId: null,
            input.ReasonText);

        // 🆕 **`WP-05`** — bút toán kho của bước này (hàm thuần quyết định, xem `D-39`).
        var (tacDong, soAnhHuong, canhBao) = await ThiHanhTacDongKhoAsync(don, input.ToStatus);

        await LuuAsync(don);

        return ToDto(don, lichSu.Id, tacDong, soAnhHuong, canhBao);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.Cancel)]
    public async Task<OrderStatusDto> CancelOrderByStatusAsync(CancelOrderByStatusDto input)
    {
        var don = await _donHang.GetAsync(input.OrderId);

        // 📌 Idempotent (cùng khuôn `CancelOrderAsync`): huỷ hai lần ⇒ trả trạng thái hiện tại, KHÔNG nhả chỗ lần nữa.
        if (don.IsCancelled)
        {
            return ToDto(don, null);
        }

        KiemPhienBan(don.ClosureVersion, input.ExpectedVersion, OrderAxis.Closure);

        var soNha = await NhaGiuChoAsync(don);

        var lichSu = don.HuyDonNhanh(
            input.CancelledBy,
            StatusActorType.Staff,
            CurrentUser.Id,
            input.ReasonCode,
            input.BusinessReason,
            correlationId: null,
            input.ReasonText ?? $"Nhả {soNha} lượt giữ chỗ.");

        await LuuAsync(don);

        Logger.LogInformation(
            "HUỶ ĐƠN ({Ai}) {Number}: đã nhả {So} lượt giữ chỗ; lý do nghiệp vụ {LyDo}.",
            input.CancelledBy, don.Number, soNha, input.BusinessReason?.ToString() ?? "(không nêu)");

        // 📌 2 dòng lịch sử (thực hiện + kết thúc) ⇒ trả dòng **sau cùng** để UI cuộn tới bước quyết định.
        return ToDto(don, lichSu[^1].Id);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.ReconcileManage)]
    public async Task<OrderStatusDto> ChangeReconciliationStatusAsync(ChangeReconciliationStatusDto input)
    {
        var don = await _donHang.GetAsync(input.OrderId);

        KiemPhienBan(don.ReconciliationVersion, input.ExpectedVersion, OrderAxis.Reconciliation);

        var lichSu = don.ChuyenDoiSoat(
            input.ToStatus,
            StatusActorType.Staff,
            CurrentUser.Id,
            input.ReasonCode,
            correlationId: null,
            input.ReasonText);

        await LuuAsync(don);

        return ToDto(don, lichSu.Id);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.ReturnRequest)]
    public async Task<OrderStatusDto> RequestReturnAsync(RequestReturnDto input)
    {
        var don = await _donHang.GetAsync(input.OrderId);

        KiemPhienBan(don.ClosureVersion, input.ExpectedVersion, OrderAxis.Closure);

        // 🚨 GUARD NGHIỆP VỤ của cạnh 25 (`SAL-003` §3): **chỉ** yêu cầu trả khi đã GIAO THÀNH CÔNG.
        // 📌 Bảng chuyển của domain chỉ biết `Open → ReturnRequested`; điều kiện *"đã giao"* + *"trong cửa sổ"*
        // cần dữ liệu (lịch sử giao) + setting ⇒ đặt ở app service, **không** nhồi vào entity (entity không có
        // `ISettingProvider`, và nhồi sẽ khiến domain phụ thuộc hạ tầng).
        if (don.FulfillmentStatus != FulfillmentStatus.Delivered)
        {
            throw new BusinessException(OrderErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("axis", OrderAxis.Closure.ToString())
                .WithData("from", don.ReturnStatus.ToString())
                .WithData("to", ReturnStatus.ReturnRequested.ToString())
                .WithData("lyDo", "Chỉ yêu cầu trả khi đơn đã GIAO THÀNH CÔNG (SAL-003 §3, cạnh 25).");
        }

        var soNgay = await LayCuaSoDoiTraAsync();
        var lichSuGiao = await _lichSu.GetListAsync(
            x => x.OrderId == don.Id
                 && x.Axis == "Fulfillment"
                 && x.ToStatus == (int)FulfillmentStatus.Delivered);

        var mocGiao = lichSuGiao.Count == 0 ? (DateTime?)null : lichSuGiao.Max(x => x.OccurredAtUtc);

        if (mocGiao is null || Clock.Now - mocGiao.Value > TimeSpan.FromDays(soNgay))
        {
            throw new BusinessException(OrderErrorCodes.ChuyenTrangThaiKhongHopLe)
                .WithData("axis", OrderAxis.Closure.ToString())
                .WithData("from", don.ReturnStatus.ToString())
                .WithData("to", ReturnStatus.ReturnRequested.ToString())
                .WithData("soNgayCuaSo", soNgay)
                .WithData("mocGiaoUtc", mocGiao)
                .WithData("lyDo", $"Đã quá cửa sổ đổi trả {soNgay} ngày kể từ lúc giao thành công.");
        }

        var lichSu = don.ChuyenKetThuc(
            ReturnStatus.ReturnRequested,
            StatusActorType.Staff,
            CurrentUser.Id,
            input.ReasonCode,
            correlationId: null,
            input.ReasonText);

        await LuuAsync(don);

        return ToDto(don, lichSu.Id);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.ReturnApprove)]
    public async Task<OrderStatusDto> ApproveReturnAsync(ApproveReturnDto input)
    {
        var don = await _donHang.GetAsync(input.OrderId);

        KiemPhienBan(don.ClosureVersion, input.ExpectedVersion, OrderAxis.Closure);

        var lichSu = don.ChuyenKetThuc(
            input.Approve ? ReturnStatus.ReturnApproved : ReturnStatus.ReturnRejected,
            StatusActorType.Staff,
            CurrentUser.Id,
            input.ReasonCode,
            correlationId: null,
            input.ReasonText);

        await LuuAsync(don);

        return ToDto(don, lichSu.Id);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.Cancel)]
    public async Task<OrderStatusDto> CancelOrderAsync(CancelOrderDto input)
    {
        var don = await _donHang.GetAsync(input.OrderId);

        // 📌 Idempotent (cùng khuôn `DonHangAppService.HuyDonAsync`): huỷ hai lần ⇒ trả trạng thái hiện tại,
        // **không** nhả chỗ lần nữa (nhả hai lần sẽ bị `Inventory:0030`).
        if (don.Status == OrderStatus.Cancelled)
        {
            return ToDto(don, null);
        }

        KiemPhienBan(don.ClosureVersion, input.ExpectedVersion, OrderAxis.Closure);

        // 🚨 NGHIỆP VỤ BÙ TRỪ — cùng một đường với `DonHangAppService.HuyDonAsync`:
        // đơn `Pending` đã giữ chỗ ⇒ nhả NGAY để hàng bán được cho người khác (không chờ TTL 15 phút).
        var soNha = await NhaGiuChoAsync(don);

        var lichSu = don.HuyDon(
            StatusActorType.Staff,
            CurrentUser.Id,
            input.ReasonCode,
            correlationId: null,
            reasonText: input.ReasonText ?? $"Nhả {soNha} lượt giữ chỗ.");

        await LuuAsync(don);

        Logger.LogInformation(
            "HUỶ ĐƠN (API trạng thái) {Number}: đã nhả {So} lượt giữ chỗ.", don.Number, soNha);

        return ToDto(don, lichSu.Id);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Orders.HistoryView)]
    public async Task<PagedResultDto<OrderStatusHistoryDto>> GetStatusHistoryAsync(
        Guid orderId,
        PagedResultRequestDto input)
    {
        // ⚠️ Không ném khi đơn không tồn tại: lịch sử là **dữ liệu riêng**, và trả mảng rỗng cho đơn lạ
        // **không** tiết lộ đơn có tồn tại hay không (`XC-004` BR-008). 📌 UI phân biệt bằng `totalCount = 0`.
        var tatCa = await _lichSu.GetListAsync(x => x.OrderId == orderId);

        var sapXep = tatCa
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.VersionAfter)
            .ToList();

        var trang = sapXep
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .Select(ToDto)
            .ToList();

        return new PagedResultDto<OrderStatusHistoryDto>(sapXep.Count, trang);
    }

    /// <summary>
    /// Lưu đơn + **map xung đột đồng thời** thành `eCommerce:Order:0002` (BR-010).
    /// 🔑 EF dùng **giá trị `RowVersion` GỐC** trong `WHERE`; ai ghi trước thì người sau nhận
    /// `DbUpdateConcurrencyException`. 📌 Nếu để nó nổi lên ⇒ `500` mù ⇒ client tưởng "hệ thống hỏng"
    /// thay vì *"người khác vừa sửa, tải lại"*.
    /// </summary>
    private async Task LuuAsync(Order don)
    {
        try
        {
            await _donHang.UpdateAsync(don, autoSave: true);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException(OrderErrorCodes.XungDotPhienBan)
                .WithData("orderId", don.Id)
                .WithData("rowVersion", don.RowVersion)
                .WithData("lyDo", "Đơn vừa bị người khác sửa ⇒ tải lại rồi thao tác.");
        }
    }

    /// <summary>
    /// 🆕 **`WP-05`** — **THI HÀNH TÁC ĐỘNG KHO** của bước vừa đổi trạng thái (`D-39`).
    ///
    /// 🔑 **LUẬT NẰM Ở HÀM THUẦN** (`OrderStateMachine.TacDongKhoKhiVao`); hàm này chỉ **gọi gateway** của module
    /// `Inventory` — **không** tự ghi sổ cái, **không** đụng repository kho. Nhờ vậy chỉ có **một** đường ghi
    /// (`ChotGiuChoService` / `CustomerReturnGateway`) và rollback là **cùng một transaction** (ABP UoW).
    ///
    /// 📌 **Idempotent theo nghiệp vụ:** chốt giữ chỗ trả về `0` khi đã chốt (lượt không còn `Held`);
    /// nhập hàng hoàn **idempotent theo số chứng từ** (`HOAN-{số đơn}`) ⇒ bấm lại/retry **không** nhân đôi tồn.
    /// </summary>
    private async Task<(OrderStateMachine.TacDongKho TacDong, int SoAnhHuong, string? CanhBao)>
        ThiHanhTacDongKhoAsync(Order don, FulfillmentStatus den)
    {
        switch (OrderStateMachine.TacDongKhoKhiVao(den))
        {
            case OrderStateMachine.TacDongKho.ChotGiuCho:
                if (string.IsNullOrWhiteSpace(don.CheckoutIdempotencyKey))
                {
                    // 📌 Đơn tạo tay **không** có giữ chỗ ⇒ không có gì để chốt. KHÔNG ném: xác nhận đơn tay là
                    // hợp lệ; nhưng **phải nói ra** để người dùng không tin "kho đã trừ".
                    Logger.LogWarning(
                        "XÁC NHẬN ĐƠN {Number}: KHÔNG có `CheckoutIdempotencyKey` ⇒ KHÔNG có lượt giữ chỗ nào để chốt " +
                        "⇒ tồn kho KHÔNG đổi. [WP05-KHONG-GIU-CHO]", don.Number);

                    return (OrderStateMachine.TacDongKho.ChotGiuCho, 0,
                        "Đơn không có lượt giữ chỗ (tạo tay) ⇒ xác nhận KHÔNG sinh bút toán kho.");
                }

                var soChot = await _giuCho.ChotTheoNguonAsync(
                    $"Checkout:{don.CheckoutIdempotencyKey}", Clock.Now);

                if (soChot == 0)
                {
                    Logger.LogWarning(
                        "XÁC NHẬN ĐƠN {Number}: không còn lượt giữ chỗ `Held` nào ⇒ có thể ĐÃ chốt trước đó " +
                        "(idempotent) hoặc giữ chỗ đã hết hạn. Tồn kho lần này KHÔNG đổi. [WP05-CHOT-0]",
                        don.Number);

                    return (OrderStateMachine.TacDongKho.ChotGiuCho, 0,
                        "Không còn lượt giữ chỗ để chốt (đã chốt trước đó hoặc đã hết hạn) ⇒ tồn kho không đổi.");
                }

                return (OrderStateMachine.TacDongKho.ChotGiuCho, soChot, null);

            case OrderStateMachine.TacDongKho.NhapHangHoan:
                var khoId = don.WarehouseId
                    ?? throw new BusinessException(OrderErrorCodes.ThieuKhoKhiHoan)
                        .WithData("number", don.Number)
                        .WithData("lyDo",
                            "Đơn chưa gán kho xuất hàng ⇒ KHÔNG biết nhập hàng hoàn vào kho nào. " +
                            "Gán kho cho đơn rồi thao tác lại.");

                var dong = await _dongDon.GetListAsync(x => x.OrderId == don.Id);
                var lines = dong.Select(x => (x.Sku, x.Quantity)).ToList();

                if (lines.Count == 0)
                {
                    // 📌 Đơn không có dòng thì "hoàn" cũng không có gì để nhập; ném để không tạo chứng từ rỗng
                    // (`CustomerReturn` 0 dòng sẽ bị `ChungTuThieuDong`).
                    throw new BusinessException(OrderErrorCodes.DuLieuDonKhongHopLe)
                        .WithData("number", don.Number)
                        .WithData("lyDo", "Đơn KHÔNG có dòng hàng ⇒ không thể nhập hàng hoàn.");
                }

                var chungTuId = await _nhapHangHoan.NhapHangKhachTraAsync(
                    khoId,
                    $"HOAN-{don.Number}",
                    lines,
                    Clock.Now,
                    note: $"Hàng hoàn của đơn {don.Number} (Đã hoàn).");

                Logger.LogInformation(
                    "ĐƠN {Number} ĐÃ HOÀN: đã nhập lại kho {Kho} bằng chứng từ {ChungTu}. [WP05-NHAP-HOAN]",
                    don.Number, khoId, chungTuId);

                return (OrderStateMachine.TacDongKho.NhapHangHoan, lines.Count, null);

            default:
                return (OrderStateMachine.TacDongKho.Khong, 0, null);
        }
    }

    /// <summary>Nhả mọi lượt giữ chỗ `Held` của đơn; trả **số lượt đã nhả**. `0` khi đơn không có khoá checkout.</summary>
    private Task<int> NhaGiuChoAsync(Order don)
        => string.IsNullOrWhiteSpace(don.CheckoutIdempotencyKey)
            ? Task.FromResult(0)
            : _giuCho.NhaTheoNguonAsync(
                $"Checkout:{don.CheckoutIdempotencyKey}",
                Clock.Now,
                ReservationStatus.Released);

    /// <summary>
    /// Đọc **cửa sổ đổi trả** từ setting `Orders.ReturnWindowDays` (mặc định **7**).
    /// 🔒 Dùng `ChinhSach.DocSo` — **không ném** khi setting bị sửa thành giá trị lạ:
    /// *"dữ liệu cấu hình cũng là dữ liệu vào"* ⇒ dùng mặc định an toàn + **ghi cảnh báo** (không làm chết API).
    /// </summary>
    private async Task<int> LayCuaSoDoiTraAsync()
    {
        var giaTriTho = await _settings.GetOrNullAsync(TenChinhSach.DonHang_CuaSoDoiTra_Ngay);
        var soNgay = ChinhSach.DocSo(giaTriTho, 7, out var hopLe);

        if (!hopLe)
        {
            Logger.LogWarning(
                "Setting {Ten} có giá trị KHÔNG hợp lệ ({GiaTri}) ⇒ dùng mặc định 7 ngày. [SAL-003-CUA-SO]",
                TenChinhSach.DonHang_CuaSoDoiTra_Ngay, giaTriTho);
        }

        return (int)Math.Floor(soNgay);
    }

    /// <summary>
    /// BR-009: `ExpectedVersion` lệch ⇒ `eCommerce:Order:0003`. 🔑 **Không** tự sửa/không bỏ qua —
    /// bỏ qua nghĩa là hai người cùng bấm, người sau **ghi đè** người trước mà không ai biết.
    /// </summary>
    private static void KiemPhienBan(long hienTai, long guiLen, OrderAxis truc)
    {
        if (hienTai != guiLen)
        {
            throw new BusinessException(OrderErrorCodes.PhienBanLech)
                .WithData("axis", truc.ToString())
                .WithData("expectedVersion", guiLen)
                .WithData("currentVersion", hienTai);
        }
    }

    private static OrderStatusDto ToDto(
        Order don,
        Guid? lichSuId,
        OrderStateMachine.TacDongKho tacDong = OrderStateMachine.TacDongKho.Khong,
        int soAnhHuong = 0,
        string? canhBao = null) => new()
    {
        OrderId = don.Id,
        FulfillmentStatus = don.FulfillmentStatus,
        PaymentStatus = don.PaymentStatus,
        ReconciliationStatus = don.ReconciliationStatus,
        ReturnStatus = don.ReturnStatus,
        IsCancelled = don.IsCancelled,
        DerivedStatus = don.Status,
        StatusLabel = OrderStateMachine.NhanHienThi(don.FulfillmentStatus, don.ReturnStatus, don.IsCancelled),
        StockEffect = tacDong.ToString(),
        StockAffectedLines = soAnhHuong,
        Warning = canhBao,
        FulfillmentVersion = don.FulfillmentVersion,
        PaymentVersion = don.PaymentVersion,
        ReconciliationVersion = don.ReconciliationVersion,
        ClosureVersion = don.ClosureVersion,
        HistoryEntryId = lichSuId
    };

    private static OrderStatusHistoryDto ToDto(OrderStatusHistory lichSu) => new()
    {
        Id = lichSu.Id,
        OrderId = lichSu.OrderId,
        Axis = lichSu.Axis,
        FromStatus = lichSu.FromStatus,
        ToStatus = lichSu.ToStatus,
        ActorType = lichSu.ActorType,
        ActorId = lichSu.ActorId,
        ReasonCode = lichSu.ReasonCode,
        ReasonText = lichSu.ReasonText,
        CorrelationId = lichSu.CorrelationId,
        VersionAfter = lichSu.VersionAfter,
        OccurredAtUtc = lichSu.OccurredAtUtc
    };
}
