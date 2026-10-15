using eCommerce.Kernel.Money;
using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// Test **tính tiền đơn** (`PRC-002` B6/B7/B8 + **4 điểm làm tròn R1–R4**) — hàm **THUẦN**.
///
/// 🚨 **VÌ SAO BỘ TEST NÀY QUAN TRỌNG NHẤT TRONG PRICING:** đây là chỗ **mất/thừa tiền thật**.
/// Sai 1–2 đồng ở đây ⇒ **hoá đơn không khớp**, khách khiếu nại, kế toán không đối soát được —
/// và **không có lỗi biên dịch, không exception** nào chỉ ra.
/// </summary>
public class TinhTienDonTests
{
    private static readonly IRoundingPolicy Tron = new RoundHalfAwayFromZeroPolicy();

    private static DongCanTinh Dong(string sku, int sl, decimal gia, decimal thue = 0m)
        => new(sku, sl, Money.Vnd(gia), thue);

    // ── R1: làm tròn ĐƠN GIÁ ─────────────────────────────────────────────────

    [Fact]
    public void R1_don_gia_duoc_lam_tron_ve_so_chu_so_cua_tien_te()
    {
        // VND = 0 chữ số thập phân ⇒ 12.345,6 ⇒ 12.346. `OrderLine.UnitPrice` là `numeric(18,4)` nên nếu KHÔNG
        // làm tròn thì đơn giá lẻ vẫn lọt vào đơn ⇒ hoá đơn có số lẻ mà không ai định giá như vậy.
        var kq = TinhTienDon.Tinh([Dong("SKU-1", 1, 12_345.6m)], Money.Vnd(0), Money.Vnd(0), Tron);

        Assert.Equal(12_346m, kq.CacDong[0].DonGiaSauLamTron.Amount);
    }

    [Fact]
    public void R1_lam_tron_don_gia_ROI_moi_nhan_voi_so_luong()
    {
        // 10.000,5 × 3: nếu NHÂN TRƯỚC rồi mới tròn ⇒ 30.001,5 ⇒ 30.002 (sai).
        // Đúng: tròn đơn giá trước ⇒ 10.001 × 3 = 30.003.
        var kq = TinhTienDon.Tinh([Dong("SKU-1", 3, 10_000.5m)], Money.Vnd(0), Money.Vnd(0), Tron);

        Assert.Equal(10_001m, kq.CacDong[0].DonGiaSauLamTron.Amount);
        Assert.Equal(30_003m, kq.CacDong[0].ThanhTienTruocGiam.Amount);
    }

    // ── R2/R3: PHÂN BỔ giảm giá / phí — BẢO TOÀN TỔNG ────────────────────────

    [Fact]
    public void R2_giam_gia_duoc_phan_bo_va_TONG_phan_bo_BANG_dung_khoan_goc()
    {
        // Chia 100.000 cho 3 dòng KHÔNG chia hết ⇒ nếu làm tròn tổng rồi chia sẽ **mất/thừa 1 đồng**.
        var dong = new List<DongCanTinh>
        {
            Dong("A", 1, 33_333m), Dong("B", 1, 33_333m), Dong("C", 1, 33_334m)
        };

        var kq = TinhTienDon.Tinh(dong, Money.Vnd(10_000m), Money.Vnd(0), Tron);

        Assert.Equal(10_000m, kq.TongGiamGia.Amount);   // 🚨 Σ phần = ĐÚNG khoản gốc
        Assert.Equal(3, kq.CacDong.Count);
    }

    [Fact]
    public void R3_phi_cung_duoc_phan_bo_va_bao_toan_tong()
    {
        var dong = new List<DongCanTinh> { Dong("A", 2, 15_000m), Dong("B", 1, 7_000m) };

        var kq = TinhTienDon.Tinh(dong, Money.Vnd(0), Money.Vnd(5_000m), Tron);

        Assert.Equal(5_000m, kq.TongPhi.Amount);
        Assert.All(kq.CacDong, d => Assert.True(d.PhiDuocPhanBo.Amount > 0m));   // có trọng số ⇒ chia được
    }

    [Fact]
    public void DON_0_DONG_thi_giam_gia_phi_deu_bang_0__khong_nem_loi()
    {
        // Đơn toàn 0 đồng (hàng tặng) mà có phí cấp đơn ⇒ trọng số toàn 0 ⇒ trả 0 thay vì ném lỗi khó hiểu.
        var kq = TinhTienDon.Tinh([Dong("A", 1, 0m)], Money.Vnd(1_000m), Money.Vnd(2_000m), Tron);

        Assert.Equal(0m, kq.TongGiamGia.Amount);
        Assert.Equal(0m, kq.TongPhi.Amount);
    }

    // ── R4: THUẾ THEO TỪNG DÒNG ──────────────────────────────────────────────

    [Fact]
    public void R4_thue_tinh_THEO_TUNG_DONG_va_tong_thue_la_TONG_cac_dong()
    {
        // Hai mức thuế suất khác nhau trên cùng đơn — `XC-001` BR-010: hoá đơn phải khớp `Σ` theo mức thuế suất.
        var dong = new List<DongCanTinh>
        {
            Dong("A", 1, 100_000m, thue: 0.10m),   // 10.000
            Dong("B", 1, 50_000m, thue: 0.08m)     // 4.000
        };

        var kq = TinhTienDon.Tinh(dong, Money.Vnd(0), Money.Vnd(0), Tron);

        Assert.Equal(10_000m, kq.CacDong[0].Thue.Amount);
        Assert.Equal(4_000m, kq.CacDong[1].Thue.Amount);
        Assert.Equal(14_000m, kq.TongThue.Amount);
    }

