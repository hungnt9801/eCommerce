using System.Reflection;
using eCommerce.HttpApi.Controllers;
using eCommerce.Orders;
using eCommerce.Pos;
using eCommerce.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace eCommerce.Tests.Reporting;

/// <summary>
/// **`WP-08` — BÁO CÁO: lõi doanh thu + mốc + so sánh với chứng từ nguồn** (`07 §WP-08`).
///
/// 🎯 **DoD của WP-08 là MỘT PHÉP SO SÁNH**: *"tổng doanh thu báo cáo = `SUM(RetailBills + Orders)` trong
/// cùng kỳ (test so sánh); job chạy lại 2 lần không nhân đôi dữ liệu"*.
/// 📌 Vì vậy test quan trọng nhất ở đây **dựng chứng từ THẬT** (`RetailBill.HoanTat` + `Order.AddLine`),
/// đẩy qua **đúng hàm ánh xạ** mà job dùng, rồi so tổng báo cáo với tổng tính **độc lập** từ chứng từ
/// (`Subtotal − DiscountTotal` của hoá đơn; Σ dòng đơn − chiết khấu đơn).
/// 🚨 Nếu chỉ test `TinhNgay` với dữ liệu tự bịa thì phép so sánh là **hiển nhiên đúng** và không bắt được
/// lỗi thật (đã có tiền lệ `WP-07`: công thức sai vẫn qua toàn bộ unit test, probe mới bắt được).
/// </summary>
public class Wp08BaoCaoTests
{
    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 1. NGÀY KINH DOANH (UTC+7) — chỗ dễ sai nhất của báo cáo theo ngày
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NgayKinhDoanh_1730_UTC_la_NGAY_HOM_SAU_o_Viet_Nam()
    {
        // 17:30 UTC = 00:30 hôm sau giờ VN ⇒ doanh thu buổi tối thuộc NGÀY MAI. Dùng `CreationTime.Date`
        // (UTC) sẽ đẩy nó về hôm trước ⇒ "doanh thu hôm nay" của nhân viên SAI đúng lúc họ đang xem màn hình.
        var utc = new DateTime(2026, 11, 4, 17, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 11, 5), NgayKinhDoanh.TuUtc(utc));
    }

    [Fact]
    public void NgayKinhDoanh_1659_UTC_van_la_CUNG_NGAY()
    {
        var utc = new DateTime(2026, 11, 4, 16, 59, 59, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 11, 4), NgayKinhDoanh.TuUtc(utc));
    }

    [Fact]
    public void NgayKinhDoanh_nua_khoang_phu_DU_mot_ngay_va_KHONG_chong_lan()
    {
        var ngay = new DateOnly(2026, 11, 4);

        var batDau = NgayKinhDoanh.BatDauUtc(ngay);
        var ketThuc = NgayKinhDoanh.KetThucUtc(ngay);

        // 📌 00:00 giờ VN = 17:00 UTC **hôm trước**.
        Assert.Equal(new DateTime(2026, 11, 3, 17, 0, 0, DateTimeKind.Utc), batDau);
        Assert.Equal(batDau.AddDays(1), ketThuc);

        // Mốc cuối là NỬA KHOẢNG: 23:59:59.5 giờ VN (= 16:59:59.5 UTC) PHẢI thuộc ngày này.
        var ganCuoiNgay = new DateTime(2026, 11, 4, 16, 59, 59, 500, DateTimeKind.Utc);

        Assert.True(ganCuoiNgay >= batDau && ganCuoiNgay < ketThuc);
        Assert.Equal(ngay, NgayKinhDoanh.TuUtc(ganCuoiNgay));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 2. KHOẢNG TỔNG HỢP (mốc watermark + backfill)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Khoang_chua_co_moc_lan_dau_lui_30_ngay()
    {
        var homNay = new DateOnly(2026, 11, 4);

        var khoang = KhoangTongHop.XacDinh(null, null, null, homNay);

        Assert.False(khoang.Rong);
        Assert.Equal(homNay, khoang.Den);
        Assert.Equal(homNay.AddDays(-(KhoangTongHop.SoNgayLanDau - 1)), khoang.Tu);
        Assert.Equal(30, khoang.SoNgay);
    }

    [Fact]
    public void Khoang_co_moc_chay_tiep_TU_NGAY_SAU_moc()
    {
        var homNay = new DateOnly(2026, 11, 4);

        var khoang = KhoangTongHop.XacDinh(new DateOnly(2026, 11, 1), null, null, homNay);

        Assert.Equal(new DateOnly(2026, 11, 2), khoang.Tu);
        Assert.Equal(homNay, khoang.Den);
        Assert.Equal(3, khoang.SoNgay);
    }

    [Fact]
    public void Khoang_da_o_cuoi_thi_RONG_va_KHONG_phai_loi()
    {
        var homNay = new DateOnly(2026, 11, 4);

        var khoang = KhoangTongHop.XacDinh(homNay, null, null, homNay);

        Assert.True(khoang.Rong);
        Assert.Empty(khoang.CacNgay());
    }

    [Fact]
    public void Khoang_backfill_THANG_moc()
    {
        // Backfill = "làm lại một khoảng đã biết" ⇒ phải thắng mốc, nếu không không bao giờ sửa lại được số cũ.
        var khoang = KhoangTongHop.XacDinh(
            new DateOnly(2026, 11, 3), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 4));

        Assert.Equal(new DateOnly(2026, 10, 1), khoang.Tu);
        Assert.Equal(new DateOnly(2026, 10, 31), khoang.Den);
    }

    [Fact]
    public void Khoang_tu_LON_HON_den_thi_NEM_Reports_0002()
    {
        var loi = Assert.Throws<BusinessException>(() => KhoangTongHop.XacDinh(
            null, new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 6)));

        Assert.Equal(ReportingErrorCodes.KhoangKhongHopLe, loi.Code);
    }

    [Fact]
    public void Khoang_qua_366_ngay_thi_NEM_chu_KHONG_tu_cat_im_lang()
    {
        // 📌 Tự cắt ngắn làm người dùng tưởng "đã backfill hết" trong khi nửa sau chưa chạy.
        var loi = Assert.Throws<BusinessException>(() => KhoangTongHop.XacDinh(
            null, new DateOnly(2025, 1, 1), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 4)));

        Assert.Equal(ReportingErrorCodes.KhoangQuaDai, loi.Code);
    }

    [Fact]
    public void Khoang_ngay_TUONG_LAI_bi_KEP_ve_hom_nay()
    {
        var khoang = KhoangTongHop.XacDinh(
            null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 11, 4));

        Assert.Equal(new DateOnly(2026, 11, 4), khoang.Den);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 3. CÔNG THỨC DOANH THU — một chỗ duy nhất
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DoanhThu_chung_tu_bang_tien_dong_TRU_chiet_khau_chung_tu()
    {
        Assert.Equal(95_000m, TongHopDoanhThu.TinhDoanhThuChungTu(100_000m, 5_000m));
    }

    [Fact]
    public void Chiet_khau_LON_HON_tien_dong_thi_NEM_chu_KHONG_ve_0()
    {
        // Doanh thu âm = chứng từ sai dấu. Trả 0 cho "đẹp" là che lỗi dữ liệu.
        var loi = Assert.Throws<BusinessException>(
            () => TongHopDoanhThu.TinhDoanhThuChungTu(10_000m, 20_000m));

        Assert.Equal(ReportingErrorCodes.DoanhThuAm, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 4. 🚨 DoD: DOANH THU BÁO CÁO = SUM(RetailBills + Orders) — chứng từ THẬT
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DoD_doanh_thu_bao_cao_BANG_tong_chung_tu_ban_le_va_don_hang()
    {
        var ngay = new DateOnly(2026, 11, 4);

        // ── (a) HOÁ ĐƠN BÁN LẺ thật: 2 món × 100.000, chiết khấu hoá đơn 10.000 ⇒ phải trả 190.000.
        var hoaDon = new RetailBill(
            Guid.NewGuid(), "POS-TEST-01", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var dongHoaDon = new List<RetailBillLine>
        {
            new(Guid.NewGuid(), hoaDon.Id, "SKU-A", 1, 100_000m),
            new(Guid.NewGuid(), hoaDon.Id, "SKU-B", 2, 50_000m)
        };

        hoaDon.HoanTat(
            dongHoaDon,
            new List<RetailBillPayment> { new(Guid.NewGuid(), hoaDon.Id, RetailPaymentMethod.Cash, 190_000m) },
            chietKhauHoaDon: 10_000m);

        // ── (b) ĐƠN HÀNG thật: 3 món × 80.000, chiết khấu đơn 15.000, dòng có thuế 5.000.
        var donHang = new Order(Guid.NewGuid(), "ORD-TEST-01", "web");
        var dongDon = donHang.AddLine(Guid.NewGuid(), "SKU-C", 3, 80_000m);
        dongDon.TaxAmount = 5_000m;
        donHang.SetDiscount(15_000m);

        // ── (c) Ánh xạ ĐÚNG hàm mà job dùng (không chép lại cách đọc cột trong test).
        var (ctHoaDon, dongTuHoaDon) = AnhXaNguonBanHang.TuHoaDonBanLe(hoaDon, dongHoaDon);
        var (ctDonHang, dongTuDonHang) = AnhXaNguonBanHang.TuDonHang(donHang, new[] { dongDon });

        // Gán ngày kinh doanh của dữ liệu test (entity chưa qua DB nên `CreationTime` là mốc mặc định).
        ctHoaDon = ctHoaDon with { NgayKinhDoanh = ngay };
        ctDonHang = ctDonHang with { NgayKinhDoanh = ngay };

        var ketQua = TongHopDoanhThu.TinhNgay(
            new[] { ctHoaDon, ctDonHang },
            dongTuHoaDon.Concat(dongTuDonHang).ToList());

        // ── (d) Tổng doanh thu báo cáo (đọc từ CỘT của bảng tổng hợp, không cộng lại dòng sự kiện).
        var doanhThuBaoCao = ketQua.TongHop.Sum(x => x.DoanhThu);

        // ── (e) Tổng tính ĐỘC LẬP từ chứng từ — đúng khẳng định DoD.
        var doanhThuHoaDon = hoaDon.Subtotal - hoaDon.DiscountTotal;                       // 190.000
        var doanhThuDonHang = (dongDon.UnitPrice * dongDon.Quantity - dongDon.DiscountAmount)
                              - donHang.DiscountTotal;                                      // 240.000 − 15.000

        Assert.Equal(190_000m, doanhThuHoaDon);
        Assert.Equal(225_000m, doanhThuDonHang);
        Assert.Equal(doanhThuHoaDon + doanhThuDonHang, doanhThuBaoCao);
        Assert.Equal(415_000m, doanhThuBaoCao);

        // 📌 VAT **không** được cộng vào doanh thu (dòng đơn có 5.000 thuế): nếu ai đó dùng
        // `OrderLine.LineTotal` thì tổng thành 420.000 — và đây là dòng bắt lỗi đó.
        Assert.Equal(5_000m, ketQua.TongHop.Single(x => x.Nguon == NguonBaoCao.DonHang).TienVat);
        Assert.Equal(0m, ketQua.TongHop.Single(x => x.Nguon == NguonBaoCao.BanLe).TienVat);
    }

    [Fact]
    public void DoD_chay_lai_HAI_lan_KHONG_nhan_doi_du_lieu()
    {
        var ngay = new DateOnly(2026, 11, 4);

        var chungTu = new ChungTuBanHangNguon(
            NguonBaoCao.BanLe, Guid.NewGuid(), "POS-01", ngay, DateTime.UtcNow, null, null, null, 0m);

        var mot = TongHopDoanhThu.TinhNgay(
            new[] { chungTu },
            new[]
            {
                new DongBanHangNguon(Guid.NewGuid(), chungTu.ChungTuId, "SKU-A", null, 2m, 100_000m, 0m, 200_000m, null, 0m)
            });

        // Lần 1: read-model rỗng + lượt mới.
        var dongSauLan1 = TongHopDoanhThu.GopTheoNgay(Array.Empty<DongSuKienBanHang>(), mot.Dong);
        var tongSauLan1 = TongHopDoanhThu.GopTongHopTheoNgay(Array.Empty<DongTongHopNgay>(), mot.TongHop);

        // Lần 2: chạy lại ĐÚNG ngày đó (mô phỏng job chạy lại) ⇒ phải THAY THẾ, không cộng dồn.
        var dongSauLan2 = TongHopDoanhThu.GopTheoNgay(dongSauLan1, mot.Dong);
        var tongSauLan2 = TongHopDoanhThu.GopTongHopTheoNgay(tongSauLan1, mot.TongHop);

        Assert.Equal(dongSauLan1.Count, dongSauLan2.Count);
        Assert.Equal(tongSauLan1.Count, tongSauLan2.Count);
        Assert.Equal(tongSauLan1.Sum(x => x.DoanhThu), tongSauLan2.Sum(x => x.DoanhThu));
        Assert.Equal(200_000m, tongSauLan2.Sum(x => x.DoanhThu));

        // 📌 Và chứng minh phép "cộng dồn" (đường SAI) sẽ nhân đôi — để thấy vì sao luật phải là THAY THẾ.
        var neuCongDon = dongSauLan1.Concat(mot.Dong).ToList();

        Assert.Equal(2, neuCongDon.Count);
        Assert.NotEqual(dongSauLan1.Count, neuCongDon.Count);
    }

    [Fact]
    public void Dong_nguon_TRUNG_trong_mot_luot_thi_NEM_Reports_0008()
    {
        var ngay = new DateOnly(2026, 11, 4);
        var chungTu = new ChungTuBanHangNguon(
            NguonBaoCao.BanLe, Guid.NewGuid(), "POS-01", ngay, DateTime.UtcNow, null, null, null, 0m);
        var dongId = Guid.NewGuid();

        var loi = Assert.Throws<BusinessException>(() => TongHopDoanhThu.TinhNgay(
            new[] { chungTu },
            new[]
            {
                new DongBanHangNguon(dongId, chungTu.ChungTuId, "SKU-A", null, 1m, 100_000m, 0m, 100_000m, null, 0m),
                new DongBanHangNguon(dongId, chungTu.ChungTuId, "SKU-A", null, 1m, 100_000m, 0m, 100_000m, null, 0m)
            }));

        Assert.Equal(ReportingErrorCodes.DongTrung, loi.Code);
    }

    [Fact]
    public void Dong_MO_COI_thi_NEM_Reports_0005()
    {
        var loi = Assert.Throws<BusinessException>(() => TongHopDoanhThu.TinhNgay(
            Array.Empty<ChungTuBanHangNguon>(),
            new[] { new DongBanHangNguon(Guid.NewGuid(), Guid.NewGuid(), "SKU-A", null, 1m, 1m, 0m, 1m, null, 0m) }));

        Assert.Equal(ReportingErrorCodes.DongMoCoi, loi.Code);
    }

    [Fact]
    public void ChungTu_co_chiet_khau_nhung_KHONG_co_dong_thi_NEM_Reports_0009()
    {
        var chungTu = new ChungTuBanHangNguon(
            NguonBaoCao.BanLe, Guid.NewGuid(), "POS-01", new DateOnly(2026, 11, 4), DateTime.UtcNow,
            null, null, null, 5_000m);

        var loi = Assert.Throws<BusinessException>(
            () => TongHopDoanhThu.TinhNgay(new[] { chungTu }, Array.Empty<DongBanHangNguon>()));

        Assert.Equal(ReportingErrorCodes.ChungTuKhongCoDong, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 5. MỐC TỔNG HỢP (watermark) — chỉ tiến
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Moc_CHI_TIEN__backfill_khoang_cu_KHONG_ha_moc()
    {
        var moc = new RptWatermark(Guid.NewGuid(), NguonBaoCao.BanLe);

        moc.GhiNhan(new DateOnly(2026, 11, 4), DateTime.UtcNow, 100);

        // Backfill tháng 10 (cũ hơn mốc) — mốc PHẢI giữ 04/11.
        moc.GhiNhan(new DateOnly(2026, 10, 31), DateTime.UtcNow, 20);

        Assert.Equal(new DateOnly(2026, 11, 4), moc.LastAggregatedDate);
        Assert.Equal(20, moc.LastRowCount);
    }

    [Fact]
    public void Moc_tien_khi_co_du_lieu_moi()
    {
        var moc = new RptWatermark(Guid.NewGuid(), NguonBaoCao.DonHang);

        moc.GhiNhan(new DateOnly(2026, 11, 3), DateTime.UtcNow, 1);
        moc.GhiNhan(new DateOnly(2026, 11, 5), DateTime.UtcNow, 7);

        Assert.Equal(new DateOnly(2026, 11, 5), moc.LastAggregatedDate);
    }

    [Fact]
    public void Moc_so_dong_AM_thi_NEM()
    {
        var moc = new RptWatermark(Guid.NewGuid(), NguonBaoCao.BanLe);

        Assert.Throws<BusinessException>(
            () => moc.GhiNhan(new DateOnly(2026, 11, 4), DateTime.UtcNow, -1));
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 6. ẢNH TỒN KHO — hai cột dẫn xuất phải khớp công thức `D-27`
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Anh_ton_kho_tinh_DUNG_InWarehouse_va_Available()
    {
        var anh = RptFactInventorySnapshot.TuTonKho(
            Guid.NewGuid(), new DateOnly(2026, 11, 4), Guid.NewGuid(), "SKU-A",
            onHand: 100m, reserved: 10m, transferring: 5m, shipping: 2m, holding: 3m,
            damaged: 1m, warranty: 1m, warrantyHolding: 0m, preOrder: 2m, waitingInbound: 4m,
            capturedAtUtc: DateTime.UtcNow);

        // `InWarehouse = OnHand − Transferring` · `Available = OnHand − Reserved − Transferring − Holding − Damaged`.
        Assert.Equal(95m, anh.InWarehouse);
        Assert.Equal(81m, anh.Available);
    }

    [Fact]
    public void Anh_ton_kho_chi_chieu_AM_thi_NEM_Reports_0006()
    {
        var loi = Assert.Throws<BusinessException>(() => RptFactInventorySnapshot.TuTonKho(
            Guid.NewGuid(), new DateOnly(2026, 11, 4), Guid.NewGuid(), "SKU-A",
            onHand: 10m, reserved: -1m, transferring: 0m, shipping: 0m, holding: 0m,
            damaged: 0m, warranty: 0m, warrantyHolding: 0m, preOrder: 0m, waitingInbound: 0m,
            capturedAtUtc: DateTime.UtcNow));

        Assert.Equal(ReportingErrorCodes.TonKhoAm, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 7. BẢNG GHI: chỉ nhận nguồn doanh thu + không nhận số âm
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dong_su_kien_KHONG_nhan_nguon_TonKho()
    {
        var suKien = new DongSuKienBanHang(
            new DateOnly(2026, 11, 4), NguonBaoCao.TonKho, Guid.NewGuid(), Guid.NewGuid(), "X-01",
            null, null, null, "SKU-A", null, 1m, 1m, 0m, 1m, null, 0m, DateTime.UtcNow);

        var loi = Assert.Throws<BusinessException>(
            () => RptFactSalesLine.TuSuKien(Guid.NewGuid(), suKien));

        Assert.Equal(ReportingErrorCodes.NguonKhongHopLe, loi.Code);
    }

    [Fact]
    public void Dong_tong_hop_KHONG_nhan_doanh_thu_AM()
    {
        var dong = new DongTongHopNgay(
            new DateOnly(2026, 11, 4), NguonBaoCao.BanLe, 1, 1, 1m, 10m, 0m, -5m, 0m);

        var loi = Assert.Throws<BusinessException>(
            () => RptAggDailySales.TuTongHop(Guid.NewGuid(), dong, DateTime.UtcNow));

        Assert.Equal(ReportingErrorCodes.DoanhThuAm, loi.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 8. HỢP ĐỒNG API: route + quyền (mỗi việc một quyền)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    public static TheoryData<Type, string, string> CapQuyen => new()
    {
        { typeof(ReportAppService), nameof(ReportAppService.ChayTongHopAsync), eCommerceConsts.Permissions.Reports.Aggregate }
    };

    [Theory]
    [MemberData(nameof(CapQuyen))]
    public void Method_GHI_phai_gan_DUNG_mot_permission(Type loai, string tenMethod, string permission)
    {
        var method = loai.GetMethod(tenMethod, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Equal(permission, method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void AppService_PHAI_co_Authorize_cap_CLASS_voi_quyen_DOC()
    {
        // 🔒 Quy ước #7: thiếu `[Authorize]` cấp class ⇒ method quên attribute là **công khai**.
        var attr = typeof(ReportAppService).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(eCommerceConsts.Permissions.Reports.Default, attr!.Policy);
    }

    [Fact]
    public void Controller_bao_cao_PHAI_co_du_5_route_va_quyen_doc_o_cap_class()
    {
        var route = typeof(ReportsController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("api/v1/reports", route!.Template);

        Assert.Equal(
            eCommerceConsts.Permissions.Reports.Default,
            typeof(ReportsController).GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var duong = typeof(ReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()
                .Select(a => "GET " + a.Template)
                .Concat(m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()
                    .Select(a => "POST " + a.Template)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[] { "GET dashboard", "GET inventory/summary", "GET orders/summary", "GET revenue", "POST aggregate" },
            duong);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 9. Quyền báo cáo phải được KHAI (hai nơi) — nếu không: 403 cho MỌI người
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Quyen_bao_cao_co_CA_HAI_gia_tri_va_dung_tien_to()
    {
        Assert.Equal("eCommerce.Reports", eCommerceConsts.Permissions.Reports.Default);
        Assert.Equal("eCommerce.Reports.Aggregate", eCommerceConsts.Permissions.Reports.Aggregate);
    }
}
