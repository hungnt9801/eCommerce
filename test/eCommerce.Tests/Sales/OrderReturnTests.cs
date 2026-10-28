using eCommerce.Orders;
using Volo.Abp;

namespace eCommerce.Tests.Sales;

/// <summary>
/// 🆕 **`S14` — PHIẾU TRẢ HÀNG (RMA)** (`SAL-006`): bất biến của entity + hai trục trạng thái.
///
/// 🚨 **LỖI IM LẶNG BỘ TEST NÀY CHẶN:** hoàn tiền/nhận hàng khi phiếu **chưa duyệt**; đóng phiếu khi
/// **hàng chưa về**; đánh dấu *"đã trả"* quá số đã mua (`OrderLine.ReturnedQuantity`); ghi lịch sử
/// **không đổi trạng thái** (dòng vô nghĩa làm timeline không đọc được).
/// </summary>
public class OrderReturnTests
{
    private static readonly Guid DonId = Guid.NewGuid();
    private static readonly Guid KhoId = Guid.NewGuid();

    private static OrderReturn Tao(string? reason = "HANG_LOI", string? idem = "K9S14-1")
        => new(
            Guid.NewGuid(), DonId, "RT-20261028-1", "web", KhoId, reason!,
            "Khách báo hàng lỗi", idem!, new string('a', 64), "Staff", Guid.NewGuid());

    private static OrderReturnLine Dong(OrderReturn phieu, int soLuong = 2)
        => new(Guid.NewGuid(), phieu.Id, Guid.NewGuid(), "SKU-1", soLuong, 100_000m, 10_000m, 9_000m);

    [Fact]
    public void Tao_hop_le__Requested_Awaiting_va_chua_co_gi()
    {
        var phieu = Tao();

        Assert.Equal(ReturnLifecycleStatus.Requested, phieu.Status);
        Assert.Equal(ReturnGoodsStatus.Awaiting, phieu.GoodsStatus);
        Assert.Equal(0, phieu.RefundedAmount);
        Assert.Equal(0m, phieu.RefundedAmount);
        Assert.Equal(0, phieu.Version);
        Assert.True(phieu.ConMo);
    }

