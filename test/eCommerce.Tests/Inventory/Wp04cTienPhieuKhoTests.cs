using eCommerce.Inventory;
using Volo.Abp;

namespace eCommerce.Tests.Inventory;

/// <summary>
/// 🆕 **`WP-04` (việc 8)** — TIỀN CỦA PHIẾU KHO (`docs/nhanh/D-37`): đơn giá dòng → thành tiền → tổng phiếu.
///
/// 🚨 **VÌ SAO PHẢI ĐO BẰNG SỐ CỤ THỂ:** tiền là chỗ *"hai nơi cùng tính một công thức"* dễ lệch nhất
/// (API, màn hình, bản in). Bộ test này khoá **ba** điều:
/// 1. **Làm tròn ở TỪNG DÒNG** rồi mới cộng ⇒ cột Thành tiền cộng lại **bằng** dòng Tổng (không lệch 1 đồng).
/// 2. **Thiếu giá ≠ giá 0**: dòng không khai đơn giá làm tổng thành `null`, **không** bị cộng như miễn phí.
/// 3. **Đơn giá âm bị chặn** ở cả entity (mã `0050`) và hàm thuần.
/// </summary>
public class Wp04cTienPhieuKhoTests
{
    [Fact]
    public void ThanhTien__3_x_25000_la_75000_va_lam_tron_2_so_le()
    {
        Assert.Equal(75_000m, TinhTienChungTu.ThanhTien(3m, 25_000m));

        // 1,5 kg × 12.345,67 = 18.518,505 ⇒ 18.518,51 (AwayFromZero).
        Assert.Equal(18_518.51m, TinhTienChungTu.ThanhTien(1.5m, 12_345.67m));
    }

    [Fact]
    public void ThanhTien__khong_khai_gia_thi_la_null__KHONG_phai_0()
    {
        // 🚨 Điểm dễ sai nhất: `0` nghĩa là "miễn phí", `null` nghĩa là "chưa khai". Gộp hai thứ là nói dối.
        Assert.Null(TinhTienChungTu.ThanhTien(5m, null));
        Assert.Null(TinhTienChungTu.TongTienHang(new[] { (5m, (decimal?)null) }));
    }

    [Fact]
    public void TongTienHang__cong_TUNG_DONG_DA_LAM_TRON_nen_bang_tong_cac_so_hien_thi()
    {
        // 3 dòng lẻ 0,335 ⇒ mỗi dòng 0,34; cộng 3 = 1,02.
        // Nếu làm tròn SAU khi cộng: 0,335×3 = 1,005 ⇒ 1,01 ⇒ cột Thành tiền và dòng Tổng lệch 1 xu.
        var dong = new[] { (1m, (decimal?)0.335m), (1m, 0.335m), (1m, 0.335m) };

        Assert.Equal(1.02m, TinhTienChungTu.TongTienHang(dong));
    }

    [Fact]
    public void TongTienHang__mot_dong_thieu_gia_lam_TONG_thanh_null_du_cac_dong_khac_co_gia()
    {
        var dong = new[] { (2m, (decimal?)10_000m), (1m, (decimal?)null) };

        Assert.Null(TinhTienChungTu.TongTienHang(dong));
    }

    [Fact]
    public void TongVat__tinh_theo_phan_tram_tung_dong_va_bo_qua_dong_khong_khai_vat()
    {
        var dong = new[]
        {
            (1m, (decimal?)100_000m, (decimal?)8m),   // 8.000
            (2m, (decimal?)50_000m, (decimal?)10m),   // 10.000
            (1m, (decimal?)30_000m, (decimal?)null),  // không khai VAT ⇒ không cộng
        };

        Assert.Equal(18_000m, TinhTienChungTu.TongVat(dong));
    }

    [Fact]
    public void TongVat__khong_dong_nao_khai_vat_thi_la_null__KHONG_phai_0()
    {
        // "Chưa khai" khác "miễn thuế" — hiện `0` trên hoá đơn là khẳng định sai về nghĩa vụ thuế.
        Assert.Null(TinhTienChungTu.TongVat(new[] { (1m, (decimal?)10_000m, (decimal?)null) }));
    }

    [Fact]
    public void TongVat__dong_co_vat_nhung_thieu_gia_thi_TONG_la_null()
    {
        Assert.Null(TinhTienChungTu.TongVat(new[] { (1m, (decimal?)null, (decimal?)8m) }));
    }

    [Fact]
    public void TongThanhToan__tien_hang_cong_vat_tru_chiet_khau()
    {
        // 100.000 + 8.000 − 5.000 = 103.000
        Assert.Equal(103_000m, TinhTienChungTu.TongThanhToan(100_000m, 8_000m, 5_000m));

        // Không khai VAT ⇒ vẫn tính được (chỉ trừ chiết khấu).
        Assert.Equal(95_000m, TinhTienChungTu.TongThanhToan(100_000m, null, 5_000m));

        // Không biết tổng hàng ⇒ không biết tổng thanh toán (không được đoán).
        Assert.Null(TinhTienChungTu.TongThanhToan(null, 8_000m, 0m));
    }

    [Fact]
    public void Chiet_khau_am__NEM_thay_vi_lang_le_tru_nguoc()
    {
        var loi = Assert.Throws<ArgumentOutOfRangeException>(
            () => TinhTienChungTu.TongThanhToan(100_000m, 0m, -1m));

        Assert.Equal("chietKhau", loi.ParamName);
    }

    [Fact]
    public void Don_gia_am__NEM_o_ham_thuan()
    {
        var loi = Assert.Throws<ArgumentOutOfRangeException>(
            () => TinhTienChungTu.ThanhTien(1m, -1m));

        Assert.Equal("donGia", loi.ParamName);
    }

    [Fact]
    public void Don_gia_am__NEM_o_ENTITY_voi_ma_0050_va_khong_doi_du_lieu_dong()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-TIEN", 2m);

        var loi = Assert.Throws<BusinessException>(() => dong.DatChiTiet(unitPrice: -5_000m));

        Assert.Equal(InventoryErrorCodes.DonGiaKhongHopLe, loi.Code);
        Assert.Null(dong.UnitPrice);
    }

    [Fact]
    public void DatChiTiet__luu_don_gia_va_khong_dung_vao_VAT_serial_khi_chi_khai_gia()
    {
        var dong = new StockDocumentLine(Guid.NewGuid(), Guid.NewGuid(), "SKU-TIEN", 2m);

        dong.DatChiTiet(unitPrice: 12_500m);

        Assert.Equal(12_500m, dong.UnitPrice);
        Assert.Null(dong.VatPercent);
        Assert.Null(dong.ImeiSerials);
        Assert.Equal(25_000m, TinhTienChungTu.ThanhTien(dong.Quantity, dong.UnitPrice));
    }
}
