using eCommerce.EInvoice;
using Volo.Abp;

// ⚠️ ALIAS BẮT BUỘC: namespace `eCommerce.EInvoice` (bao quanh) CHE MẤT class `eCommerce.EInvoice.EInvoice`
// khi viết `EInvoiceEntity.TaoMoi(...)`. Theo quy tắc C#, namespace ở phạm vi bao ngoài được ưu tiên hơn
// type nhập bằng `using`. Alias dưới đây khử mơ hồ đó.
using EInvoiceEntity = eCommerce.EInvoice.EInvoice;

namespace eCommerce.Tests.EInvoiceStateMachine;

/// <summary>
/// Test cho **`EINV-001`** — state machine **2 trục** của hoá đơn điện tử (mục (e)).
///
/// 🎯 Vì sao đây là phần đáng làm trước: hoá đơn là **chứng từ pháp lý**. Một chuyển trạng thái sai
/// (phát hành hoá đơn chưa ký, huỷ hoá đơn đã thay thế) không làm chương trình lỗi — nó làm
/// **sổ sách sai** và **phải làm lại tờ khai**. Vì vậy mọi chuyển sai phải **NÉM LỖI** (quy ước #11).
/// </summary>
public class EInvoiceStateMachineTests
{
    private static EInvoiceEntity TaoHoaDon()
        => EInvoiceEntity.TaoMoi("1C26TAA", "0000001", new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));

    private static EInvoiceEntity HoaDonDaPhatHanh()
    {
        var hd = TaoHoaDon();
        hd.Ky("<xml-da-ky/>");
        hd.PhatHanh();
        return hd;
    }

    // ── Trạng thái khởi tạo ─────────────────────────────────────────────────

    [Fact]
    public void Hoa_don_moi_o_trang_thai_NHAP_va_CHUA_GUI_CQT()
    {
        var hd = TaoHoaDon();

        Assert.Equal(EInvoiceStatus.Nhap, hd.Status);
        Assert.Equal(TaxAuthorityStatus.ChuaGui, hd.TaxAuthorityStatus);
        Assert.Null(hd.MaCQT);
    }

    [Fact]
    public void Thieu_so_hoac_ky_hieu_thi_NEM_LOI()
    {
        Assert.Equal(EInvoiceErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => EInvoiceEntity.TaoMoi("", "1", DateTime.UtcNow)).Code);
        Assert.Equal(EInvoiceErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => EInvoiceEntity.TaoMoi("1C26TAA", "  ", DateTime.UtcNow)).Code);
    }

    // ── 🚨 Ràng buộc pháp lý quan trọng nhất: phải KÝ trước khi PHÁT HÀNH ──

    [Fact]
    public void KHONG_phat_hanh_duoc_hoa_don_CHUA_KY()
    {
        var hd = TaoHoaDon();

        var ex = Assert.Throws<BusinessException>(() => hd.PhatHanh());

        Assert.Equal(EInvoiceErrorCodes.ChuaKyKhongDuocPhatHanh, ex.Code);
        Assert.Equal(EInvoiceStatus.Nhap, hd.Status);   // ⬅️ trạng thái KHÔNG đổi khi thao tác bị từ chối
    }

    [Fact]
    public void Ky_roi_moi_phat_hanh_duoc()
    {
        var hd = TaoHoaDon();

        hd.Ky("<xml-da-ky/>");
        Assert.Equal(EInvoiceStatus.DaKy, hd.Status);

        hd.PhatHanh();
        Assert.Equal(EInvoiceStatus.DaPhatHanh, hd.Status);
    }

    [Fact]
    public void Ky_hai_lan_thi_NEM_LOI()
    {
        var hd = TaoHoaDon();
        hd.Ky("<xml/>");

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => hd.Ky("<xml2/>")).Code);
    }

    [Fact]
    public void Ky_voi_XML_rong_thi_NEM_LOI()
    {
        Assert.Equal(EInvoiceErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => TaoHoaDon().Ky("   ")).Code);
    }

    // ── Hai trục ĐỘC LẬP ────────────────────────────────────────────────────

    [Fact]
    public void Chua_phat_hanh_thi_KHONG_gui_duoc_CQT()
    {
        var hd = TaoHoaDon();
        hd.Ky("<xml/>");

        var ex = Assert.Throws<BusinessException>(() => hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(EInvoiceErrorCodes.ChuaPhatHanhKhongDuocGuiCQT, ex.Code);
    }

    [Fact]
    public void Chua_gui_thi_KHONG_nhan_duoc_ket_qua_CQT()
    {
        var hd = HoaDonDaPhatHanh();

        Assert.Equal(EInvoiceErrorCodes.ChuaGuiKhongTheNhanKetQua,
            Assert.Throws<BusinessException>(() => hd.NhanKetQuaCQT("MCQT-1")).Code);
    }

    [Fact]
    public void Luong_gui_va_nhan_ket_qua_CQT()
    {
        var hd = HoaDonDaPhatHanh();

        hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));
        Assert.Equal(TaxAuthorityStatus.DaGui, hd.TaxAuthorityStatus);

        hd.NhanKetQuaCQT("MCQT-123");
        Assert.Equal(TaxAuthorityStatus.DaNhan, hd.TaxAuthorityStatus);
        Assert.Equal("MCQT-123", hd.MaCQT);
    }

    [Fact]
    public void CQT_TU_CHOI_thi_KHONG_doi_TRUC_PHAT_HANH()
    {
        // 🚩 Điểm quan trọng nhất của thiết kế 2 trục: hoá đơn ĐÃ phát hành cho khách,
        // nhưng CQT từ chối — CẢ HAI sự thật đều phải giữ.
        var hd = HoaDonDaPhatHanh();
        hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));

        hd.BiTuChoiCQT("ERR:5");

        Assert.Equal(TaxAuthorityStatus.BiTuChoi, hd.TaxAuthorityStatus);
        Assert.Equal("ERR:5", hd.LoiCQT);
        Assert.Equal(EInvoiceStatus.DaPhatHanh, hd.Status);   // ⬅️ trục 1 KHÔNG đổi
    }

    [Fact]
    public void Gui_ket_qua_CQT_rong_thi_NEM_LOI()
    {
        var hd = HoaDonDaPhatHanh();
        hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(EInvoiceErrorCodes.ThieuThongTinBatBuoc,
            Assert.Throws<BusinessException>(() => hd.NhanKetQuaCQT("  ")).Code);
    }

    [Fact]
    public void Chi_duoc_GUI_LAI_khi_dang_bi_tu_choi()
    {
        var hd = HoaDonDaPhatHanh();

        // Chưa gửi ⇒ không "gửi lại" được
        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => hd.ChoPhepGuiLaiCQT()).Code);

        hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));
        hd.BiTuChoiCQT("ERR:1");
        hd.ChoPhepGuiLaiCQT();

        Assert.Equal(TaxAuthorityStatus.ChuaGui, hd.TaxAuthorityStatus);
        // Lỗi cũ được xoá khi gửi lại (không giữ lỗi đã xử lý).
        hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));
        Assert.Null(hd.LoiCQT);
    }

    // ── Điều chỉnh / thay thế / huỷ ─────────────────────────────────────────

    [Fact]
    public void Dieu_chinh_can_LY_DO()
    {
        var hd = HoaDonDaPhatHanh();

        Assert.Equal(EInvoiceErrorCodes.ThieuLyDo,
            Assert.Throws<BusinessException>(() => hd.DieuChinh("  ", Guid.NewGuid())).Code);

        hd.DieuChinh("Sai mã số thuế người mua", Guid.NewGuid());
        Assert.Equal(EInvoiceStatus.DaDieuChinh, hd.Status);
        Assert.Equal("Sai mã số thuế người mua", hd.LyDo);
    }

    [Fact]
    public void Thay_the_can_LY_DO()
    {
        var hd = HoaDonDaPhatHanh();

        Assert.Equal(EInvoiceErrorCodes.ThieuLyDo,
            Assert.Throws<BusinessException>(() => hd.ThayThe("", Guid.NewGuid())).Code);

        hd.ThayThe("Sai mặt hàng", Guid.NewGuid());
        Assert.Equal(EInvoiceStatus.DaThayThe, hd.Status);
    }

    [Fact]
    public void KHONG_dieu_chinh_duoc_hoa_don_CHUA_phat_hanh()
    {
        var hd = TaoHoaDon();
        hd.Ky("<xml/>");

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => hd.DieuChinh("lý do", Guid.NewGuid())).Code);
    }

    [Fact]
    public void Huy_duoc_tu_DaKy_hoac_DaPhatHanh_va_can_LY_DO()
    {
        var hd1 = TaoHoaDon();
        hd1.Ky("<xml/>");
        Assert.Equal(EInvoiceErrorCodes.ThieuLyDo, Assert.Throws<BusinessException>(() => hd1.Huy("")).Code);
        hd1.Huy("Huỷ do lập sai");
        Assert.Equal(EInvoiceStatus.DaHuy, hd1.Status);

        var hd2 = HoaDonDaPhatHanh();
        hd2.Huy("Huỷ theo thoả thuận");
        Assert.Equal(EInvoiceStatus.DaHuy, hd2.Status);
    }

    [Fact]
    public void KHONG_huy_duoc_hoa_don_DA_DIEU_CHINH_hoac_DA_THAY_THE()
    {
        // 🚨 Nếu cho huỷ thì sẽ có HAI chứng từ cùng "xoá" một nghĩa vụ ⇒ sổ sách không đối chiếu được.
        var daDieuChinh = HoaDonDaPhatHanh();
        daDieuChinh.DieuChinh("lý do", Guid.NewGuid());
        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => daDieuChinh.Huy("lý do")).Code);

        var daThayThe = HoaDonDaPhatHanh();
        daThayThe.ThayThe("lý do", Guid.NewGuid());
        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => daThayThe.Huy("lý do")).Code);
    }

    [Fact]
    public void DA_HUY_la_trang_thai_KET_THUC_khong_thao_tac_duoc_nua()
    {
        var hd = HoaDonDaPhatHanh();
        hd.Huy("Huỷ theo thoả thuận");

        // Mọi thao tác sau đó đều phải ném lỗi (hoá đơn đã kết thúc).
        Assert.Equal(EInvoiceErrorCodes.HoaDonDaKetThuc, Assert.Throws<BusinessException>(() => hd.Huy("lần nữa")).Code);
        Assert.Equal(EInvoiceErrorCodes.HoaDonDaKetThuc, Assert.Throws<BusinessException>(() => hd.PhatHanh()).Code);
        Assert.Equal(EInvoiceErrorCodes.HoaDonDaKetThuc, Assert.Throws<BusinessException>(() => hd.Ky("<x/>")).Code);
        Assert.Equal(EInvoiceErrorCodes.HoaDonDaKetThuc, Assert.Throws<BusinessException>(() => hd.DieuChinh("l", Guid.NewGuid())).Code);
    }

    [Fact]
    public void Phat_hanh_hai_lan_thi_NEM_LOI()
    {
        var hd = HoaDonDaPhatHanh();

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => hd.PhatHanh()).Code);
    }

    [Fact]
    public void Gui_CQT_hai_lan_lien_tiep_thi_NEM_LOI()
    {
        var hd = HoaDonDaPhatHanh();
        hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(EInvoiceErrorCodes.ChuyenTrangThaiKhongHopLe,
            Assert.Throws<BusinessException>(() => hd.GuiCQT(new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc))).Code);
    }
}
