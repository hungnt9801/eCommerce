using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Orders;

/// <summary>
/// 🆕 **`S14` phần B — DỊCH VỤ PHIẾU TRẢ HÀNG** (`SAL-006`).
///
/// 🔑 **MỘT chỗ quyết định** *"khi nào đơn chuyển trục kết thúc"*: mọi chuyển dịch đều gọi
/// <see cref="Order.ChuyenKetThuc"/> (máy trạng thái `SAL-003`), **không** gán `ReturnStatus` trực tiếp.
/// 📌 Nhờ vậy cạnh `ReturnRequested → ReturnApproved → Returned` (và 🆕 `S14H`: `ReturnApproved →
/// PartiallyReturned → Returned`) được kiểm **một lần** và lịch sử đơn (`OrdStatusHistory`) vẫn do aggregate
/// đơn ghi — **không** có bản sao thứ hai của luật.
///
/// 🚨 **GIỮ CHỖ TRẢ NGAY KHI TẠO PHIẾU** (`OrderLine.TangTra`): phiếu bị **từ chối/huỷ** thì trả lại
/// (`GiamTra`). Hai phiếu đồng thời cùng dòng đơn ⇒ chỉ một phiếu qua được (khoá + guard) ⇒ **không trả vượt**.
/// </summary>
public class OrderReturnService : DomainService
{
    public const int ChoKhoaToiDaGiay = 30;

    private readonly IRepository<OrderReturn, Guid> _phieuTra;
    private readonly IRepository<OrderReturnLine, Guid> _dongTra;
    private readonly IRepository<OrderReturnHistory, Guid> _lichSu;
    private readonly IRepository<Order, Guid> _don;
    private readonly IRepository<OrderLine, Guid> _dongDon;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ICurrentTenant _currentTenant;

