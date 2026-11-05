using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Pos;

/// <summary>
/// 💵 **CA THU NGÂN** (`PosCashSessions`) — `WP-06` (`nhanh-pos-module-map.md` §5.3).
///
/// 🔑 **VÌ SAO LÀ ENTITY RIÊNG (không phải "một cờ trên hoá đơn"):** kết ca là **biên bản TIỀN MẶT**:
/// thu ngân đếm két, ghi **tiền bàn giao thực tế**, và hệ thống phải **chốt** số chênh lệch tại thời điểm đó.
/// Nếu chỉ suy từ hoá đơn thì: ① không biết **tiền đầu ca**; ② sửa hoá đơn cũ sẽ **làm đổi** báo cáo kết ca
/// **đã in và đã ký** — đúng kiểu sai lệch không truy được.
///
/// 🚨 **CÔNG THỨC (chép đúng Nhanh §5.3):**
/// <code>
/// tiền trong ca = doanh thu tiền mặt − chi phí khác − trả hàng bằng tiền mặt
/// tiền cuối ca  = tiền đầu ca + tiền trong ca
/// chênh lệch    = tiền bàn giao thực tế − tiền cuối ca
/// </code>
/// 🔒 `ExpectedCash`/`Variance` **lưu** tại lúc kết ca (số **đóng băng**) — không suy lại khi đọc, vì
/// hoá đơn của ca có thể bị huỷ/trả **sau** đó và báo cáo đã in phải giữ nguyên.
/// </summary>
public class CashSession : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiSoCaToiDa = 64;
    public const int DoDaiGhiChuToiDa = 1024;

    public Guid? TenantId { get; protected set; }

    /// <summary>Số ca (`POS-CA-yyyyMMdd-NN`) — in trên biên bản bàn giao.</summary>
    public string Number { get; protected set; } = default!;

    /// <summary>Quầy/kho của ca — một quầy chỉ có **một** ca mở (unique index có bộ lọc ở DB).</summary>
    public Guid WarehouseId { get; protected set; }

    /// <summary>Thu ngân giữ ca (`null` = phiên hệ thống/không xác định).</summary>
    public Guid? CashierId { get; protected set; }

    public CashSessionStatus Status { get; protected set; } = CashSessionStatus.Open;

    /// <summary>**Tiền đầu ca** — đếm mệnh giá hoặc nhập tổng (Nhanh cho cả hai cách).</summary>
    public decimal OpeningCash { get; protected set; }

    public DateTime OpenedAtUtc { get; protected set; }

    public DateTime? ClosedAtUtc { get; protected set; }

    public Guid? ClosedBy { get; protected set; }

    /// <summary>**Doanh thu TIỀN MẶT** của ca (Σ `TienMatThucThu` của hoá đơn — đã trừ tiền thối).</summary>
    public decimal CashRevenue { get; protected set; }

    /// <summary>**Trả hàng bằng tiền mặt** trong ca (phiếu trả có `RefundMethod = Cash`).</summary>
    public decimal CashRefund { get; protected set; }

    /// <summary>**Chi phí khác** bằng tiền mặt (điện nước, ship nội bộ…) — nhập lúc kết ca.</summary>
    public decimal OtherExpense { get; protected set; }

    /// <summary>**Tiền cuối ca theo sổ** = `OpeningCash + CashRevenue − OtherExpense − CashRefund`.</summary>
    public decimal ExpectedCash { get; protected set; }

    /// <summary>**Tiền bàn giao thực tế** (thu ngân đếm) — chỉ có sau khi kết ca.</summary>
    public decimal HandoverCash { get; protected set; }

    /// <summary>**Chênh lệch** = `HandoverCash − ExpectedCash`; âm = thiếu, dương = thừa.</summary>
    public decimal Variance { get; protected set; }

    public string? Note { get; set; }

    protected CashSession() { }

    public CashSession(Guid id, string number, Guid warehouseId, Guid? cashierId, decimal openingCash, DateTime openedAtUtc)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new BusinessException(PosErrorCodes.TienCaKhongHopLe)
                .WithData("truong", nameof(number))
                .WithData("lyDo", "Số ca là BẮT BUỘC.");
        }

        if (number.Trim().Length > DoDaiSoCaToiDa)
        {
            throw new BusinessException(PosErrorCodes.TienCaKhongHopLe)
                .WithData("truong", nameof(number)).WithData("toiDa", DoDaiSoCaToiDa);
        }

        if (warehouseId == Guid.Empty)
        {
            throw new BusinessException(PosErrorCodes.ThieuKho)
                .WithData("lyDo", "Ca phải gắn KHO/QUẦY (một quầy một ca).");
        }

        KiemTien(openingCash, nameof(openingCash));

        Number = number.Trim();
        WarehouseId = warehouseId;
        CashierId = cashierId;
        OpeningCash = Math.Round(openingCash, 2, MidpointRounding.AwayFromZero);
        OpenedAtUtc = openedAtUtc;
    }

    /// <summary>
    /// 🚨 **KẾT CA** — CHỈ MỘT LẦN (Nhanh: *"đã kết ca là không quay ngược được"*).
    /// 📌 Tham số `cashRevenue`/`cashRefund` do app service **cộng từ hoá đơn/phiếu trả của ca** — entity
    /// không đọc DB; nhờ vậy công thức vẫn **test được bằng tay** (không cần Postgres).
    /// </summary>
    public void KetCa(
        decimal cashRevenue,
        decimal cashRefund,
        decimal otherExpense,
        decimal handoverCash,
        Guid nguoiKet,
        DateTime atUtc,
        string? ghiChu = null)
    {
        if (Status == CashSessionStatus.Closed)
        {
            throw new BusinessException(PosErrorCodes.CaDaKetThuc)
                .WithData("number", Number)
                .WithData("lyDo", "Ca ĐÃ kết thúc — không kết ca lần hai (số đã bàn giao).");
        }

        KiemTien(cashRevenue, nameof(cashRevenue));
        KiemTien(cashRefund, nameof(cashRefund));
        KiemTien(otherExpense, nameof(otherExpense));
        KiemTien(handoverCash, nameof(handoverCash));

        if (ghiChu is { Length: > DoDaiGhiChuToiDa })
        {
            throw new BusinessException(PosErrorCodes.TienCaKhongHopLe)
                .WithData("truong", nameof(ghiChu)).WithData("toiDa", DoDaiGhiChuToiDa);
        }

        CashRevenue = Math.Round(cashRevenue, 2, MidpointRounding.AwayFromZero);
        CashRefund = Math.Round(cashRefund, 2, MidpointRounding.AwayFromZero);
        OtherExpense = Math.Round(otherExpense, 2, MidpointRounding.AwayFromZero);
        HandoverCash = Math.Round(handoverCash, 2, MidpointRounding.AwayFromZero);

        ExpectedCash = Math.Round(
            OpeningCash + CashRevenue - OtherExpense - CashRefund, 2, MidpointRounding.AwayFromZero);

        Variance = Math.Round(HandoverCash - ExpectedCash, 2, MidpointRounding.AwayFromZero);

        Status = CashSessionStatus.Closed;
        ClosedAtUtc = atUtc;
        ClosedBy = nguoiKet;
        Note = string.IsNullOrWhiteSpace(ghiChu) ? Note : ghiChu.Trim();
    }

    /// <summary>Ca còn mở và **của mình** — guard cho thao tác bán/huỷ.</summary>
    public bool DangMo => Status == CashSessionStatus.Open;

    /// <summary>Kết ca của người khác cần quyền quản lý ca (`0014`) — kiểm ở app service (có `CurrentUser`).</summary>
    public bool CuaThuNgan(Guid? nguoiDung) => nguoiDung is not null && CashierId == nguoiDung;

    private static void KiemTien(decimal soTien, string truong)
    {
        if (soTien < 0m)
        {
            throw new BusinessException(PosErrorCodes.TienCaKhongHopLe)
                .WithData("truong", truong)
                .WithData("soTien", soTien)
                .WithData("lyDo", "Tiền của ca KHÔNG được âm.");
        }
    }
}