    [Fact]
    public void R4_thue_lam_tron_TUNG_DONG__khong_lam_tron_tong_thue()
    {
        // 3 dòng, mỗi dòng thuế 50,5 ⇒ mỗi dòng làm tròn lên 51 ⇒ tổng 153.
        // Nếu làm tròn TỔNG: 151,5 ⇒ 152 ⇒ LỆCH 1 ĐỒNG so với Σ dòng ⇒ hoá đơn không khớp.
        var dong = new List<DongCanTinh>
        {
            Dong("A", 1, 1_010m, thue: 0.05m),
            Dong("B", 1, 1_010m, thue: 0.05m),
            Dong("C", 1, 1_010m, thue: 0.05m)
        };

        var kq = TinhTienDon.Tinh(dong, Money.Vnd(0), Money.Vnd(0), Tron);

        Assert.All(kq.CacDong, d => Assert.Equal(51m, d.Thue.Amount));   // 50,5 ⇒ 51
        Assert.Equal(153m, kq.TongThue.Amount);
    }

    [Fact]
    public void R4_DOI_CHUNG_neu_lam_tron_TONG_thi_ra_152__khac_153()
    {
        // Đối chứng số học cho luật R4: chứng minh hai cách làm tròn CHO KẾT QUẢ KHÁC NHAU ⇒ luật không vô nghĩa.
        var tongChuaTron = 50.5m + 50.5m + 50.5m;

        Assert.Equal(151.5m, tongChuaTron);
        Assert.Equal(152m, Tron.Round(Money.Vnd(tongChuaTron)).Amount);   // cách SAI (làm tròn tổng)
        Assert.Equal(153m, Tron.Round(Money.Vnd(50.5m)).Amount * 3);      // cách ĐÚNG (Σ dòng đã tròn)
    }

    // ── ADR-06: TỔNG = Σ DÒNG ĐÃ LÀM TRÒN ────────────────────────────────────

    [Fact]
    public void TONG_thanh_toan_BANG_dung_tong_cac_dong_da_lam_tron()
    {
        var dong = new List<DongCanTinh>
        {
            Dong("A", 3, 10_000.4m, thue: 0.10m),
            Dong("B", 1, 7_777.7m, thue: 0.08m)
        };

        var kq = TinhTienDon.Tinh(dong, Money.Vnd(3_333m), Money.Vnd(1_111m), Tron);

        var tuCacDong = kq.CacDong.Aggregate(
            Money.Zero(Currency.Vnd), (a, d) => a + d.ThanhTienSauGiam + d.Thue);

        Assert.Equal(tuCacDong.Amount, kq.TongThanhToan.Amount);
        Assert.Equal(2, kq.SoDong);
    }

    [Fact]
    public void Tong_truoc_giam_BANG_tong_thanh_tien_tung_dong()
    {
        var kq = TinhTienDon.Tinh(
            [Dong("A", 2, 10_000m), Dong("B", 5, 3_000m)], Money.Vnd(0), Money.Vnd(0), Tron);

        Assert.Equal(20_000m + 15_000m, kq.TongTruocGiam.Amount);
    }

    // ── ADR-08: chính sách làm tròn được GHI LẠI để giải trình ───────────────

    [Fact]
    public void Ket_qua_PHAI_chua_ma_chinh_sach_lam_tron__ADR08()
    {
        var kq = TinhTienDon.Tinh([Dong("A", 1, 1_000m)], Money.Vnd(0), Money.Vnd(0), Tron);

        Assert.Equal(Tron.Ten, kq.MaChinhSachLamTron);
    }

    [Fact]
    public void Chinh_sach_KHONG_lam_tron_thi_don_gia_giu_nguyen_so_le()
    {
        var kq = TinhTienDon.Tinh(
            [Dong("A", 1, 12_345.6789m)], Money.Vnd(0), Money.Vnd(0), new NoRoundingPolicy());

        Assert.Equal(12_345.6789m, kq.CacDong[0].DonGiaSauLamTron.Amount);
    }

    // ── Bất biến đầu vào ─────────────────────────────────────────────────────

    [Fact]
    public void Don_RONG_thi_NEM()
    {
        Assert.Throws<ArgumentException>(
            () => TinhTienDon.Tinh([], Money.Vnd(0), Money.Vnd(0), Tron));
    }

    [Fact]
    public void Quantity_nho_hon_1_thi_NEM__KHONG_im_lang_coi_la_1()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TinhTienDon.Tinh([Dong("A", 0, 1_000m)], Money.Vnd(0), Money.Vnd(0), Tron));
    }

    [Fact]
    public void TRON_loai_tien_thi_NEM__cam_tu_quy_doi()
    {
        var dong = new List<DongCanTinh>
        {
            new("A", 1, Money.Vnd(1_000m), 0m),
            new("B", 1, Money.Usd(10m), 0m)      // 🚨 khác loại tiền
        };

        Assert.Throws<ArgumentException>(() => TinhTienDon.Tinh(dong, Money.Vnd(0), Money.Vnd(0), Tron));
    }

    [Fact]
    public void Giam_gia_cap_don_LECH_loai_tien_thi_NEM()
    {
        Assert.Throws<ArgumentException>(() => TinhTienDon.Tinh(
            [Dong("A", 1, 1_000m)], Money.Usd(1m), Money.Vnd(0), Tron));
    }
}