    public OrderReturnService(
        IRepository<OrderReturn, Guid> phieuTra,
        IRepository<OrderReturnLine, Guid> dongTra,
        IRepository<OrderReturnHistory, Guid> lichSu,
        IRepository<Order, Guid> don,
        IRepository<OrderLine, Guid> dongDon,
        IAbpDistributedLock distributedLock,
        ICurrentTenant currentTenant)
    {
        _phieuTra = phieuTra;
        _dongTra = dongTra;
        _lichSu = lichSu;
        _don = don;
        _dongDon = dongDon;
        _distributedLock = distributedLock;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Tạo phiếu trả. **NÉM** `Sales:0010` khi thiếu dữ liệu/đơn không hợp lệ; `Sales:0011` khi **trả vượt**
    /// số đã mua của một dòng.
    /// 📌 Idempotent theo `idempotencyKey`: gọi lại **cùng khoá** trả về phiếu cũ, **không** tạo phiếu thứ hai.
    /// </summary>
    public async Task<OrderReturn> TaoPhieuAsync(
        Guid orderId,
        Guid warehouseId,
        string reasonCode,
        string? reasonText,
        IReadOnlyCollection<(Guid OrderLineId, int Quantity)> lines,
        string idempotencyKey,
        StatusActorType actorType,
        Guid? actorId,
        DateTime? atUtc = null)
    {
        var at = atUtc ?? DateTime.UtcNow;

        if (lines.Count == 0)
        {
            throw Loi("Phiếu trả phải có ít nhất một dòng.");
        }

        var tenKhoa = $"sales:return:{_currentTenant.Id?.ToString() ?? "host"}:{orderId}";

        await using var khoa = await _distributedLock.TryAcquireAsync(tenKhoa, TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", tenKhoa).WithData("choGiay", ChoKhoaToiDaGiay);

        // ── IDEMPOTENCY (BR-003): cùng khoá ⇒ trả kết quả cũ, KHÔNG tạo phiếu thứ hai ──────────────
        var daCo = await _phieuTra.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);

        if (daCo is not null)
        {
            Logger.LogInformation(
                "PHIẾU TRẢ (`S14`): khoá idempotency {Khoa} đã dùng ⇒ trả phiếu cũ {Phieu}.",
                idempotencyKey, daCo.ReturnNumber);

            return daCo;
        }

        var don = await _don.FindAsync(orderId) ?? throw Loi("Không tìm thấy đơn.").WithData("orderId", orderId);

        if (don.Status == OrderStatus.Cancelled)
        {
            throw Loi("Đơn đã HUỶ ⇒ không tạo phiếu trả.").WithData("orderId", orderId);
        }

        var dongDon = await _dongDon.GetListAsync(x => x.OrderId == orderId);
        var theoId = dongDon.ToDictionary(x => x.Id);

        var payloadHash = BamPayload(orderId, lines);

        var soPhieu = SinhSoPhieuTra.Sinh(at, (int)await _phieuTra.CountAsync(x => x.CreationTime >= at.Date));
        var phieu = new OrderReturn(
            GuidGenerator.Create(), orderId, soPhieu, don.ChannelCode, warehouseId, reasonCode, reasonText,
            idempotencyKey, payloadHash, actorType.ToString(), actorId,
            refundCurrency: don.Currency, correlationId: null);

        await _phieuTra.InsertAsync(phieu, autoSave: false);

        var daTao = new List<OrderReturnLine>();

        foreach (var (orderLineId, soLuong) in lines)
        {
            if (!theoId.TryGetValue(orderLineId, out var dongGoc))
            {
                throw Loi("Dòng đơn KHÔNG thuộc đơn này.").WithData("orderLineId", orderLineId);
            }

            // 🚨 HÀNG RÀO ADR-11 — kiểm **và** giữ chỗ trên chính dòng đơn.
            dongGoc.TangTra(soLuong);
            await _dongDon.UpdateAsync(dongGoc, autoSave: false);

            var dongTra = new OrderReturnLine(
                GuidGenerator.Create(), phieu.Id, dongGoc.Id, dongGoc.Sku, soLuong,
                dongGoc.UnitPrice, dongGoc.DiscountAmount, dongGoc.TaxAmount);

            daTao.Add(dongTra);
            await _dongTra.InsertAsync(dongTra, autoSave: false);
        }

        // Đơn chuyển trục kết thúc sang `ReturnRequested` — qua máy trạng thái, không gán tay.
        var lichSuDon = don.ChuyenKetThuc(ReturnStatus.ReturnRequested, actorType, actorId, reasonCode,
            correlationId: null, reasonText: reasonText, occurredAtUtc: at);

        await _don.UpdateAsync(don, autoSave: false);

        await GhiLichSuAsync(phieu, "Lifecycle", -1, (int)ReturnLifecycleStatus.Requested, actorType, actorId,
            reasonCode, reasonText, at);

        Logger.LogInformation(
            "PHIẾU TRẢ (`S14`): tạo {Phieu} cho đơn {Don} · {SoDong} dòng · lý do {LyDo}. [S14-TAO]",
            phieu.ReturnNumber, don.Number, daTao.Count, reasonCode);

        // `lichSuDon` đã được aggregate đơn ghi vào `StatusHistory`; chỉ cần giữ tham chiếu để tránh cảnh báo.
        _ = lichSuDon;

        return phieu;
    }

    public async Task<OrderReturn> DuyetAsync(Guid id, StatusActorType actorType, Guid? actorId, DateTime? atUtc = null)
    {
        var phieu = await LayAsync(id);
        var truoc = phieu.Status;

        phieu.Duyet(actorType.ToString(), actorId);

        var don = await _don.GetAsync(phieu.OrderId);
        don.ChuyenKetThuc(ReturnStatus.ReturnApproved, actorType, actorId, "DUYET_TRA", occurredAtUtc: atUtc);
        await _don.UpdateAsync(don, autoSave: false);

        await GhiLichSuAsync(phieu, "Lifecycle", (int)truoc, (int)phieu.Status, actorType, actorId, "DUYET_TRA", null, atUtc ?? DateTime.UtcNow);

        return await LuuAsync(phieu);
    }

    public async Task<OrderReturn> TuChoiAsync(Guid id, string lyDo, StatusActorType actorType, Guid? actorId, DateTime? atUtc = null)
    {
        var phieu = await LayAsync(id);
        var truoc = phieu.Status;

        phieu.TuChoi(actorType.ToString(), actorId, lyDo);

        await TraLaiDaGiuAsync(phieu);
        await _phieuTra.UpdateAsync(phieu, autoSave: false);

        var don = await _don.GetAsync(phieu.OrderId);
        don.ChuyenKetThuc(ReturnStatus.ReturnRejected, actorType, actorId, "TU_CHOI_TRA", reasonText: lyDo, occurredAtUtc: atUtc);
        await _don.UpdateAsync(don, autoSave: false);

        await GhiLichSuAsync(phieu, "Lifecycle", (int)truoc, (int)phieu.Status, actorType, actorId, "TU_CHOI_TRA", lyDo, atUtc ?? DateTime.UtcNow);

        return await LuuAsync(phieu);
    }

    public async Task<OrderReturn> HuyAsync(Guid id, string lyDo, StatusActorType actorType, Guid? actorId, DateTime? atUtc = null)
    {
        var phieu = await LayAsync(id);
        var truoc = phieu.Status;

        phieu.Huy(actorType.ToString(), actorId, lyDo);

        await TraLaiDaGiuAsync(phieu);
        await _phieuTra.UpdateAsync(phieu, autoSave: false);

        // ⚠️ Đơn **không** đổi trục kết thúc ở đây: máy trạng thái `SAL-003` chưa có cạnh
        // `ReturnRequested → Open`. 📌 Phiếu huỷ nghĩa là *"yêu cầu này không hiệu lực"* — đơn vẫn có thể
        // tạo phiếu trả khác; ghi rõ ở `SAL-006` §29 để không ai tưởng đã đồng bộ.
        await GhiLichSuAsync(phieu, "Lifecycle", (int)truoc, (int)phieu.Status, actorType, actorId, "HUY_TRA", lyDo, atUtc ?? DateTime.UtcNow);

        return await LuuAsync(phieu);
    }

    public async Task<OrderReturn> NhanHangAsync(
        Guid id,
        IReadOnlyCollection<(Guid ReturnLineId, int ReceivedQuantity, ReturnDisposition Disposition)> ketQua,
        StatusActorType actorType,
        Guid? actorId,
        DateTime? atUtc = null)
    {
        var at = atUtc ?? DateTime.UtcNow;
        var phieu = await LayAsync(id);

        var dong = await _dongTra.GetListAsync(x => x.ReturnId == id);
        var theoId = dong.ToDictionary(x => x.Id);

        var capNhat = new List<(OrderReturnLine, int, ReturnDisposition)>();

        foreach (var (returnLineId, soLuong, xuLy) in ketQua)
        {
            if (!theoId.TryGetValue(returnLineId, out var d))
            {
                throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                    .WithData("lyDo", "Dòng trả KHÔNG thuộc phiếu này.").WithData("returnLineId", returnLineId);
            }

            capNhat.Add((d, soLuong, xuLy));
        }

        // 🚨 `S14F` — "nhận đủ" phải tính trên **TOÀN BỘ** dòng của phiếu, không phải tập con của lần gọi.
        // 📌 Số nhận hiệu lực của một dòng sau lần gọi này = số sắp ghi (khi > 0); `0` + `Unset` là **no-op**
        // nên giữ nguyên số đã có. Đây đúng là giá trị `GhiNhanHang` sẽ để lại trên dòng.
        var soNhanSau = dong.ToDictionary(x => x.Id, x => x.ReceivedQuantity);

        foreach (var (d, soLuong, _) in capNhat)
        {
            if (soLuong > 0)
            {
                soNhanSau[d.Id] = soLuong;
            }
        }

        var daNhanDuTatCaDong = dong.Count > 0
            && dong.All(x => soNhanSau[x.Id] >= x.Quantity);

        var truocLifecycle = phieu.Status;
        var truocGoods = phieu.GoodsStatus;

        phieu.NhanHang(actorType.ToString(), actorId, capNhat, daNhanDuTatCaDong, at);

        foreach (var (d, _, _) in capNhat)
        {
            await _dongTra.UpdateAsync(d, autoSave: false);
        }

        // Hàng đã về **đủ** ⇒ đơn sang `Returned`; mới về **một phần** ⇒ đơn sang `PartiallyReturned`
        // (qua máy trạng thái, **không** gán tay). 🆕 `S14H`.
        //
        // 🔒 **CHỈ CHUYỂN KHI TRẠNG THÁI ĐƠN THỰC SỰ ĐỔI** — `ChuyenDonAsync` tự bỏ qua khi đơn đã ở đúng
        // trạng thái đích. 📌 Nếu không bỏ qua: nhận thêm một phần nữa khi đơn đã `PartiallyReturned` sẽ gọi
        // cạnh tự thân (`from = to`) ⇒ máy trạng thái NÉM `0001` **dù nghiệp vụ hợp lệ**. Và "nhận một phần"
        // **không** được kéo đơn lùi/xuống trạng thái khác.
        if (phieu.Status == ReturnLifecycleStatus.Received)
        {
            await ChuyenDonAsync(phieu.OrderId, ReturnStatus.Returned, "DA_TRA", actorType, actorId, at);
        }
        else if (phieu.GoodsStatus == ReturnGoodsStatus.PartiallyReceived)
        {
            await ChuyenDonAsync(phieu.OrderId, ReturnStatus.PartiallyReturned, "NHAN_MOT_PHAN", actorType, actorId, at);
        }

        await _phieuTra.UpdateAsync(phieu, autoSave: false);

        await GhiLichSuAsync(phieu, "Goods", (int)truocGoods, (int)phieu.GoodsStatus, actorType, actorId, "NHAN_HANG", null, at);

        if (truocLifecycle != phieu.Status)
        {
            await GhiLichSuAsync(phieu, "Lifecycle", (int)truocLifecycle, (int)phieu.Status, actorType, actorId, "NHAN_HANG", null, at);
        }

        return await LuuAsync(phieu);
    }

    public async Task<OrderReturn> DongAsync(Guid id, StatusActorType actorType, Guid? actorId, DateTime? atUtc = null)
    {
        var phieu = await LayAsync(id);
        var truoc = phieu.Status;

        phieu.Dong(actorType.ToString(), actorId, atUtc ?? DateTime.UtcNow);
        await _phieuTra.UpdateAsync(phieu, autoSave: false);

        await GhiLichSuAsync(phieu, "Lifecycle", (int)truoc, (int)phieu.Status, actorType, actorId, "DONG_PHIEU", null, atUtc ?? DateTime.UtcNow);

        return await LuuAsync(phieu);
    }

    /// <summary>Ghi nhận **đã hoàn tiền** (thủ công) — `Q4`: chỉ ghi nhận, không gọi cổng.</summary>
    public async Task<OrderReturn> GhiNhanHoanTienAsync(Guid id, decimal soTien, StatusActorType actorType, Guid? actorId)
    {
        var phieu = await LayAsync(id);

        phieu.GhiNhanHoanTien(soTien, ReturnRefundMethod.Manual);
        await _phieuTra.UpdateAsync(phieu, autoSave: false);

        // 📌 Không ghi `OrdReturnHistory` cho tiền: `FromStatus`/`ToStatus` là **trạng thái**, không phải số tiền
        // ⇒ nhét số tiền vào đó làm cột trạng thái mang hai nghĩa (đúng lỗi đã tránh ở `S13`). Bản ghi tiền là
        // chính `RefundedAmount` + audit của ABP (`LastModificationTime`/`LastModifierId`).
        _ = actorType;
        _ = actorId;

        return await LuuAsync(phieu);
    }

    /// <summary>
    /// 🆕 **`S14` phần C** — gắn chứng từ **nhập kho hàng hoàn** cho các dòng `Restock` (sau khi gateway tạo+ghi sổ).
    /// 🔒 Kiểm **thuộc phiếu** trước khi gán: hai phiếu khác nhau không được trỏ vào cùng một dòng.
    /// </summary>
    public async Task GanChungTuKhoAsync(Guid returnId, Guid chungTuId, IReadOnlyCollection<Guid> returnLineIds)
    {
        var dong = await _dongTra.GetListAsync(x => x.ReturnId == returnId);
        var theoId = dong.ToDictionary(x => x.Id);

        foreach (var id in returnLineIds)
        {
            if (!theoId.TryGetValue(id, out var d))
            {
                throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
                    .WithData("lyDo", "Dòng trả KHÔNG thuộc phiếu này.").WithData("returnLineId", id);
            }

            d.GanChungTuKho(chungTuId);
            await _dongTra.UpdateAsync(d, autoSave: true);
        }
    }

    /// <summary>
    /// 🆕 **`S14H`** — đồng bộ **trục kết thúc của ĐƠN** với mức hàng hoàn đã nhận, **qua máy trạng thái**
    /// (`Order.ChuyenKetThuc`) — không gán `ReturnStatus` trực tiếp, không tự ghi `OrdStatusHistory`
    /// (aggregate đơn ghi lịch sử + phát ETO trong **cùng** transaction).
    ///
    /// 🔒 **KHÔNG chuyển khi trạng thái KHÔNG đổi:** đơn đã ở đúng trạng thái đích ⇒ trả về ngay. Hai lý do:
    /// ① máy trạng thái **không có cạnh tự thân** (`from = to` ⇒ NÉM `0001`);
    /// ② nhận thêm **một phần nữa** (đơn đã `PartiallyReturned`) là nghiệp vụ **hợp lệ** — không được biến
    /// nó thành lỗi. Đây là điểm khác với `DatTrangThaiThanhToan` (trả `false`) — ở đây là nội bộ, không lộ API.
    /// </summary>
    private async Task ChuyenDonAsync(
        Guid orderId,
        ReturnStatus moi,
        string reasonCode,
        StatusActorType actorType,
        Guid? actorId,
        DateTime at)
    {
        var don = await _don.GetAsync(orderId);

        if (don.ReturnStatus == moi)
        {
            return;
        }

        don.ChuyenKetThuc(moi, actorType, actorId, reasonCode, occurredAtUtc: at);

        await _don.UpdateAsync(don, autoSave: false);
    }

    private async Task<OrderReturn> LayAsync(Guid id)
        => await _phieuTra.FindAsync(id)
           ?? throw new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe)
               .WithData("lyDo", "Không tìm thấy phiếu trả.").WithData("returnId", id);

