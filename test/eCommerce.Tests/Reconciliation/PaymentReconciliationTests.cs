using eCommerce.Reconciliation;
using Volo.Abp;

namespace eCommerce.Tests.Reconciliation;

/// <summary>
/// **MÁY TRẠNG THÁI ĐỐI SOÁT THANH TOÁN** (`REC-001` §3–§5, 27/10/2026):
/// Run/Line/Discrepancy **phải NÉM** khi chuyển sai — đây là bằng chứng tài chính, không được ghi đè im lặng.
/// </summary>
public class PaymentReconciliationTests
{
    private static PaymentReconciliationRun LuotMoi() => new(
        Guid.NewGuid(),
        DateTime.UtcNow.AddHours(-1),
        DateTime.UtcNow,
        PaymentReconciliationTrigger.Schedule);

    [Fact]
    public void Run_KetThuc_khong_lech__HoanTat()
    {
        var run = LuotMoi();

        run.KetThuc(lineTotal: 3, lineMatched: 3, lineSkipped: 0, discrepancyCount: 0);

        Assert.Equal(PaymentReconciliationRunStatus.HoanTat, run.Status);
        Assert.NotNull(run.FinishedAtUtc);
        Assert.Equal(3, run.LineTotal);
    }

    [Fact]
    public void Run_KetThuc_co_lech__HoanTatCoLech__khong_do_nguoi_goi_chon()
    {
        var run = LuotMoi();

        run.KetThuc(3, 2, 0, 1);

        Assert.Equal(PaymentReconciliationRunStatus.HoanTatCoLech, run.Status);
        Assert.Equal(1, run.DiscrepancyCount);
    }

    [Fact]
    public void Run_ket_thuc_HAI_LAN__nem_0007()
    {
        var run = LuotMoi();
        run.KetThuc(1, 1, 0, 0);

        var loi = Assert.Throws<BusinessException>(() => run.KetThuc(1, 1, 0, 0));

        Assert.Equal(ReconErrorCodes.LuotDaDong, loi.Code);
    }

    [Fact]
    public void Run_ThatBai_roi_KetThuc__nem_0007()
    {
        var run = LuotMoi();
        run.ThatBai("ProviderUnavailable");

        Assert.Equal(PaymentReconciliationRunStatus.ThatBai, run.Status);
        Assert.Throws<BusinessException>(() => run.KetThuc(1, 0, 0, 1));
    }

    [Fact]
    public void Run_PeriodFrom_khong_nho_hon_PeriodTo__nem()
    {
        Assert.Throws<BusinessException>(() => new PaymentReconciliationRun(
            Guid.NewGuid(), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), PaymentReconciliationTrigger.Manual));
    }

    private static PaymentReconciliationLine DongMoi() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "vnpay", "TXN-1", "REQ-1", localStatus: 1, localAmountMinorUnits: 100_000);

    [Fact]
    public void Line_ghi_phan_hoi_roi_Khop__trang_thai_cuoi()
    {
        var line = DongMoi();

        line.GhiPhanHoiCong("00", "00", 100_000, "VNP-1", DateTime.UtcNow, "20261027120000", "01");
        line.Khop();

        Assert.Equal(PaymentReconciliationLineStatus.Khop, line.Status);
        Assert.True(line.DaKetThuc);
        Assert.Equal(1, line.AttemptCount);
    }

    [Fact]
    public void Line_da_ket_thuc__chuyen_tiep__nem_0008()
    {
        var line = DongMoi();
        line.Khop();

        var loi = Assert.Throws<BusinessException>(() => line.Lech(Guid.NewGuid()));

        Assert.Equal(ReconErrorCodes.DongDaKetThuc, loi.Code);
    }

    [Fact]
    public void Line_BoQua_khong_ly_do__nem_0010()
    {
        var line = DongMoi();

        var loi = Assert.Throws<BusinessException>(() => line.BoQua("  "));

        Assert.Equal(ReconErrorCodes.LyDoBoQuaKhongHopLe, loi.Code);
    }

    private static PaymentDiscrepancy LechMoi() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), orderId: Guid.NewGuid(),
        "vnpay", "TXN-1", PaymentDiscrepancyKind.AmountMismatch, PaymentDiscrepancySeverity.Cao,
        localAmountMinorUnits: 100_000, providerAmountMinorUnits: 90_000);

    [Fact]
    public void Discrepancy_XuLy_ghi_chu_ngan__nem_0009()
    {
        var lech = LechMoi();

        var loi = Assert.Throws<BusinessException>(() => lech.XuLy(Guid.NewGuid(), "ngắn"));

        Assert.Equal(ReconErrorCodes.LyDoXuLyNgan, loi.Code);
    }

    [Fact]
    public void Discrepancy_XuLy_du_ghi_chu__DaXuLy()
    {
        var lech = LechMoi();

        lech.XuLy(Guid.NewGuid(), "Đã kiểm tra với cổng, khớp lại số tiền.");

        Assert.Equal(PaymentDiscrepancyStatus.DaXuLy, lech.Status);
        Assert.NotNull(lech.ResolvedAtUtc);
    }

    [Fact]
    public void Discrepancy_BoQua_ly_do_ngoai_danh_sach__nem_0010()
    {
        var lech = LechMoi();

        var loi = Assert.Throws<BusinessException>(() => lech.BoQua("tự nghĩ ra"));

        Assert.Equal(ReconErrorCodes.LyDoBoQuaKhongHopLe, loi.Code);
    }

    [Fact]
    public void Discrepancy_BoQua_dung_danh_sach__BoQua()
    {
        var lech = LechMoi();

        lech.BoQua("TestTransaction");

        Assert.Equal(PaymentDiscrepancyStatus.BoQua, lech.Status);
    }
}
