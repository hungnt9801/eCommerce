using eCommerce.Application.Contracts.Pos;
using eCommerce.Inventory;
using eCommerce.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Pos;

/// <summary>
/// **API TRẢ HÀNG** (`WP-06`) — màn #14 `pos/return`, đặc tả `nhanh-pos-module-map.md` §5.4.
///
/// 🚨 **THỨ TỰ TIỀN (KHÔNG ĐƯỢC ĐẢO):** `tiền hàng trả − phí trả hàng` ⇒ **trừ công nợ trước** ⇒
/// còn lại mới **hoàn tiền**. 📌 Hoàn tiền trước sẽ **đưa tiền ra** cho khách **đang còn nợ** — cùng một
/// khách, hai chiều tiền, và không ai đối chiếu được.
///
/// 🔒 **BA việc trong MỘT transaction** (ABP UoW): ① nhập lại kho (`CustomerReturn`, tăng `OnHand`);
/// ② tăng `ReturnedQuantity` trên dòng hoá đơn (chặn trả vượt — DB có `CHECK`); ③ ghi tiền (giảm nợ/hoàn tiền).
/// Thiếu vế nào thì **sổ sách và kho nói khác nhau**.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Pos.Default)]
public class RetailReturnAppService : ApplicationService, IRetailReturnAppService
{
    private readonly IRepository<RetailReturn, Guid> _phieuTra;
    private readonly IRepository<RetailReturnLine, Guid> _dongTra;
    private readonly IRepository<RetailBill, Guid> _hoaDon;
    private readonly IRepository<RetailBillLine, Guid> _dongHoaDon;
    private readonly IRepository<CashSession, Guid> _ca;
    private readonly IPosStockGateway _kho;

    /// <summary>🆕 `WP-07` — sổ điểm/công nợ của khách (cùng UoW với phiếu trả).</summary>
    private readonly eCommerce.Customers.ICustomerLedger _soKhach;