    [Theory]
    [InlineData(null, "K9S14-1")]   // thiếu ReasonCode
    [InlineData("HANG_LOI", null)]  // thiếu IdempotencyKey
    public void Tao_thieu_du_lieu_bat_buoc__NEM_0010(string? reason, string? idem)
    {
        var loi = Assert.Throws<BusinessException>(() => Tao(reason, idem));

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe, loi.Code);
    }

    [Fact]
    public void Duyet__chi_tu_Requested__va_tang_Version()
    {
        var phieu = Tao();

        phieu.Duyet("Staff", Guid.NewGuid());
        Assert.Equal(ReturnLifecycleStatus.Approved, phieu.Status);
        Assert.Equal(1, phieu.Version);

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.Duyet("Staff", Guid.NewGuid())).Code);
    }

    [Fact]
    public void TuChoi__bat_buoc_ly_do()
    {
        var phieu = Tao();

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.TuChoi("Staff", Guid.NewGuid(), "   ")).Code);

        phieu.TuChoi("Staff", Guid.NewGuid(), "Không đủ điều kiện đổi trả");
        Assert.Equal(ReturnLifecycleStatus.Rejected, phieu.Status);
        Assert.False(phieu.ConMo);
    }

    [Fact]
    public void Huy__bi_chan_khi_DA_NHAN_HANG()
    {
        var phieu = Tao();
        phieu.Duyet("Staff", Guid.NewGuid());
        var dong = Dong(phieu);

        phieu.NhanHang("Staff", Guid.NewGuid(), [(dong, 2, ReturnDisposition.Restock)], DateTime.UtcNow);

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.Huy("Staff", Guid.NewGuid(), "Huỷ")).Code);
    }

    [Fact]
    public void NhanHang__chi_tu_Approved__va_hai_trang_thai_dung_dan()
    {
        var phieu = Tao();
        var dong1 = Dong(phieu);
        var dong2 = new OrderReturnLine(Guid.NewGuid(), phieu.Id, Guid.NewGuid(), "SKU-2", 3, 50_000m, 0m, 0m);

        // Chưa duyệt ⇒ chưa được nhận hàng.
        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() =>
                phieu.NhanHang("Staff", Guid.NewGuid(), [(dong1, 1, ReturnDisposition.Restock)], DateTime.UtcNow)).Code);

        phieu.Duyet("Staff", Guid.NewGuid());

        // Nhận một phần ⇒ PartiallyReceived, phiếu vẫn `Approved` (chưa phải `Received`).
        phieu.NhanHang("Staff", Guid.NewGuid(),
            [(dong1, 1, ReturnDisposition.Restock), (dong2, 0, ReturnDisposition.Damaged)], DateTime.UtcNow);

        Assert.Equal(ReturnGoodsStatus.PartiallyReceived, phieu.GoodsStatus);
        Assert.Equal(ReturnLifecycleStatus.Approved, phieu.Status);
        Assert.Null(phieu.ReceivedAtUtc);
    }

    [Fact]
    public void NhanHang__du_hang__Received_va_co_moc()
    {
        var phieu = Tao();
        phieu.Duyet("Staff", Guid.NewGuid());
        var dong = Dong(phieu);
        var luc = new DateTime(2026, 10, 28, 4, 0, 0, DateTimeKind.Utc);

        phieu.NhanHang("Staff", Guid.NewGuid(), [(dong, 2, ReturnDisposition.Restock)], luc);

        Assert.Equal(ReturnGoodsStatus.Received, phieu.GoodsStatus);
        Assert.Equal(ReturnLifecycleStatus.Received, phieu.Status);
        Assert.Equal(luc, phieu.ReceivedAtUtc);
    }

    [Fact]
    public void NhanHang__thieu_disposition__va_vuot_so_yeu_cau__NEM()
    {
        var phieu = Tao();
        phieu.Duyet("Staff", Guid.NewGuid());
        var dong = Dong(phieu, soLuong: 2);

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() =>
                phieu.NhanHang("Staff", Guid.NewGuid(), [(dong, 2, ReturnDisposition.Unset)], DateTime.UtcNow)).Code);

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() =>
                phieu.NhanHang("Staff", Guid.NewGuid(), [(dong, 3, ReturnDisposition.Restock)], DateTime.UtcNow)).Code);
    }

    [Fact]
    public void Dong__chi_khi_hang_da_ket_thuc()
    {
        var phieu = Tao();
        phieu.Duyet("Staff", Guid.NewGuid());

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.Dong("Staff", Guid.NewGuid(), DateTime.UtcNow)).Code);

        var dong = Dong(phieu);
        phieu.NhanHang("Staff", Guid.NewGuid(), [(dong, 2, ReturnDisposition.Restock)], DateTime.UtcNow);
        phieu.Dong("Staff", Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(ReturnLifecycleStatus.Closed, phieu.Status);
        Assert.NotNull(phieu.ClosedAtUtc);
    }

    [Fact]
    public void GhiNhanHoanTien__cong_don_va_chan_so_tien_khong_hop_le()
    {
        var phieu = Tao();

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() => phieu.GhiNhanHoanTien(0m, ReturnRefundMethod.Manual)).Code);

        phieu.GhiNhanHoanTien(150_000m, ReturnRefundMethod.Manual);
        phieu.GhiNhanHoanTien(10_000m, ReturnRefundMethod.Manual);

        Assert.Equal(160_000m, phieu.RefundedAmount);
        Assert.Equal(ReturnRefundMethod.Manual, phieu.RefundMethod);
    }

    [Fact]
    public void Dong_tra__tinh_tien_hoan_SAU_giam_gia_va_giu_thue_rieng()
    {
        var dong = new OrderReturnLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 2, 100_000m, 10_000m, 9_000m);

        Assert.Equal(180_000m, dong.RefundAmount);       // (100.000 − 10.000) × 2
        Assert.Equal(9_000m, dong.TaxAmountSnapshot);    // thuế giữ riêng để đảo hoá đơn
    }

    [Fact]
    public void Lich_su__khong_doi_trang_thai__NEM()
    {
        var loi = Assert.Throws<BusinessException>(() => new OrderReturnHistory(
            Guid.NewGuid(), Guid.NewGuid(), "Lifecycle", 1, 1, "Staff", Guid.NewGuid(),
            "DUYET", null, Guid.NewGuid(), 1, DateTime.UtcNow));

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe, loi.Code);
    }

    // ── Hàng rào `OrderLine.ReturnedQuantity` (ADR-11) ─────────────────────────────────────────

    [Fact]
    public void Dong_don__trả_vuot_so_da_mua__NEM_0011__va_giam_tra_doi_xung()
    {
        var dong = new OrderLine(Guid.NewGuid(), "SKU-1", 3, 100_000m);

        dong.TangTra(2);
        Assert.Equal(2, dong.ReturnedQuantity);

        var loi = Assert.Throws<BusinessException>(() => dong.TangTra(2));
        Assert.Equal(SalesErrorCodes.TraVuotSoDaMua, loi.Code);
        Assert.Equal(2, dong.ReturnedQuantity);

        dong.TangTra(1);                    // bằng đúng số đã mua ⇒ hợp lệ
        Assert.Equal(3, dong.ReturnedQuantity);

        dong.GiamTra(3);                    // huỷ phiếu ⇒ trả lại chỗ
        Assert.Equal(0, dong.ReturnedQuantity);

        Assert.Equal(SalesErrorCodes.PhieuTraKhongHopLe,
            Assert.Throws<BusinessException>(() => dong.GiamTra(1)).Code);
    }
}
