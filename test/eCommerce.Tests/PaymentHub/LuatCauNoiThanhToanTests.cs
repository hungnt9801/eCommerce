using eCommerce.PaymentHub.Payments;

namespace eCommerce.Tests.PaymentHub;

/// <summary>
/// 🚨 **LUẬT CẦU NỐI: SỐ TIỀN CỦA CỔNG PHẢI KHỚP SỐ TIỀN ĐÃ LẬP GIAO DỊCH** (`S4` — đóng `F-PAY-2`).
///
/// **VÌ SAO PHẢI CÓ PHÉP SO NÀY (chữ ký HMAC **không** thay thế được nó):** chữ ký chỉ chứng minh payload
/// **không bị sửa** — nó **không** chứng minh payload thuộc **đúng đơn này** với **đúng số tiền**. Hai ca thật:
/// <list type="bullet">
///   <item>`HashSecret` **lệch** giữa hai môi trường (sandbox vs thật) ⇒ một IPN của đơn khác vẫn "hợp lệ";</item>
///   <item>lỗi sinh mã khiến hai đơn dùng **cùng** `vnp_TxnRef` ⇒ IPN khớp **dòng đầu**, dòng còn lại vĩnh viễn
///     `Initiated` **trong khi tiền đã vào**.</item>
/// </list>
/// 📌 Và `SAL-003` **#12** yêu cầu **khớp số tiền** mới được `Paid` ⇒ đây là điều kiện, không phải "kiểm thêm".
/// 🔒 Hai chiều đều được kiểm (lệch **cao hơn** và **thấp hơn**): một phép so chỉ bắt một chiều là cổng nửa vời.
/// </summary>
public class LuatCauNoiThanhToanTests
{
    [Fact]
    public void Khop_so_tien_va_tien_te__KHOP()
    {
        Assert.Equal(
            KetQuaDoiChieu.Khop,
            LuatCauNoiThanhToan.DoiChieu(250_000m, "VND", 250_000m, "VND"));
    }

    [Theory]
    [InlineData(250_001)]
    [InlineData(249_999)]
    public void Lech_DU_MOT_DONG__LECH_SO_TIEN__ca_hai_chieu(decimal soTienCong)
    {
        Assert.Equal(
            KetQuaDoiChieu.LechSoTien,
            LuatCauNoiThanhToan.DoiChieu(soTienCong, "VND", 250_000m, "VND"));
    }

    [Fact]
    public void Khac_tien_te__LECH_TIEN_TE__du_so_tien_bang_nhau()
    {
        // 🔒 Tiền tệ phải được so **TRƯỚC** số tiền: 100 USD và 100 VND "khớp số" nhưng **sai bản chất**.
        Assert.Equal(
            KetQuaDoiChieu.LechTienTe,
            LuatCauNoiThanhToan.DoiChieu(100m, "USD", 100m, "VND"));
    }

    [Fact]
    public void Tien_te_rong__coi_la_VND__nen_KHOP()
    {
        // Cổng không khai `vnp_CurrCode` là ca **thật** (một số provider VN không gửi tiền tệ).
        Assert.Equal(
            KetQuaDoiChieu.Khop,
            LuatCauNoiThanhToan.DoiChieu(250_000m, null, 250_000m, "VND"));
        Assert.Equal(
            KetQuaDoiChieu.Khop,
            LuatCauNoiThanhToan.DoiChieu(250_000m, "  ", 250_000m, null));
    }

    [Fact]
    public void Tien_te_khac_hoa_thuong__van_KHOP()
    {
        Assert.Equal(
            KetQuaDoiChieu.Khop,
            LuatCauNoiThanhToan.DoiChieu(250_000m, "vnd", 250_000m, "VND"));
    }

    [Fact]
    public void Bon_chu_so_thap_phan__KHONG_lam_lech_ket_qua()
    {
        // Cột là `numeric(18,4)` ⇒ giá trị đọc lên có thể là `250000.0000`. So sánh phải theo **giá trị**,
        // không theo **chuỗi** (`"250000"` ≠ `"250000.0000"` nếu ai đó so chuỗi).
        Assert.Equal(
            KetQuaDoiChieu.Khop,
            LuatCauNoiThanhToan.DoiChieu(250_000.0000m, "VND", 250_000m, "VND"));
    }

    [Fact]
    public void So_tien_rat_lon__van_so_dung()
    {
        // `decimal` chứ không `double`: 9999999999.9999 là giá trị **hợp lệ** của `numeric(18,4)`.
        // Nếu ai đó đổi sang `double` thì phép so quanh ngưỡng này sẽ sai vài đồng — và sai tiền là sai thật.
        Assert.Equal(
            KetQuaDoiChieu.Khop,
            LuatCauNoiThanhToan.DoiChieu(9_999_999_999.9999m, "VND", 9_999_999_999.9999m, "VND"));
    }
}