    public RetailReturnAppService(
        IRepository<RetailReturn, Guid> phieuTra,
        IRepository<RetailReturnLine, Guid> dongTra,
        IRepository<RetailBill, Guid> hoaDon,
        IRepository<RetailBillLine, Guid> dongHoaDon,
        IRepository<CashSession, Guid> ca,
        IPosStockGateway kho,
        eCommerce.Customers.ICustomerLedger soKhach)
    {
        _phieuTra = phieuTra;
        _dongTra = dongTra;
        _hoaDon = hoaDon;
        _dongHoaDon = dongHoaDon;
        _ca = ca;
        _kho = kho;
        _soKhach = soKhach;
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Pos.Return)]
    public async Task<RetailReturnDto> CreateAsync(CreateRetailReturnDto input)
    {
        var hoaDon = await _hoaDon.FindAsync(input.RetailBillId)
            ?? throw new BusinessException(PosErrorCodes.KhongTimThayHoaDon)
                .WithData("id", input.RetailBillId);

        if (hoaDon.Status == RetailBillStatus.Voided)
        {
            throw new BusinessException(PosErrorCodes.HoaDonGocDaHuy)
                .WithData("number", hoaDon.Number)
                .WithData("lyDo", "Hoá đơn gốc ĐÃ HUỶ ⇒ không tạo được phiếu trả (hàng đã nhập lại khi huỷ).");
        }

        var dongHoaDon = await _dongHoaDon.GetListAsync(x => x.RetailBillId == hoaDon.Id);

        // 📌 Ca ghi nhận **trả tiền mặt vào ca đang mở**; hoá đơn của ca đã kết vẫn được trả (Nhanh cho phép),
        //    nhưng khoản hoàn **không** tính vào ca đã kết (số đã bàn giao) ⇒ `CashSessionId = null` khi không
        //    có ca mở. Ghi rõ để `ExpectedCash` của ca mới không bị trừ oan.
        var caDangMo = await _ca.FirstOrDefaultAsync(
            x => x.WarehouseId == hoaDon.WarehouseId && x.Status == CashSessionStatus.Open);

        var at = Clock.Now;
        var tienTo = $"PT-{at:yyyyMMdd}-";
        // ⚠️ `GetCountAsync(predicate)` KHÔNG tồn tại trên `IRepository<TEntity, TKey>` (CS1660) — xem
        // `PaymentAppService` §198.
        var truyVanPhieu = await _phieuTra.GetQueryableAsync();
        var dem = await AsyncExecuter.CountAsync(truyVanPhieu.Where(x => x.Number.StartsWith(tienTo)));

        var phieu = new RetailReturn(
            GuidGenerator.Create(),
            tienTo + (dem + 1).ToString("D4"),
            hoaDon.Id,
            hoaDon.WarehouseId,
            caDangMo?.Id,
            hoaDon.CustomerId);

        var dongTra = new List<RetailReturnLine>();

        // 📌 Gom theo SKU: một SKU có thể nằm trên **nhiều dòng** hoá đơn (bán 2 lần cùng món) ⇒ cộng dồn
        //    đúng dòng gốc, nếu không sẽ báo "vượt số đã mua" sai.
        foreach (var yeuCau in input.Lines)
        {
            var conLai = yeuCau.Quantity;
            var ungVien = dongHoaDon
                .Where(x => string.Equals(x.Sku, yeuCau.Sku?.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.CreationTime)
                .ToList();

            if (ungVien.Count == 0 || conLai <= 0m)
            {
                throw new BusinessException(PosErrorCodes.PhieuTraThieuDong)
                    .WithData("sku", yeuCau.Sku)
                    .WithData("lyDo", "SKU không thuộc hoá đơn gốc, hoặc số lượng trả ≤ 0.");
            }

            foreach (var dongGoc in ungVien)
            {
                if (conLai <= 0m)
                {
                    break;
                }

                var conTraDuoc = dongGoc.Quantity - dongGoc.ReturnedQuantity;

                if (conTraDuoc <= 0m)
                {
                    continue;
                }

                var lay = Math.Min(conLai, conTraDuoc);

                dongGoc.CongTra(lay);
                conLai -= lay;

                dongTra.Add(new RetailReturnLine(
                    GuidGenerator.Create(), phieu.Id, dongGoc.Sku, lay, dongGoc.UnitPrice));
            }

            if (conLai > 0m)
            {
                throw new BusinessException(PosErrorCodes.TraVuotSoDaMua)
                    .WithData("sku", yeuCau.Sku)
                    .WithData("conLaiChuaTra", conLai)
                    .WithData("lyDo", "Tổng số trả VƯỢT số đã mua (đã trừ các lần trả trước).");
            }
        }

        phieu.GanDong(dongTra);

        phieu.TinhTien(
            input.ReturnFeeAmount,
            hoaDon.DebtAmount,
            input.RefundMethod,
            input.PointsReversed,
            input.Note);

        // ══════════════════════════════════════════════════════════════════════════════════════════
        // GHI: phiếu → dòng → NHẬP KHO → giảm nợ hoá đơn → lưu dòng hoá đơn (ReturnedQuantity)
        // 📌 Nhập kho **trước** khi lưu phiếu là không cần thiết (cùng transaction), nhưng đặt sau khi
        //    `TinhTien` để nếu tiền sai (`0020`) thì **không** tạo chứng từ kho rồi rollback.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        await _phieuTra.InsertAsync(phieu, autoSave: true);
        await _dongTra.InsertManyAsync(dongTra, autoSave: true);

        var chungTuId = await _kho.NhapHangTraAsync(
            hoaDon.WarehouseId,
            $"NK-{phieu.Number}",
            hoaDon.Number,
            dongTra.Select(x => new PosStockLine(x.Sku, x.Quantity, x.UnitPriceSnapshot)).ToList(),
            at,
            note: $"Nhập lại kho hàng khách trả — phiếu {phieu.Number} (hoá đơn {hoaDon.Number}).");

        phieu.GanChungTuKho(chungTuId);

        if (phieu.DebtReductionAmount > 0m)
        {
            hoaDon.GiamNo(phieu.DebtReductionAmount);
        }

        await _phieuTra.UpdateAsync(phieu, autoSave: true);
        await _hoaDon.UpdateAsync(hoaDon, autoSave: true);
        await _dongHoaDon.UpdateManyAsync(dongHoaDon, autoSave: true);

        // 🆕 `WP-07` — ghi **sổ khách**: trừ tiền tích luỹ (hàng trả), thu hồi điểm đã tích của phần trả,
        // và giảm công nợ ĐÚNG phần `DebtReductionAmount` đã giảm trên hoá đơn ⇐ hai bên là **một** sự kiện.
        // 📌 Đặt sau khi hoá đơn đã giảm nợ để nếu sổ khách ném thì cả hai cùng rollback (cùng UoW).
        await _soKhach.GhiNhanTraHangAsync(
            phieu.CustomerId,
            tienHangTra: phieu.GoodsAmount,
            diemThuHoi: phieu.PointsReversed,
            noGiam: phieu.DebtReductionAmount,
            nguon: $"POS-TRA:{phieu.Number}");

        Logger.LogInformation(
            "POS: TRẢ HÀNG {Number} cho hoá đơn {HoaDon} · hàng trả {Hang} · phí {Phi} · giảm nợ {GiamNo} · " +
            "hoàn tiền {Hoan} · chứng từ kho {ChungTu}. [WP06-TRA-HANG]",
            phieu.Number, hoaDon.Number, phieu.GoodsAmount, phieu.ReturnFeeAmount,
            phieu.DebtReductionAmount, phieu.RefundAmount, chungTuId);

        return ToDto(phieu, hoaDon.Number);
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<RetailReturnDto>> GetListAsync(RetailReturnListInput input)
    {
        var q = await _phieuTra.GetQueryableAsync();

        if (input.RetailBillId is { } hoaDonId)
        {
            q = q.Where(x => x.RetailBillId == hoaDonId);
        }

        if (input.WarehouseId is { } khoId)
        {
            q = q.Where(x => x.WarehouseId == khoId);
        }

        if (input.FromUtc is { } tu)
        {
            q = q.Where(x => x.CreationTime >= tu);
        }

        if (input.ToUtc is { } den)
        {
            q = q.Where(x => x.CreationTime <= den);
        }

        var tong = await AsyncExecuter.LongCountAsync(q);

        var co = input.MaxResultCount is < 1 or > 200 ? 50 : input.MaxResultCount;
        var bo = input.SkipCount < 0 ? 0 : input.SkipCount;

        var trang = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Number).Skip(bo).Take(co));

        var ds = new List<RetailReturnDto>();

        foreach (var phieu in trang)
        {
            phieu.GanDong(await _dongTra.GetListAsync(x => x.RetailReturnId == phieu.Id));

            var soHoaDon = (await _hoaDon.FindAsync(phieu.RetailBillId))?.Number ?? phieu.RetailBillId.ToString();

            ds.Add(ToDto(phieu, soHoaDon));
        }

        return new PagedResultDto<RetailReturnDto>(tong, ds);
    }

    /// <inheritdoc />
    public async Task<RetailReturnDto> GetAsync(Guid id)
    {
        var phieu = await _phieuTra.FindAsync(id)
            ?? throw new BusinessException(PosErrorCodes.KhongTimThayHoaDon)
                .WithData("id", id)
                .WithData("lyDo", "Phiếu trả KHÔNG tồn tại.");

        phieu.GanDong(await _dongTra.GetListAsync(x => x.RetailReturnId == id));

        var soHoaDon = (await _hoaDon.FindAsync(phieu.RetailBillId))?.Number ?? phieu.RetailBillId.ToString();

        return ToDto(phieu, soHoaDon);
    }

    private static RetailReturnDto ToDto(RetailReturn x, string soHoaDon) => new()
    {
        Id = x.Id,
        Number = x.Number,
        RetailBillId = x.RetailBillId,
        RetailBillNumber = soHoaDon,
        WarehouseId = x.WarehouseId,
        CashSessionId = x.CashSessionId,
        CustomerId = x.CustomerId,
        Status = x.Status.ToString(),
        GoodsAmount = x.GoodsAmount,
        ReturnFeeAmount = x.ReturnFeeAmount,
        DebtReductionAmount = x.DebtReductionAmount,
        RefundAmount = x.RefundAmount,
        RefundMethod = x.RefundMethod.ToString(),
        PointsReversed = x.PointsReversed,
        Note = x.Note,
        StockDocumentId = x.StockDocumentId,
        CreationTime = x.CreationTime,
        Lines = x.Lines.Select(d => new RetailReturnLineDto
        {
            Id = d.Id,
            Sku = d.Sku,
            Quantity = d.Quantity,
            UnitPriceSnapshot = d.UnitPriceSnapshot,
            RefundAmount = d.RefundAmount
        }).ToList()
    };
}
