using eCommerce.Application.Contracts.Pos;
using eCommerce.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Application.Pos;

/// <summary>
/// **API CA THU NGÂN** (`WP-06`) — màn kết ca (`nhanh-pos-module-map.md` §5.3).
///
/// 🚨 **CÔNG THỨC KẾT CA NẰM Ở DOMAIN** (`CashSession.KetCa`); service này **chỉ cộng số từ chứng từ**:
/// `CashRevenue = Σ TienMatThucThu` của hoá đơn **không huỷ** trong ca;
/// `CashRefund  = Σ RefundAmount` của phiếu trả **hoàn tiền mặt** trong ca.
///
/// 📌 **VÌ SAO `TienMatThucThu` (đã trừ tiền thối) chứ không phải Σ tiền mặt:** khách đưa 500.000 cho hoá đơn
/// 450.000 ⇒ két có **450.000**; cộng đủ 500.000 thì **mọi ca đều báo thiếu** đúng bằng tiền thối.
///
/// 🔒 **Hai bất biến:** ① một quầy **một** ca mở (unique index có bộ lọc ở DB); ② kết ca **một lần**
/// (Nhanh: *"đã kết ca là không quay ngược được"*) — số `ExpectedCash`/`Variance` **đóng băng**.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Pos.Default)]
public class CashSessionAppService : ApplicationService, ICashSessionAppService
{
    private readonly IRepository<CashSession, Guid> _ca;
    private readonly IRepository<RetailBill, Guid> _hoaDon;
    private readonly IRepository<RetailBillPayment, Guid> _thanhToan;
    private readonly IRepository<RetailReturn, Guid> _traHang;

    public CashSessionAppService(
        IRepository<CashSession, Guid> ca,
        IRepository<RetailBill, Guid> hoaDon,
        IRepository<RetailBillPayment, Guid> thanhToan,
        IRepository<RetailReturn, Guid> traHang)
    {
        _ca = ca;
        _hoaDon = hoaDon;
        _thanhToan = thanhToan;
        _traHang = traHang;
    }

    /// <inheritdoc />
    public async Task<CashSessionDto?> GetCurrentAsync(Guid warehouseId)
    {
        var ca = await _ca.FirstOrDefaultAsync(
            x => x.WarehouseId == warehouseId && x.Status == CashSessionStatus.Open);

        return ca is null ? null : await ToDtoAsync(ca);
    }

    /// <inheritdoc />
    public async Task<CashSessionDto> GetAsync(Guid id)
    {
        var ca = await _ca.FindAsync(id)
            ?? throw new BusinessException(PosErrorCodes.KhongCoCaDangMo)
                .WithData("id", id)
                .WithData("lyDo", "Ca KHÔNG tồn tại.");

        return await ToDtoAsync(ca);
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<CashSessionDto>> GetListAsync(
        Guid? warehouseId, DateTime? fromUtc, DateTime? toUtc, int skipCount = 0, int maxResultCount = 50)
    {
        var q = await _ca.GetQueryableAsync();

        if (warehouseId is { } khoId)
        {
            q = q.Where(x => x.WarehouseId == khoId);
        }

        if (fromUtc is { } tu)
        {
            q = q.Where(x => x.OpenedAtUtc >= tu);
        }

        if (toUtc is { } den)
        {
            q = q.Where(x => x.OpenedAtUtc <= den);
        }

        var tong = await AsyncExecuter.LongCountAsync(q);

        var co = maxResultCount is < 1 or > 200 ? 50 : maxResultCount;
        var bo = skipCount < 0 ? 0 : skipCount;

        var trang = await AsyncExecuter.ToListAsync(
            q.OrderByDescending(x => x.OpenedAtUtc).ThenByDescending(x => x.Number).Skip(bo).Take(co));

        var ds = new List<CashSessionDto>();

        foreach (var ca in trang)
        {
            ds.Add(await ToDtoAsync(ca));
        }

        return new PagedResultDto<CashSessionDto>(tong, ds);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Pos.SessionManage)]
    public async Task<CashSessionDto> OpenAsync(OpenCashSessionDto input)
    {
        var dangMo = await _ca.FirstOrDefaultAsync(
            x => x.WarehouseId == input.WarehouseId && x.Status == CashSessionStatus.Open);

        if (dangMo is not null)
        {
            // 📌 Một quầy một ca: mở chồng ca thì tiền mặt của hai ca **không tách được** khi đếm két.
            throw new BusinessException(PosErrorCodes.DaCoCaDangMo)
                .WithData("number", dangMo.Number)
                .WithData("warehouseId", input.WarehouseId)
                .WithData("lyDo", "Quầy đang có CA MỞ — kết ca cũ trước khi mở ca mới.");
        }

        var at = Clock.Now;
        var tienTo = $"POS-CA-{at:yyyyMMdd}-";
        // ⚠️ Đếm bằng `AsyncExecuter.CountAsync(IQueryable)`: `IRepository<TEntity, TKey>` của ABP **không** có
        // overload `GetCountAsync(predicate)` — lambda sẽ bị hiểu là `CancellationToken` (lỗi `CS1660`, đã gặp
        // ở `PaymentAppService` §198). Ghi lại để phiên sau không sửa vòng vo.
        var truyVan = await _ca.GetQueryableAsync();
        var dem = await AsyncExecuter.CountAsync(truyVan.Where(x => x.Number.StartsWith(tienTo)));

        var ca = new CashSession(
            GuidGenerator.Create(),
            tienTo + (dem + 1).ToString("D3"),
            input.WarehouseId,
            CurrentUser.Id,
            input.OpeningCash,
            at);

        await _ca.InsertAsync(ca, autoSave: true);

        Logger.LogInformation(
            "POS: MỞ CA {Number} kho {Kho} · tiền đầu ca {TienDau}. [WP06-MO-CA]",
            ca.Number, ca.WarehouseId, ca.OpeningCash);

        return await ToDtoAsync(ca);
    }

