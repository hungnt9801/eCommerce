using eCommerce.Pos;
using Volo.Abp;

namespace eCommerce.Tests.Pos;

/// <summary>
/// 🚨 **`WP-06` — BẤT BIẾN CỦA QUẦY BÁN LẺ** (hoá đơn · ca thu ngân · trả hàng).
///
/// 📌 Mỗi ca dưới đây khoá **một luật đo được từ tài liệu Nhanh** (`nhanh-pos-module-map.md` §5.1–§5.4).
/// 🔑 Vì sao test **hàm thuần + entity** (không cần Postgres): công thức tiền là chỗ sai **im lặng** nhất —
/// `ExpectedCash` lệch 50.000 thì **không có lỗi nào**, chỉ có thu ngân bị quy trách nhiệm oan.
/// </summary>
public class Wp06PosBanLeTests
{
    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // ① TIỀN CỦA MỘT DÒNG
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2, 50_000, 0, 100_000)]
    [InlineData(3, 25_000, 5_000, 70_000)]   // 75.000 − 5.000
    [InlineData(1.5, 20_000, 0, 30_000)]     // hàng cân đo: 1,5 kg
    public void ThanhTienDong_tinh_dung(decimal soLuong, decimal donGia, decimal chietKhau, decimal mongDoi)
    {
        Assert.Equal(mongDoi, TinhTienHoaDon.ThanhTienDong(soLuong, donGia, chietKhau));
    }

    [Fact]
    public void ThanhTienDong__SoLuong_KHONG_DUONG__nem_0002()
    {
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.ThanhTienDong(0, 10_000));

        Assert.Equal(PosErrorCodes.SoLuongKhongHopLe, loi.Code);
    }

    [Fact]
    public void ThanhTienDong__DonGia_AM__nem_0003()
    {
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.ThanhTienDong(1, -1));

        Assert.Equal(PosErrorCodes.TienKhongHopLe, loi.Code);
    }

    [Fact]
    public void ThanhTienDong__ChietKhau_VUOT_thanh_tien__nem_0003()
    {
        // 🚨 Chiết khấu 60.000 cho dòng 1 × 50.000 ⇒ thành tiền ÂM. Cho lọt là **bán âm tiền**.
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.ThanhTienDong(1, 50_000, 60_000));

        Assert.Equal(PosErrorCodes.TienKhongHopLe, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // ② TOÀN HOÁ ĐƠN — VAT + chiết khấu + điểm
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Tinh__3_SP_chia_2_phuong_thuc__tong_dung()
    {
        // Hoá đơn mẫu của DoD: 3 SP (50.000 + 30.000 × 2), VAT 8% chỉ ở dòng đầu.
        var ketQua = TinhTienHoaDon.Tinh(
        [
            new TinhTienHoaDon.DongVao(1, 50_000, VatPercent: 8),
            new TinhTienHoaDon.DongVao(2, 30_000)
        ]);

        Assert.Equal(110_000m, ketQua.TienHang);
        Assert.Equal(4_000m, ketQua.TienVat);       // 50.000 × 8%
        Assert.Equal(114_000m, ketQua.TongThanhToan);
        Assert.Equal(0m, ketQua.ChietKhauDong);
    }

    [Fact]
    public void Tinh__CHIET_KHAU_HAI_TANG__nem_0004()
    {
        // 🚨 Luật Nhanh §5.1: chiết khấu theo SP **HOẶC** theo tổng — KHÔNG cả hai.
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.Tinh(
            [new TinhTienHoaDon.DongVao(1, 50_000, ChietKhauDong: 5_000)],
            chietKhauHoaDon: 10_000));

        Assert.Equal(PosErrorCodes.ChietKhauHaiTang, loi.Code);
    }

    [Fact]
    public void Tinh__ChietKhau_HoaDon_VUOT_tien_hang__nem_0005()
    {
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.Tinh(
            [new TinhTienHoaDon.DongVao(1, 50_000)],
            chietKhauHoaDon: 60_000));

        Assert.Equal(PosErrorCodes.ChietKhauVuotTienHang, loi.Code);
    }

    [Fact]
    public void Tinh__Diem_VUOT_so_phai_tra_con_lai__nem_0015()
    {
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.Tinh(
            [new TinhTienHoaDon.DongVao(1, 50_000)],
            chietKhauHoaDon: 10_000,
            tienDiem: 45_000)); // còn 40.000

        Assert.Equal(PosErrorCodes.DiemKhongHopLe, loi.Code);
    }

    [Fact]
    public void Tinh__hoa_don_RONG__nem_0001()
    {
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.Tinh([]));

        Assert.Equal(PosErrorCodes.HoaDonThieuDong, loi.Code);
    }

    [Fact]
    public void QuyDoiDiem__1_diem_1000d()
    {
        Assert.Equal(5_000m, TinhTienHoaDon.QuyDoiDiem(5));
    }

    [Fact]
    public void QuyDoiDiem__ty_le_0__nem_0015()
    {
        // 📌 Tỉ lệ 0 biến *"tiêu 100 điểm"* thành **miễn phí 0 đồng** — im lặng và sai.
        var loi = Assert.Throws<BusinessException>(() => TinhTienHoaDon.QuyDoiDiem(100, 0m));

        Assert.Equal(PosErrorCodes.DiemKhongHopLe, loi.Code);
    }

    [Theory]
    [InlineData(100_000, 100_000, 0, 0)]      // trả đủ
    [InlineData(100_000, 120_000, 20_000, 0)] // thừa 20.000
    [InlineData(100_000, 70_000, 0, 30_000)]  // nợ 30.000
    public void ChiaPhanDaTra__khong_bao_gio_vua_thua_vua_no(
        decimal phaiTra, decimal daTra, decimal thua, decimal no)
    {
        var (tienThua, conNo) = TinhTienHoaDon.ChiaPhanDaTra(phaiTra, daTra);

        Assert.Equal(thua, tienThua);
        Assert.Equal(no, conNo);
        Assert.False(tienThua > 0 && conNo > 0);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // ③ HOÁ ĐƠN — luật công nợ + tiền mặt thực thu
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    private static RetailBill HoaDon(Guid? khach = null, Guid? ca = null)
        => new(
            Guid.NewGuid(), $"POS-20261104-{Random.Shared.Next(1, 9999):D4}", Guid.NewGuid(),
            Guid.NewGuid(), ca ?? Guid.NewGuid(), khach);

    private static RetailBillLine Dong(RetailBill bill, decimal soLuong, decimal donGia, decimal vat = 0m)
        => new(Guid.NewGuid(), bill.Id, "SKU-1", soLuong, donGia, 0m, vat);

    [Fact]
    public void HoanTat__tra_du__PaidAmount_bang_TongThanhToan()
    {
        var bill = HoaDon();

        bill.HoanTat(
            [Dong(bill, 2, 30_000)],
            [
                new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 40_000),
                new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.BankTransfer, 20_000)
            ],
            tienKhachDua: 100_000);

        Assert.Equal(60_000m, bill.TotalPayment);
        Assert.Equal(60_000m, bill.PaidAmount);
        Assert.Equal(0m, bill.DebtAmount);
        Assert.Equal(0m, bill.ChangeAmount);
        Assert.Equal(RetailBillStatus.Completed, bill.Status);
    }

    [Theory]
    [InlineData(1, 50_000, 2, 50_000)] // 50.000 × 2 phương thức
    [InlineData(3, 20_000, 1, 60_000)]
    public void HoanTat__chia_nhieu_phuong_thuc__tong_dung(
        decimal sl, decimal gia, int soDong, decimal mongDoiPhaiTra)
    {
        var bill = HoaDon();
        var dong = Dong(bill, sl, gia);

        var thanhToan = Enumerable.Range(0, soDong)
            .Select(_ => new RetailBillPayment(
                Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, mongDoiPhaiTra / soDong))
            .ToList();

        bill.HoanTat([dong], thanhToan);

        Assert.Equal(mongDoiPhaiTra, bill.TotalPayment);
        Assert.Equal(mongDoiPhaiTra, bill.PaidAmount);
    }

    [Fact]
    public void HoanTat__tra_THUA__tinh_tien_thoi_va_TienMatThucThu_tru_tien_thoi()
    {
        var bill = HoaDon();

        bill.HoanTat(
            [Dong(bill, 1, 450_000)],
            [new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 500_000)],
            tienKhachDua: 500_000);

        Assert.Equal(450_000m, bill.TotalPayment);
        Assert.Equal(50_000m, bill.ChangeAmount);

        // 🚨 Két chỉ có 450.000 — cộng đủ 500.000 thì MỌI ca đều báo thiếu đúng 50.000.
        Assert.Equal(450_000m, bill.TienMatThucThu());
    }

    [Fact]
    public void HoanTat__CON_NO_ma_THIEU_KHACH__nem_0006()
    {
        var bill = HoaDon(khach: null);

        var loi = Assert.Throws<BusinessException>(() => bill.HoanTat(
            [Dong(bill, 1, 100_000)],
            [new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 60_000)],
            hanThanhToan: DateTime.UtcNow.AddDays(7)));

        Assert.Equal(PosErrorCodes.ThieuThongTinCongNo, loi.Code);
    }

    [Fact]
    public void HoanTat__CON_NO_ma_THIEU_HAN__nem_0006()
    {
        var bill = HoaDon(khach: Guid.NewGuid());

        var loi = Assert.Throws<BusinessException>(() => bill.HoanTat(
            [Dong(bill, 1, 100_000)],
            [new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 60_000)]));

        Assert.Equal(PosErrorCodes.ThieuThongTinCongNo, loi.Code);
    }

    [Fact]
    public void HoanTat__CON_NO_co_KHACH_va_HAN__luu_dung()
    {
        var bill = HoaDon(khach: Guid.NewGuid());
        var han = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

        bill.HoanTat(
            [Dong(bill, 1, 100_000)],
            [new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 60_000)],
            hanThanhToan: han);

        Assert.Equal(40_000m, bill.DebtAmount);
        Assert.Equal(han, bill.DueDateUtc);
    }

    [Fact]
    public void TaoHoaDon__ClientBillId_RONG__bi_tu_choi()
    {
        // 🚨 Khoá rỗng làm MỌI hoá đơn offline trùng nhau ⇒ chỉ bán được một hoá đơn.
        var loi = Assert.Throws<BusinessException>(() => new RetailBill(
            Guid.NewGuid(), "POS-20261104-0001", Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(PosErrorCodes.TrungKhoaKhacNoiDung, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // ④ HUỶ — idempotent + ca đã kết
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Huy__lan_2_tra_false__khong_dao_lan_hai()
    {
        var bill = HoaDon();
        bill.HoanTat([Dong(bill, 1, 100_000)], [new RetailBillPayment(
            Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 100_000)]);

        Assert.True(bill.Huy(Guid.NewGuid(), "khách đổi ý", DateTime.UtcNow, caDaKetThuc: false));
        Assert.False(bill.Huy(Guid.NewGuid(), "huỷ lần hai", DateTime.UtcNow, caDaKetThuc: false));

        Assert.Equal(RetailBillStatus.Voided, bill.Status);
    }

    [Fact]
    public void Huy__CA_DA_KET__nem_0010()
    {
        var bill = HoaDon();
        bill.HoanTat([Dong(bill, 1, 100_000)], [new RetailBillPayment(
            Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 100_000)]);

        var loi = Assert.Throws<BusinessException>(() =>
            bill.Huy(Guid.NewGuid(), "huỷ sau khi kết ca", DateTime.UtcNow, caDaKetThuc: true));

        Assert.Equal(PosErrorCodes.CaDaKetThuc, loi.Code);
        Assert.Equal(RetailBillStatus.Completed, bill.Status); // KHÔNG đổi trạng thái
    }

    [Fact]
    public void Huy__hoa_don_da_huy__TienMatThucThu_bang_0()
    {
        var bill = HoaDon();
        bill.HoanTat([Dong(bill, 1, 100_000)], [new RetailBillPayment(
            Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 100_000)]);

        bill.Huy(Guid.NewGuid(), "huỷ", DateTime.UtcNow, caDaKetThuc: false);

        // 📌 Huỷ thì tiền mặt của ca **không** được cộng (tiền đã trả lại khách) — nếu cộng, kết ca báo THỪA.
        Assert.Equal(0m, bill.TienMatThucThu());
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // ⑤ KẾT CA — công thức Nhanh §5.3
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    private static CashSession Ca(decimal tienDau = 500_000m)
        => new(Guid.NewGuid(), "POS-CA-20261104-001", Guid.NewGuid(), Guid.NewGuid(), tienDau, DateTime.UtcNow);

    [Fact]
    public void KetCa__cong_thuc_tien_cuoi_ca_va_chenh_lech()
    {
        var ca = Ca(500_000m);

        // doanh thu tiền mặt 1.200.000 · chi phí 50.000 · trả hàng tiền mặt 100.000
        // tiền cuối ca = 500.000 + 1.200.000 − 50.000 − 100.000 = 1.550.000
        ca.KetCa(
            cashRevenue: 1_200_000m,
            cashRefund: 100_000m,
            otherExpense: 50_000m,
            handoverCash: 1_540_000m,
            nguoiKet: Guid.NewGuid(),
            atUtc: DateTime.UtcNow);

        Assert.Equal(1_550_000m, ca.ExpectedCash);
        Assert.Equal(-10_000m, ca.Variance);  // thiếu 10.000
        Assert.Equal(CashSessionStatus.Closed, ca.Status);
        Assert.NotNull(ca.ClosedAtUtc);
    }

    [Fact]
    public void KetCa__lan_2__nem_0010()
    {
        var ca = Ca();
        ca.KetCa(100_000, 0, 0, 600_000, Guid.NewGuid(), DateTime.UtcNow);

        var loi = Assert.Throws<BusinessException>(() =>
            ca.KetCa(100_000, 0, 0, 600_000, Guid.NewGuid(), DateTime.UtcNow));

        Assert.Equal(PosErrorCodes.CaDaKetThuc, loi.Code);
    }

    [Fact]
    public void MoCa__tien_dau_AM__nem_0013()
    {
        var loi = Assert.Throws<BusinessException>(() => new CashSession(
            Guid.NewGuid(), "POS-CA-20261104-002", Guid.NewGuid(), null, -1m, DateTime.UtcNow));

        Assert.Equal(PosErrorCodes.TienCaKhongHopLe, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    // ⑥ TRẢ HÀNG — trừ nợ TRƯỚC, hoàn tiền SAU (§5.4) + chặn trả vượt
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TraHang__hoa_don_CON_NO__tru_no_truoc_roi_moi_hoan_tien()
    {
        var bill = HoaDon(khach: Guid.NewGuid());
        bill.HoanTat(
            [Dong(bill, 2, 100_000)],
            [new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 150_000)],
            hanThanhToan: DateTime.UtcNow.AddDays(7));

        Assert.Equal(50_000m, bill.DebtAmount);

        // Trả 1 món (100.000), phí 0 ⇒ 100.000: trừ hết nợ 50.000, còn 50.000 hoàn tiền mặt.
        var phieu = new RetailReturn(Guid.NewGuid(), "PT-20261104-0001", bill.Id, bill.WarehouseId);
        phieu.GanDong([new RetailReturnLine(Guid.NewGuid(), phieu.Id, "SKU-1", 1, 100_000)]);
        phieu.TinhTien(0m, bill.DebtAmount, RetailPaymentMethod.Cash);

        Assert.Equal(100_000m, phieu.GoodsAmount);
        Assert.Equal(50_000m, phieu.DebtReductionAmount);
        Assert.Equal(50_000m, phieu.RefundAmount);

        // 🚨 Bất biến cân bằng (DB cũng có CHECK y hệt).
        Assert.Equal(
            phieu.GoodsAmount,
            phieu.DebtReductionAmount + phieu.RefundAmount + phieu.ReturnFeeAmount);
    }

    [Fact]
    public void TraHang__hoa_don_KHONG_NO__hoan_toan_bo()
    {
        var phieu = new RetailReturn(Guid.NewGuid(), "PT-20261104-0002", Guid.NewGuid(), Guid.NewGuid());
        phieu.GanDong([new RetailReturnLine(Guid.NewGuid(), phieu.Id, "SKU-1", 1, 80_000)]);
        phieu.TinhTien(10_000m, conNoCuaHoaDon: 0m, RetailPaymentMethod.BankTransfer);

        Assert.Equal(0m, phieu.DebtReductionAmount);
        Assert.Equal(70_000m, phieu.RefundAmount); // 80.000 − phí 10.000
    }

    [Fact]
    public void TraHang__phi_VUOT_tien_hang__nem_0020()
    {
        var phieu = new RetailReturn(Guid.NewGuid(), "PT-20261104-0003", Guid.NewGuid(), Guid.NewGuid());
        phieu.GanDong([new RetailReturnLine(Guid.NewGuid(), phieu.Id, "SKU-1", 1, 10_000)]);

        var loi = Assert.Throws<BusinessException>(() =>
            phieu.TinhTien(20_000m, 0m, RetailPaymentMethod.Cash));

        Assert.Equal(PosErrorCodes.TienHoanKhongHopLe, loi.Code);
    }

    [Fact]
    public void TraHang__hoan_bang_DIEM__bi_tu_choi()
    {
        var phieu = new RetailReturn(Guid.NewGuid(), "PT-20261104-0004", Guid.NewGuid(), Guid.NewGuid());
        phieu.GanDong([new RetailReturnLine(Guid.NewGuid(), phieu.Id, "SKU-1", 1, 10_000)]);

        var loi = Assert.Throws<BusinessException>(() =>
            phieu.TinhTien(0m, 0m, RetailPaymentMethod.Points));

        Assert.Equal(PosErrorCodes.TienHoanKhongHopLe, loi.Code);
    }

    [Fact]
    public void DongHoaDon__tra_VUOT_so_da_mua__nem_0019()
    {
        var dong = new RetailBillLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", 2, 10_000);

        dong.CongTra(1);

        var loi = Assert.Throws<BusinessException>(() => dong.CongTra(2)); // 1 + 2 > 2

        Assert.Equal(PosErrorCodes.TraVuotSoDaMua, loi.Code);
        Assert.Equal(1m, dong.ReturnedQuantity); // KHÔNG cộng dở dang khi ném
    }

    [Fact]
    public void GiamNo__vuot_con_no__nem_0020()
    {
        var bill = HoaDon(khach: Guid.NewGuid());
        bill.HoanTat(
            [Dong(bill, 1, 100_000)],
            [new RetailBillPayment(Guid.NewGuid(), bill.Id, RetailPaymentMethod.Cash, 90_000)],
            hanThanhToan: DateTime.UtcNow.AddDays(7));

        var loi = Assert.Throws<BusinessException>(() => bill.GiamNo(20_000m));

        Assert.Equal(PosErrorCodes.TienHoanKhongHopLe, loi.Code);
    }
}
