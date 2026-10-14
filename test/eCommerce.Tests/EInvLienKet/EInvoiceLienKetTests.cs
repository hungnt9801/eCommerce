using eCommerce.EInvoice;
using Volo.Abp;

// ⚠️ ALIAS BẮT BUỘC: namespace `eCommerce.EInvoice` (bao quanh) CHE MẤT class `eCommerce.EInvoice.EInvoice`
// — bẫy đã gặp 3 lần trong dự án (Money, EInvoice, và ở đây).
using EInvoiceEntity = eCommerce.EInvoice.EInvoice;

namespace eCommerce.Tests.EInvLienKet;

/// <summary>
/// Test cho **liên kết thay thế / điều chỉnh** của hoá đơn (`EINV-003`).
///
/// 🎯 Ba bất biến được khoá ở đây:
///   1. **CHIỀU liên kết**: trường nằm trên **HOÁ ĐƠN GỐC** và trỏ tới **bản thay thế/điều chỉnh**.
///      Tài liệu cũ trong code ghi **ngược chiều** ⇒ test này khoá chiều đúng để không ai đảo lại.
///      (Đảo chiều sẽ làm mọi truy vấn "tìm bản thay thế của X" trả về **rỗng** và bị hiểu nhầm là
///      *"chưa thay thế"* — một lỗi im lặng điển hình.)
///   2. **Id liên kết vô nghĩa bị chặn ngay** (`Guid.Empty`, hoặc trỏ tới chính nó) — mã `0108`.
///   3. **Một gốc không thể bị thay thế hai lần** — đã được **máy trạng thái** chặn (không phải việc mới).
/// </summary>
public class EInvoiceLienKetTests
{
    private static readonly DateTime NgayLap = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

    private static EInvoiceEntity HoaDonDaPhatHanh(string soHoaDon = "00000001")
    {
        var hoaDon = EInvoiceEntity.TaoMoi("1C26TAA", soHoaDon, NgayLap);
        hoaDon.Ky("<xml-da-ky/>");
        hoaDon.PhatHanh();
        return hoaDon;
    }

    // ── ① CHIỀU liên kết (khoá để không ai đảo lại) ────────────────────────

    [Fact]
    public void ThayThe_dat_tren_GOC_va_tro_toi_BAN_THAY_THE()
    {
        var goc = HoaDonDaPhatHanh();
        var banThayTheId = Guid.NewGuid();

        goc.ThayThe("Sai mã số thuế người mua", banThayTheId);

        // ⬅️ CHIỀU ĐÚNG: GỐC → BẢN THAY THẾ. Nếu ai đảo chiều, hai dòng dưới sẽ đỏ.
        Assert.Equal(banThayTheId, goc.HoaDonBiThayTheId);
        Assert.Equal(EInvoiceStatus.DaThayThe, goc.Status);
    }

    [Fact]
    public void DieuChinh_dat_tren_GOC_va_tro_toi_BAN_DIEU_CHINH()
    {
        var goc = HoaDonDaPhatHanh();
        var banDieuChinhId = Guid.NewGuid();

        goc.DieuChinh("Điều chỉnh tiền hàng", banDieuChinhId);

        Assert.Equal(banDieuChinhId, goc.HoaDonBiDieuChinhId);
        Assert.Equal(EInvoiceStatus.DaDieuChinh, goc.Status);
    }

    // ── ② Id liên kết vô nghĩa ─────────────────────────────────────────────

    [Fact]
    public void ThayThe_bang_Guid_EMPTY_thi_NEM_LOI_0108()
    {
        var goc = HoaDonDaPhatHanh();

        var ex = Assert.Throws<BusinessException>(() => goc.ThayThe("lý do", Guid.Empty));

        Assert.Equal(EInvoiceErrorCodes.ThieuHoaDonLienKet, ex.Code);
        Assert.Equal("ThayThe", ex.Data["hanhDong"]);
        // ⬅️ Trạng thái KHÔNG đổi: không có chuyện gốc thành "đã thay thế" mà trỏ tới hư không.
        Assert.Equal(EInvoiceStatus.DaPhatHanh, goc.Status);
        Assert.Null(goc.HoaDonBiThayTheId);
    }

    [Fact]
    public void DieuChinh_bang_Guid_EMPTY_thi_NEM_LOI_0108()
    {
        var goc = HoaDonDaPhatHanh();

        var ex = Assert.Throws<BusinessException>(() => goc.DieuChinh("lý do", Guid.Empty));

        Assert.Equal(EInvoiceErrorCodes.ThieuHoaDonLienKet, ex.Code);
        Assert.Equal(EInvoiceStatus.DaPhatHanh, goc.Status);
    }

    [Fact]
    public void Hoa_don_KHONG_the_thay_the_CHINH_NO()
    {
        var goc = HoaDonDaPhatHanh();

        var ex = Assert.Throws<BusinessException>(() => goc.ThayThe("lý do", goc.Id));

        Assert.Equal(EInvoiceErrorCodes.ThieuHoaDonLienKet, ex.Code);
        Assert.Contains("chính nó", ex.Data["lyDo"]!.ToString());
    }

    [Fact]
    public void Hoa_don_KHONG_the_dieu_chinh_CHINH_NO()
    {
        var goc = HoaDonDaPhatHanh();

        var ex = Assert.Throws<BusinessException>(() => goc.DieuChinh("lý do", goc.Id));

        Assert.Equal(EInvoiceErrorCodes.ThieuHoaDonLienKet, ex.Code);
    }

    // ── ③ Một gốc không bị thay thế hai lần (đã có sẵn — ghi lại để không mất) ──

    [Fact]
    public void Goc_KHONG_the_bi_thay_the_HAI_LAN()
    {
        var goc = HoaDonDaPhatHanh();
        goc.ThayThe("lần 1", Guid.NewGuid());

        var ex = Assert.Throws<BusinessException>(() => goc.ThayThe("lần 2", Guid.NewGuid()));

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
        // ⇒ chiều "một gốc ↔ một bản thay thế" đã được máy trạng thái bảo đảm.
        //   Chiều NGƯỢC LẠI (một bản thay thế bị hai gốc cùng trỏ tới) do RÀNG BUỘC DB chặn
        //   — xem `IX_EInvInvoices_TenantId_HoaDonBiThayTheId` và test tích hợp tương ứng.
    }

    [Fact]
    public void Goc_CHUA_phat_hanh_thi_KHONG_thay_the_duoc()
    {
        var nhap = EInvoiceEntity.TaoMoi("1C26TAA", "00000099", NgayLap);

        var ex = Assert.Throws<BusinessException>(() => nhap.ThayThe("lý do", Guid.NewGuid()));

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    [Fact]
    public void ThayThe_thieu_LY_DO_thi_NEM_LOI()
    {
        var goc = HoaDonDaPhatHanh();

        var ex = Assert.Throws<BusinessException>(() => goc.ThayThe("   ", Guid.NewGuid()));

        Assert.Equal(EInvoiceErrorCodes.ThieuLyDo, ex.Code);
    }

    [Fact]
    public void Huy_hoa_don_DA_THAY_THE_thi_bi_chan()
    {
        // Bất biến: hoá đơn đã thay thế thì không huỷ thêm — huỷ thêm sẽ làm HAI chứng từ cùng "xoá"
        // một nghĩa vụ ⇒ sổ sách không đối chiếu được.
        var goc = HoaDonDaPhatHanh();
        goc.ThayThe("lý do", Guid.NewGuid());

        var ex = Assert.Throws<BusinessException>(() => goc.Huy("huỷ thêm"));

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }
}
