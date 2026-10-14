using eCommerce.EInvoice;
using Volo.Abp;

namespace eCommerce.Tests.EInvoiceResubmission;

/// <summary>
/// Test cho **chính sách gửi lại cơ quan thuế** (mục (3)).
///
/// 🎯 Hai thái cực sai đều tốn kém: gửi lại **vô điều kiện** ⇒ hoá đơn lỗi vĩnh viễn bị gửi mãi mãi
/// (rác log, có thể bị CQT chặn tần suất); **không** gửi lại ⇒ hoá đơn hợp lệ nhưng lỗi tạm thời
/// **mắc kẹt vĩnh viễn**. Bộ test khoá cả hai phía.
/// </summary>
public class EInvoiceResubmissionPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    private static EInvoiceResubmissionPolicy.KetQua DanhGia(
        TaxAuthorityStatus trangThai = TaxAuthorityStatus.BiTuChoi,
        string? maLoi = "5",
        int soLan = 1,
        DateTime? lanCuoi = null)
        => EInvoiceResubmissionPolicy.DanhGia(trangThai, maLoi, soLan, lanCuoi, Now);

    [Fact]
    public void Loi_TAM_THOI_con_luot_va_du_khoang_cach_thi_GUI_LAI()
    {
        var kq = DanhGia(maLoi: "5", soLan: 1, lanCuoi: Now.AddHours(-1));

        Assert.True(kq.NenGuiLai);
        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.DuDieuKien, kq.LyDo);
    }

    [Fact]
    public void Chua_tung_gui_thi_khong_vuong_khoang_cach()
    {
        var kq = DanhGia(lanCuoi: null);

        Assert.True(kq.NenGuiLai);
    }

    [Fact]
    public void Khong_phai_dang_bi_tu_choi_thi_KHONG_gui_lai()
    {
        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.KhongPhaiDangBiTuChoi,
            DanhGia(trangThai: TaxAuthorityStatus.DaNhan).LyDo);

        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.KhongPhaiDangBiTuChoi,
            DanhGia(trangThai: TaxAuthorityStatus.ChuaGui).LyDo);
    }

    [Fact]
    public void HET_LUOT_THU_thi_DUNG_tu_dong()
    {
        // Ngưỡng thử = 5 (chốt XC-002 Q3).
        Assert.True(DanhGia(soLan: 4, lanCuoi: Now.AddHours(-1)).NenGuiLai);

        var kq = DanhGia(soLan: 5, lanCuoi: Now.AddHours(-1));
        Assert.False(kq.NenGuiLai);
        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.HetLuotThu, kq.LyDo);
    }

    [Fact]
    public void LOI_VINH_VIEN_thi_KHONG_gui_lai_tu_dong()
    {
        // 🚨 Gửi lại y nguyên nội dung sẽ hỏng y nguyên ⇒ phải có người sửa.
        var kq = DanhGia(maLoi: "3", soLan: 1, lanCuoi: Now.AddHours(-1));

        Assert.False(kq.NenGuiLai);
        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.LoiVinhVien, kq.LyDo);
        Assert.Contains("phải sửa nội dung", kq.ThongDiep);
    }

    [Fact]
    public void Ma_loi_null_duoc_coi_la_loi_VINH_VIEN_khong_doan_bua()
    {
        // Không biết mã lỗi ⇒ KHÔNG tự động gửi lại (an toàn hơn là gửi mù).
        var kq = DanhGia(maLoi: null, lanCuoi: Now.AddHours(-1));

        Assert.False(kq.NenGuiLai);
        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.LoiVinhVien, kq.LyDo);
    }

    [Fact]
    public void CHUA_DU_KHOANG_CACH_thi_cho()
    {
        var kq = DanhGia(lanCuoi: Now.AddMinutes(-5));   // mới 5 phút, cần 15

        Assert.False(kq.NenGuiLai);
        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.ChuaDuKhoangCach, kq.LyDo);
    }

    [Fact]
    public void DUNG_bang_khoang_cach_thi_duoc_gui()
    {
        var kq = DanhGia(lanCuoi: Now - EInvoiceResubmissionPolicy.KhoangCachToiThieu);

        Assert.True(kq.NenGuiLai);
    }

    [Fact]
    public void Uu_tien_kiem_loi_VINH_VIEN_truoc_khi_kiem_khoang_cach()
    {
        // Cả hai điều kiện đều không đạt ⇒ phải báo lý do QUAN TRỌNG HƠN (lỗi vĩnh viễn),
        // vì đó là việc cần người xử lý, không phải chỉ chờ thêm.
        var kq = DanhGia(maLoi: "3", soLan: 1, lanCuoi: Now.AddSeconds(-10));

        Assert.Equal(EInvoiceResubmissionPolicy.LyDo.LoiVinhVien, kq.LyDo);
    }

    [Fact]
    public void ThongDiep_neu_ro_lan_thu_tiep_theo()
    {
        var kq = DanhGia(soLan: 2, lanCuoi: Now.AddHours(-1));

        Assert.Contains("3/5", kq.ThongDiep);
    }
}