    private async Task<OrderReturn> LuuAsync(OrderReturn phieu)
    {
        await _phieuTra.UpdateAsync(phieu, autoSave: true);
        return phieu;
    }

    /// <summary>Trả lại phần **giữ chỗ trả** khi phiếu bị từ chối/huỷ (đối xứng với `TangTra`).</summary>
    private async Task TraLaiDaGiuAsync(OrderReturn phieu)
    {
        var dong = await _dongTra.GetListAsync(x => x.ReturnId == phieu.Id);

        foreach (var d in dong)
        {
            var dongDon = await _dongDon.FindAsync(d.OrderLineId);

            if (dongDon is null)
            {
                continue;
            }

            dongDon.GiamTra(d.Quantity);
            await _dongDon.UpdateAsync(dongDon, autoSave: false);
        }
    }

    private async Task GhiLichSuAsync(
        OrderReturn phieu, string axis, int tu, int den,
        StatusActorType actorType, Guid? actorId, string reasonCode, string? reasonText, DateTime atUtc)
    {
        if (tu == den)
        {
            return;   // 📌 Không có chuyển dịch ⇒ không ghi dòng vô nghĩa (`CHECK` cũng cấm `from = to`).
        }

        await _lichSu.InsertAsync(new OrderReturnHistory(
            GuidGenerator.Create(), phieu.Id, axis, tu, den, actorType.ToString(), actorId,
            reasonCode, reasonText, GuidGenerator.Create(), phieu.Version, atUtc), autoSave: false);
    }

    /// <summary>SHA-256 của payload **canonical** (đơn + các dòng đã sắp xếp) — phát hiện dùng lại khoá.</summary>
    private static string BamPayload(Guid orderId, IReadOnlyCollection<(Guid OrderLineId, int Quantity)> lines)
    {
        var noiDung = orderId + "|" + string.Join(";", lines
            .OrderBy(x => x.OrderLineId)
            .Select(x => $"{x.OrderLineId}:{x.Quantity}"));

        var bam = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(noiDung));
        return Convert.ToHexString(bam).ToLowerInvariant();
    }

    private static BusinessException Loi(string lyDo)
        => new BusinessException(SalesErrorCodes.PhieuTraKhongHopLe).WithData("lyDo", lyDo);
}

/// <summary>🆕 **`S14`** — sinh **mã phiếu trả** `RT-yyyyMMdd-N` (hàm **THUẦN**, test được không cần DB).</summary>
public static class SinhSoPhieuTra
{
    public static string Sinh(DateTime atUtc, int soThuTu)
        => $"RT-{atUtc:yyyyMMdd}-{Math.Max(1, soThuTu)}";
}


