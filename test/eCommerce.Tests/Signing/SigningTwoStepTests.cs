using eCommerce.Signing;
using Volo.Abp;

namespace eCommerce.Tests.Signing;

/// <summary>
/// Test cho **`SIGN-001`** — quy trình **ký 2 bước thủ công** (mục (g)).
///
/// 🎯 Lỗi im lặng nguy hiểm nhất được khoá ở đây: **ký nhầm tài liệu**. Nếu không kiểm hash, hệ thống
/// sẽ nhận **chữ ký của tài liệu A** và gắn vào **tài liệu B** — chữ ký vẫn hợp lệ về kỹ thuật nhưng
/// **vô hiệu về pháp lý**, và **không có cảnh báo nào**.
/// Nhóm thứ hai: **chứng thư hết hiệu lực** — phải kiểm tại **đúng thời điểm ký**, không phải lúc tạo yêu cầu.
/// </summary>
public class SigningTwoStepTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly DateTime ThoiDiemKy = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    private static SigningCertificate ChungThuConHieuLuc()
        => SigningCertificate.TaoMoi(
            "SER-001", "CÔNG TY TNHH THỬ NGHIỆM", "VNPT-CA",
            ThoiDiemKy.AddYears(-1), ThoiDiemKy.AddYears(1));

    // ── Bước 1 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Buoc1_tao_yeu_cau_o_trang_thai_CHO_KY_va_ghi_lai_hash()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);

        Assert.Equal(SigningRequestStatus.ChoKy, yeuCau.TrangThai);
        Assert.Equal(HashA, yeuCau.HashTaiLieu);
        Assert.Equal("SHA256", yeuCau.ThuatToanBam);
        Assert.False(yeuCau.DaKyXong);
    }

    [Fact]
    public void Buoc1_thieu_ma_tai_lieu_thi_NEM_LOI()
    {
        Assert.Equal(SigningErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => SigningRequest.TaoYeuCau("  ", HashA)).Code);
    }

    [Fact]
    public void Buoc1_hash_sai_dinh_dang_SHA256_thi_NEM_LOI()
    {
        // SHA-256 dạng hex phải đúng 64 ký tự — sai thì báo ngay, không để lọt tới bước 2.
        var ex = Assert.Throws<BusinessException>(() => SigningRequest.TaoYeuCau("HD-1", "abc123"));
        Assert.Equal(SigningErrorCodes.HashKhongHopLe, ex.Code);

        Assert.Equal(SigningErrorCodes.HashKhongHopLe,
            Assert.Throws<BusinessException>(() => SigningRequest.TaoYeuCau("HD-1", new string('z', 64))).Code);
    }

    // ── Bước 2: luồng hợp lệ ────────────────────────────────────────────────

    [Fact]
    public void Buoc2_hash_khop_va_chung_thu_hop_le_thi_KY_XONG()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);
        var chungThu = ChungThuConHieuLuc();

        yeuCau.HoanTatKy(HashA, chungThu, "Y2h1LWt5LWJhc2U2NA==", "Nguyễn Văn A", ThoiDiemKy);

        Assert.True(yeuCau.DaKyXong);
        Assert.Equal(chungThu.Id, yeuCau.ChungThuSoId);
        Assert.Equal("Y2h1LWt5LWJhc2U2NA==", yeuCau.ChuKyDaKy);
        Assert.Equal("Nguyễn Văn A", yeuCau.NguoiKy);
        Assert.Equal(ThoiDiemKy, yeuCau.ThoiDiemKy);
    }

    [Fact]
    public void Hash_khop_KHONG_phan_biet_chu_hoa_thuong()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA.ToUpperInvariant());

        yeuCau.HoanTatKy(HashA, ChungThuConHieuLuc(), "Y2g=", "A", ThoiDiemKy);

        Assert.True(yeuCau.DaKyXong);
    }

    // ── 🚨 Bất biến quan trọng nhất: HASH PHẢI KHỚP ─────────────────────────

    [Fact]
    public void KY_NHAM_TAI_LIEU_thi_NEM_LOI_va_KHONG_doi_trang_thai()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);

        var ex = Assert.Throws<BusinessException>(
            () => yeuCau.HoanTatKy(HashB, ChungThuConHieuLuc(), "Y2g=", "A", ThoiDiemKy));

        Assert.Equal(SigningErrorCodes.HashKhongKhop, ex.Code);
        // ⬅️ Không được "nhận tạm" chữ ký của tài liệu khác:
        Assert.Equal(SigningRequestStatus.ChoKy, yeuCau.TrangThai);
        Assert.Null(yeuCau.ChuKyDaKy);
        Assert.Null(yeuCau.ChungThuSoId);
    }

    [Fact]
    public void Hash_da_ky_sai_dinh_dang_cung_bi_tu_choi()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);

        Assert.Equal(SigningErrorCodes.HashKhongHopLe,
            Assert.Throws<BusinessException>(
                () => yeuCau.HoanTatKy("khong-phai-hash", ChungThuConHieuLuc(), "Y2g=", "A", ThoiDiemKy)).Code);
    }

    // ── 🚨 Chứng thư: kiểm hiệu lực TẠI THỜI ĐIỂM KÝ ───────────────────────

    [Fact]
    public void Chung_thu_HET_HAN_tai_thoi_diem_ky_thi_NEM_LOI()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);
        var hetHan = SigningCertificate.TaoMoi(
            "SER-OLD", "Cty cũ", "VNPT-CA",
            ThoiDiemKy.AddYears(-2), ThoiDiemKy.AddDays(-1));   // hết hạn HÔM QUA

        var ex = Assert.Throws<BusinessException>(
            () => yeuCau.HoanTatKy(HashA, hetHan, "Y2g=", "A", ThoiDiemKy));

        Assert.Equal(SigningErrorCodes.ChungThuKhongHopLe, ex.Code);
        Assert.Equal(SigningRequestStatus.ChoKy, yeuCau.TrangThai);
    }

    [Fact]
    public void Chung_thu_CHUA_hieu_luc_tai_thoi_diem_ky_thi_NEM_LOI()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);
        var tuongLai = SigningCertificate.TaoMoi(
            "SER-NEW", "Cty mới", "VNPT-CA",
            ThoiDiemKy.AddDays(1), ThoiDiemKy.AddYears(1));      // hiệu lực từ NGÀY MAI

        Assert.Equal(SigningErrorCodes.ChungThuKhongHopLe,
            Assert.Throws<BusinessException>(
                () => yeuCau.HoanTatKy(HashA, tuongLai, "Y2g=", "A", ThoiDiemKy)).Code);
    }

    [Fact]
    public void Chung_thu_DA_THU_HOI_thi_NEM_LOI_du_ngay_con_trong_hieu_luc()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);
        var chungThu = ChungThuConHieuLuc();
        chungThu.ThuHoi();

        Assert.Equal(SigningErrorCodes.ChungThuKhongHopLe,
            Assert.Throws<BusinessException>(
                () => yeuCau.HoanTatKy(HashA, chungThu, "Y2g=", "A", ThoiDiemKy)).Code);
    }

    [Fact]
    public void Chung_thu_con_han_luc_TAO_yeu_cau_nhung_HET_HAN_luc_KY_thi_bi_tu_choi()
    {
        // 🚩 Đây là lý do phải kiểm tại THỜI ĐIỂM KÝ: yêu cầu có thể nằm chờ người ký nhiều ngày.
        var chungThu = SigningCertificate.TaoMoi(
            "SER-SAP", "Cty", "VNPT-CA",
            ThoiDiemKy.AddYears(-1), ThoiDiemKy.AddDays(5));

        var yeuCau = SigningRequest.TaoYeuCau("HD-0001", HashA);   // tạo lúc chứng thư còn hạn

        var kyMuon = ThoiDiemKy.AddDays(10);                        // ký SAU khi chứng thư hết hạn

        Assert.True(chungThu.ConHieuLuc(ThoiDiemKy));               // còn hạn khi tạo
        Assert.False(chungThu.ConHieuLuc(kyMuon));                  // hết hạn khi ký

        Assert.Equal(SigningErrorCodes.ChungThuKhongHopLe,
            Assert.Throws<BusinessException>(
                () => yeuCau.HoanTatKy(HashA, chungThu, "Y2g=", "A", kyMuon)).Code);
    }

    [Fact]
    public void Chung_thu_kiem_hiệu_luc_BAO_GOM_ca_hai_moc_bien()
    {
        var cert = SigningCertificate.TaoMoi(
            "SER-1", "Cty", "CA", ThoiDiemKy, ThoiDiemKy.AddDays(10));

        Assert.True(cert.ConHieuLuc(ThoiDiemKy));                   // đúng mốc bắt đầu
        Assert.True(cert.ConHieuLuc(ThoiDiemKy.AddDays(10)));       // đúng mốc kết thúc
        Assert.False(cert.ConHieuLuc(ThoiDiemKy.AddDays(10).AddSeconds(1)));
    }

    [Fact]
    public void Chung_thu_co_HieuLucDen_truoc_HieuLucTu_thi_NEM_LOI()
    {
        Assert.Equal(SigningErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => SigningCertificate.TaoMoi(
                "SER-X", "Cty", "CA", ThoiDiemKy, ThoiDiemKy.AddDays(-1))).Code);
    }

    [Fact]
    public void Thu_hoi_chung_thu_hai_lan_thi_NEM_LOI()
    {
        var cert = ChungThuConHieuLuc();
        cert.ThuHoi();

        Assert.Equal(SigningErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => cert.ThuHoi()).Code);
    }

    // ── Thiếu thông tin & trạng thái kết thúc ───────────────────────────────

    [Fact]
    public void Thieu_chu_ky_hoac_nguoi_ky_thi_NEM_LOI()
    {
        var yc1 = SigningRequest.TaoYeuCau("HD-1", HashA);
        Assert.Equal(SigningErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => yc1.HoanTatKy(HashA, ChungThuConHieuLuc(), "  ", "A", ThoiDiemKy)).Code);

        var yc2 = SigningRequest.TaoYeuCau("HD-2", HashA);
        Assert.Equal(SigningErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => yc2.HoanTatKy(HashA, ChungThuConHieuLuc(), "Y2g=", "", ThoiDiemKy)).Code);
    }

    [Fact]
    public void KY_HAI_LAN_thi_NEM_LOI()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-1", HashA);
        yeuCau.HoanTatKy(HashA, ChungThuConHieuLuc(), "Y2g=", "A", ThoiDiemKy);

        Assert.Equal(SigningErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(
                () => yeuCau.HoanTatKy(HashA, ChungThuConHieuLuc(), "Y2g2", "B", ThoiDiemKy)).Code);
    }

    [Fact]
    public void Huy_can_LY_DO_va_sau_khi_huy_thi_KHONG_ky_duoc()
    {
        var yeuCau = SigningRequest.TaoYeuCau("HD-1", HashA);

        Assert.Equal(SigningErrorCodes.ThieuLyDo,
            Assert.Throws<BusinessException>(() => yeuCau.Huy("   ")).Code);

        yeuCau.Huy("Sai nội dung, lập lại hoá đơn");
        Assert.Equal(SigningRequestStatus.DaHuy, yeuCau.TrangThai);

        // Đã kết thúc ⇒ không ký được nữa (báo đúng mã "đã kết thúc", không phải lỗi chung).
        Assert.Equal(SigningErrorCodes.DaKetThuc,
            Assert.Throws<BusinessException>(
                () => yeuCau.HoanTatKy(HashA, ChungThuConHieuLuc(), "Y2g=", "A", ThoiDiemKy)).Code);
    }
}
