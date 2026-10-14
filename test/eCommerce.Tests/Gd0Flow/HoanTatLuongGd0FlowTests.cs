using eCommerce.Address;
using eCommerce.EInvoice;
using eCommerce.EInvoice.Application;
using eCommerce.Signing;
using Volo.Abp;
using EInvoiceEntity = eCommerce.EInvoice.EInvoice;


namespace eCommerce.Tests.Gd0Flow;

/// <summary>
/// Test cho **phần còn lại của luồng GĐ0**: ký (bước 2) → phát hành → gửi CQT → nhận kết quả.
///
/// 🎯 Vì sao test CẢ CHUỖI chứ không chỉ từng bước: các bước này **có thứ tự bắt buộc**, và lỗi thật
/// thường nằm ở **thứ tự** (phát hành trước khi ký? gửi CQT trước khi phát hành?) — từng bước riêng lẻ
/// vẫn "đúng" nhưng cả chuỗi thì sai.
/// </summary>
public class HoanTatLuongGd0FlowTests
{
    private static readonly DateTime NgayLap = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
    private const string HashSai = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static List<AdministrativeUnit> DonVi()
    {
        var v2 = AdministrativeSchemaVersion.V2;
        return
        [
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Tinh, "01", "Thành phố Hà Nội"),
            AdministrativeUnit.TaoMoi(v2, AdministrativeLevel.Xa, "00004", "Phường Ba Đình", parentCode: "01")
        ];
    }

    /// <summary>Chạy bước 1 của luồng (đã test riêng) để lấy hoá đơn nháp + yêu cầu ký.</summary>
    private static (EInvoiceEntity HoaDon, SigningRequest YeuCauKy, SigningCertificate ChungThu) ChuanBi()
    {
        var keHoach = PhatHanhHoaDonGd0Planner.Lap(
            DonVi(), new PhatHanhHoaDonGd0Input(2, "01", "00004", "1C26TAA", "00000042", NgayLap));

        var chungThu = SigningCertificate.TaoMoi(
            "SER-1", "CÔNG TY THỬ NGHIỆM", "VNPT-CA", NgayLap.AddYears(-1), NgayLap.AddYears(1));

        return (keHoach.HoaDon, keHoach.YeuCauKy, chungThu);
    }

    private static void KyThanhCong(
        EInvoiceEntity hoaDon, SigningRequest yeuCauKy, SigningCertificate chungThu, DateTime? thoiDiem = null)
        => HoanTatLuongGd0Planner.Ky(
            hoaDon, yeuCauKy, chungThu,
            yeuCauKy.HashTaiLieu, "Y2h1LWt5", "Nguyễn Văn A", "<xml-da-ky/>", thoiDiem ?? NgayLap);

    // ── Chuỗi THÀNH CÔNG đầy đủ ─────────────────────────────────────────────

    [Fact]
    public void Chuoi_day_du_ky_phat_hanh_gui_CQT_nhan_ket_qua()
    {
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();

        // Bước 2 — ký
        KyThanhCong(hoaDon, yeuCauKy, chungThu);
        Assert.Equal(SigningRequestStatus.DaKy, yeuCauKy.TrangThai);
        Assert.Equal(EInvoiceStatus.DaKy, hoaDon.Status);

        // Phát hành + gửi CQT
        HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap);
        Assert.Equal(EInvoiceStatus.DaPhatHanh, hoaDon.Status);
        Assert.Equal(TaxAuthorityStatus.DaGui, hoaDon.TaxAuthorityStatus);
        Assert.Equal(1, hoaDon.SoLanGuiCQT);

        // Nhận kết quả
        var ketQua = HoanTatLuongGd0Planner.NhanKetQuaCqt(hoaDon, "OK:MCQT-123");

        Assert.True(ketQua.ThanhCong);
        Assert.Equal(TaxAuthorityStatus.DaNhan, hoaDon.TaxAuthorityStatus);
        Assert.Equal("MCQT-123", hoaDon.MaCQT);
        Assert.Equal("MCQT-123", ketQua.MaCQT);
    }

    // ── 🚨 Chốt chặn ở tầng luồng: yêu cầu ký phải thuộc hoá đơn này ────────

    [Fact]
    public void YEU_CAU_KY_KHONG_THUOC_HOA_DON_thi_NEM_LOI()
    {
        // Kịch bản nguy hiểm: ký tài liệu của hoá đơn A rồi gắn vào hoá đơn B.
        // Domain không thể biết (hash vẫn khớp) — chỉ LUỒNG mới biết ⇒ phải chặn ở đây.
        var (hoaDon, _, chungThu) = ChuanBi();

        var yeuCauKyCuaHoaDonKhac = SigningRequest.TaoYeuCau(
            "HD:1C26TAA/00000099", new string('a', 64));

        var ex = Assert.Throws<BusinessException>(() => HoanTatLuongGd0Planner.Ky(
            hoaDon, yeuCauKyCuaHoaDonKhac, chungThu,
            yeuCauKyCuaHoaDonKhac.HashTaiLieu, "Y2g=", "A", "<xml/>", NgayLap));

        Assert.Equal(Gd0FlowErrorCodes.YeuCauKyKhongThuocHoaDon, ex.Code);
    }

    [Fact]
    public void KY_BANG_HASH_KHAC_thi_NEM_LOI_va_KHONG_danh_dau_da_ky()
    {
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();

        var ex = Assert.Throws<BusinessException>(() => HoanTatLuongGd0Planner.Ky(
            hoaDon, yeuCauKy, chungThu, HashSai, "Y2g=", "A", "<xml/>", NgayLap));

        Assert.Equal(SigningErrorCodes.HashKhongKhop, ex.Code);
        // ⬅️ CẢ HAI đều chưa đổi trạng thái ⇒ không có "nửa vời".
        Assert.Equal(SigningRequestStatus.ChoKy, yeuCauKy.TrangThai);
        Assert.Equal(EInvoiceStatus.Nhap, hoaDon.Status);
    }

    // ── Thứ tự bắt buộc ─────────────────────────────────────────────────────

    [Fact]
    public void Phat_hanh_khi_CHUA_KY_thi_NEM_LOI()
    {
        var (hoaDon, _, _) = ChuanBi();

        var ex = Assert.Throws<BusinessException>(
            () => HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap));

        Assert.Equal(EInvoiceErrorCodes.ChuaKyKhongDuocPhatHanh, ex.Code);
    }

    [Fact]
    public void Gui_CQT_khi_CHUA_PHAT_HANH_thi_NEM_LOI()
    {
        // Ký rồi nhưng CHƯA phát hành ⇒ chưa được gửi CQT (Domain cưỡng chế).
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();
        KyThanhCong(hoaDon, yeuCauKy, chungThu);

        var ex = Assert.Throws<BusinessException>(() => hoaDon.GuiCQT(NgayLap));

        Assert.Equal(EInvoiceErrorCodes.ChuaPhatHanhKhongDuocGuiCQT, ex.Code);
    }

    [Fact]
    public void Nhan_ket_qua_khi_CHUA_GUI_thi_NEM_LOI()
    {
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();
        KyThanhCong(hoaDon, yeuCauKy, chungThu);
        hoaDon.PhatHanh();   // đã phát hành nhưng CHƯA gửi

        var ex = Assert.Throws<BusinessException>(
            () => HoanTatLuongGd0Planner.NhanKetQuaCqt(hoaDon, "OK:MCQT-1"));

        Assert.Equal(EInvoiceErrorCodes.ChuaGuiKhongTheNhanKetQua, ex.Code);
    }

    // ── Hai trục trạng thái, kiểm ở mức CHUỖI ───────────────────────────────

    [Fact]
    public void CQT_TU_CHOI_thi_hoa_don_VAN_da_phat_hanh()
    {
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();
        KyThanhCong(hoaDon, yeuCauKy, chungThu);
        HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap);

        var ketQua = HoanTatLuongGd0Planner.NhanKetQuaCqt(hoaDon, "ERR:5:Quá tải");

        Assert.False(ketQua.ThanhCong);
        Assert.Equal("5", ketQua.MaLoi);
        Assert.Equal(TaxAuthorityStatus.BiTuChoi, hoaDon.TaxAuthorityStatus);
        Assert.Equal(EInvoiceStatus.DaPhatHanh, hoaDon.Status);   // ⬅️ trục 1 KHÔNG đổi
        Assert.Equal("5", hoaDon.LoiCQT);
    }

    [Fact]
    public void Phan_hoi_KHONG_nhan_dang_duoc_thi_NEM_LOI_va_KHONG_doi_trang_thai()
    {
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();
        KyThanhCong(hoaDon, yeuCauKy, chungThu);
        HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap);

        var ex = Assert.Throws<BusinessException>(
            () => HoanTatLuongGd0Planner.NhanKetQuaCqt(hoaDon, "SUCCESS"));

        Assert.Equal(EInvoiceSubmissionErrorCodes.PhanHoiKhongNhanDangDuoc, ex.Code);
        Assert.Equal(TaxAuthorityStatus.DaGui, hoaDon.TaxAuthorityStatus);   // vẫn đang chờ
    }

    [Fact]
    public void Sau_khi_bi_tu_choi_thi_mo_lai_va_gui_lai_duoc()
    {
        // Khép vòng với job gửi lại (mục 4): chính sách cho phép ⇒ mở lại ⇒ gửi lại ⇒ nhận kết quả.
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();
        KyThanhCong(hoaDon, yeuCauKy, chungThu);
        HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap);
        HoanTatLuongGd0Planner.NhanKetQuaCqt(hoaDon, "ERR:5");

        var quyetDinh = EInvoiceResubmissionPolicy.DanhGia(
            hoaDon.TaxAuthorityStatus, hoaDon.LoiCQT, hoaDon.SoLanGuiCQT, hoaDon.LanGuiCuoiUtc,
            NgayLap.AddHours(1));

        Assert.True(quyetDinh.NenGuiLai);

        hoaDon.ChoPhepGuiLaiCQT();
        HoanTatLuongGd0Planner.GuiLaiCqt(hoaDon, NgayLap.AddHours(1));   // Dùng GuiLaiCqt (KHÔNG gọi lại PhatHanh — hoá đơn đã phát hành).

        Assert.Equal(TaxAuthorityStatus.DaGui, hoaDon.TaxAuthorityStatus);
        Assert.Equal(2, hoaDon.SoLanGuiCQT);
    }

    [Fact]
    public void Huy_hoa_don_da_phat_hanh_trong_luong_van_chan_dung()
    {
        var (hoaDon, yeuCauKy, chungThu) = ChuanBi();
        KyThanhCong(hoaDon, yeuCauKy, chungThu);
        HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap);

        hoaDon.Huy("Huỷ theo thoả thuận");
        Assert.Equal(EInvoiceStatus.DaHuy, hoaDon.Status);

        Assert.Equal(EInvoiceErrorCodes.HoaDonDaKetThuc,
            Assert.Throws<BusinessException>(() => HoanTatLuongGd0Planner.PhatHanhVaGuiCqt(hoaDon, NgayLap)).Code);
    }
}
