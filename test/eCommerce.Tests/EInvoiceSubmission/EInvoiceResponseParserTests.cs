using eCommerce.EInvoice;
using Volo.Abp;

namespace eCommerce.Tests.EInvoiceSubmission;

/// <summary>
/// Test cho **phân tích phản hồi cơ quan thuế** (`EINV-002`).
///
/// 🎯 Lỗi im lặng nguy hiểm nhất: coi **"không nhận dạng được" là thành công** ⇒ hệ thống đánh dấu
/// hoá đơn **đã gửi xong** trong khi **CQT chưa nhận** ⇒ hoá đơn **không được cấp mã** mà kế toán
/// tưởng là xong. Vì vậy lớp này **FAIL-CLOSED**: chỉ `OK:` **có mã** mới là thành công.
/// </summary>
public class EInvoiceResponseParserTests
{
    [Theory]
    [InlineData("OK:MCQT-123")]
    [InlineData("ok:MCQT-123")]
    [InlineData("  OK:MCQT-123  ")]
    [InlineData("OK: MCQT-123")]
    public void OK_co_ma_thi_THANH_CONG(string phanHoi)
    {
        var kq = EInvoiceResponseParser.PhanTich(phanHoi);

        Assert.True(kq.ThanhCong);
        Assert.Equal("MCQT-123", kq.MaCQT);
        Assert.Null(kq.MaLoi);
    }

    [Fact]
    public void OK_kem_mo_ta_thi_tach_duoc()
    {
        var kq = EInvoiceResponseParser.PhanTich("OK:MCQT-1:Hoá đơn đã được cấp mã");

        Assert.True(kq.ThanhCong);
        Assert.Equal("MCQT-1", kq.MaCQT);
        Assert.Equal("Hoá đơn đã được cấp mã", kq.MoTa);
    }

    [Theory]
    [InlineData("ERR:5", "5")]
    [InlineData("err:5", "5")]
    [InlineData("ERR: 5", "5")]
    public void ERR_thi_THAT_BAI_va_giu_ma_loi(string phanHoi, string maLoiMongDoi)
    {
        var kq = EInvoiceResponseParser.PhanTich(phanHoi);

        Assert.False(kq.ThanhCong);
        Assert.Equal(maLoiMongDoi, kq.MaLoi);
        Assert.Null(kq.MaCQT);
    }

    [Fact]
    public void ERR_kem_mo_ta_thi_tach_duoc()
    {
        var kq = EInvoiceResponseParser.PhanTich("ERR:5:Sai định dạng XML");

        Assert.False(kq.ThanhCong);
        Assert.Equal("5", kq.MaLoi);
        Assert.Equal("Sai định dạng XML", kq.MoTa);
    }

    // ── 🚨 Các nhánh FAIL-CLOSED (không được coi là thành công) ────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Phan_hoi_RONG_thi_NEM_LOI(string? phanHoi)
    {
        var ex = Assert.Throws<BusinessException>(() => EInvoiceResponseParser.PhanTich(phanHoi));
        Assert.Equal(EInvoiceSubmissionErrorCodes.PhanHoiRong, ex.Code);
    }

    [Theory]
    [InlineData("OK:")]
    [InlineData("OK:   ")]
    public void OK_THIEU_MA_thi_coi_la_LOI_chu_khong_phai_thanh_cong(string phanHoi)
    {
        // `OK:` mà KHÔNG có mã ⇒ không thể đối chiếu về sau ⇒ KHÔNG được coi là thành công.
        var ex = Assert.Throws<BusinessException>(() => EInvoiceResponseParser.PhanTich(phanHoi));
        Assert.Equal(EInvoiceSubmissionErrorCodes.OkThieuMaCQT, ex.Code);
    }

    [Fact]
    public void OK_khong_co_dau_hai_cham_la_dinh_dang_KHONG_nhan_dang_duoc()
    {
        // ⚠️ Sửa kỳ vọng của test (không phải sửa code): `"OK"` thiếu dấu `:` là **định dạng lạ**,
        // nên rơi vào nhánh "không nhận dạng được" — vẫn FAIL-CLOSED, chỉ khác mã lỗi.
        // Ban đầu tôi gộp `"OK"` chung với `"OK:"` nên test đỏ; hành vi của code là ĐÚNG.
        var ex = Assert.Throws<BusinessException>(() => EInvoiceResponseParser.PhanTich("OK"));
        Assert.Equal(EInvoiceSubmissionErrorCodes.PhanHoiKhongNhanDangDuoc, ex.Code);
    }

    [Theory]
    [InlineData("SUCCESS")]
    [InlineData("200 OK")]
    [InlineData("<xml>ket qua</xml>")]
    [InlineData("Thành công")]
    [InlineData("0")]
    public void Chuoi_KHONG_nhan_dang_duoc_thi_NEM_LOI(string phanHoi)
    {
        // 🚨 Nhánh quan trọng nhất: KHÔNG được mặc định là thành công.
        var ex = Assert.Throws<BusinessException>(() => EInvoiceResponseParser.PhanTich(phanHoi));
        Assert.Equal(EInvoiceSubmissionErrorCodes.PhanHoiKhongNhanDangDuoc, ex.Code);
    }

    // ── Phân loại lỗi tạm thời ──────────────────────────────────────────────

    [Theory]
    [InlineData("5", true)]
    [InlineData("TIMEOUT", true)]
    [InlineData("timeout", true)]
    [InlineData("3", false)]
    [InlineData(null, false)]
    public void Nhan_dien_loi_TAM_THOI(string? maLoi, bool mongDoi)
    {
        Assert.Equal(mongDoi, EInvoiceResponseParser.LaLoiTamThoi(maLoi));
    }
}