    /// <inheritdoc />
    [Authorize(eCommerceConsts.Permissions.Pos.SessionManage)]
    public async Task<CashSessionDto> CloseAsync(Guid id, CloseCashSessionDto input)
    {
        var ca = await _ca.FindAsync(id)
            ?? throw new BusinessException(PosErrorCodes.KhongCoCaDangMo).WithData("id", id);

        var (doanhThuTienMat, traHangTienMat) = await TinhTienMatCuaCaAsync(ca.Id);

        // 🚨 `KetCa` ném `0010` nếu ca đã kết — chốt số **một lần**, đúng Nhanh ("đã kết ca là không quay ngược").
        ca.KetCa(
            doanhThuTienMat,
            traHangTienMat,
            input.OtherExpense,
            input.HandoverCash,
            CurrentUser.Id ?? Guid.Empty,
            Clock.Now,
            input.Note);

        await _ca.UpdateAsync(ca, autoSave: true);

        Logger.LogInformation(
            "POS: KẾT CA {Number} · doanh thu tiền mặt {DoanhThu} · trả hàng tiền mặt {TraHang} · " +
            "chi phí {ChiPhi} · tiền cuối ca {CuoiCa} · bàn giao {BanGiao} · CHÊNH LỆCH {Lech}. [WP06-KET-CA]",
            ca.Number, ca.CashRevenue, ca.CashRefund, ca.OtherExpense, ca.ExpectedCash, ca.HandoverCash, ca.Variance);

        return await ToDtoAsync(ca);
    }

    /// <summary>
    /// Cộng **doanh thu tiền mặt** + **trả hàng bằng tiền mặt** của ca từ chứng từ.
    /// 📌 Hoá đơn **đã huỷ** bị loại (tiền đã trả lại khách — `TienMatThucThu` cũng trả 0).
    /// </summary>
    private async Task<(decimal DoanhThu, decimal TraHang)> TinhTienMatCuaCaAsync(Guid caId)
    {
        var hoaDon = await _hoaDon.GetListAsync(x => x.CashSessionId == caId);

        if (hoaDon.Count > 0)
        {
            var ids = hoaDon.Select(x => x.Id).ToList();
            var thanhToan = await _thanhToan.GetListAsync(x => ids.Contains(x.RetailBillId));

            var nhom = thanhToan.GroupBy(x => x.RetailBillId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var hd in hoaDon)
            {
                hd.GanThanhToan(nhom.TryGetValue(hd.Id, out var ds) ? ds : []);
            }
        }

        var doanhThu = hoaDon.Sum(x => x.TienMatThucThu());

        var phieuTra = await _traHang.GetListAsync(
            x => x.CashSessionId == caId && x.Status == RetailReturnStatus.Posted);

        var traHang = phieuTra
            .Where(x => x.RefundMethod == RetailPaymentMethod.Cash)
            .Sum(x => x.RefundAmount);

        return (
            Math.Round(doanhThu, 2, MidpointRounding.AwayFromZero),
            Math.Round(traHang, 2, MidpointRounding.AwayFromZero));
    }

    private async Task<CashSessionDto> ToDtoAsync(CashSession ca)
    {
        // 📌 Đếm **sống** số hoá đơn của ca (không lưu cột): ca đang mở thì con số này **đổi liên tục**;
        //    lưu sẵn sẽ thành số **sai** ngay sau hoá đơn kế tiếp.
        var truyVan = await _hoaDon.GetQueryableAsync();
        var soHoaDon = await AsyncExecuter.CountAsync(truyVan.Where(x => x.CashSessionId == ca.Id));

        return new CashSessionDto
        {
            Id = ca.Id,
            Number = ca.Number,
            WarehouseId = ca.WarehouseId,
            CashierId = ca.CashierId,
            Status = ca.Status.ToString(),
            OpeningCash = ca.OpeningCash,
            OpenedAtUtc = ca.OpenedAtUtc,
            ClosedAtUtc = ca.ClosedAtUtc,
            ClosedBy = ca.ClosedBy,
            CashRevenue = ca.CashRevenue,
            CashRefund = ca.CashRefund,
            OtherExpense = ca.OtherExpense,
            ExpectedCash = ca.ExpectedCash,
            HandoverCash = ca.HandoverCash,
            Variance = ca.Variance,
            BillCount = (int)soHoaDon,
            Note = ca.Note,
            CreationTime = ca.CreationTime
        };
    }
}
