using eCommerce.Tax.Mau01;

namespace eCommerce.Tests.Tax;

/// <summary>
/// Test cho **Mẫu 01 — Phần II** theo trả lời của kế toán trưởng ngày 13/10/2026
/// (`XC-006` §15.1.5): **Câu 1 = B** · **Câu 1b = A**.
///
/// 🎯 Vì sao test này quan trọng: đáp án **B** **mâu thuẫn** với công thức in trong biểu mẫu và lệch
/// **6.000.000 đ cho mỗi 100 triệu doanh thu**. Test khoá lại **cả hai** cách hiểu ⇒ khi kế toán chạy
/// phép thử HTKK và đổi ý, ta **đổi 1 tham số**, không phải sửa công thức.
/// </summary>
public class Mau01Part2CalculatorTests
{
    /// <summary>Ví dụ đúng như trong phiếu hỏi: doanh thu 100 triệu · (4) = 10% · (5) = 8%.</summary>
    private static Mau01Part2InputRow DongViDu(decimal doanhThu = 100_000_000m, bool thuocDienGiam = true)
        => new("Hàng hoá A", doanhThu, 0.10m, 0.08m, thuocDienGiam);

    [Fact]
    public void Cau1_B_cot6_bang_doanh_thu_nhan_thue_suat_sau_giam()
    {
        var kq = Mau01Part2Calculator.Tinh([DongViDu()], Mau01Column6Mode.ReducedRateTax);

        var dong = Assert.Single(kq.Rows);
        Assert.Equal(8_000_000m, dong.Cot6);          // 100.000.000 × 8%
        Assert.Equal(8_000_000m, kq.TongCot6);
    }

    [Fact]
    public void Cau1_A_cot6_bang_so_thue_DUOC_GIAM_theo_cong_thuc_trong_mau()
    {
        var kq = Mau01Part2Calculator.Tinh([DongViDu()], Mau01Column6Mode.ReductionAmount);

        var dong = Assert.Single(kq.Rows);
        Assert.Equal(2_000_000m, dong.Cot6);          // 100.000.000 × (10% − 8%)
        Assert.Equal(2_000_000m, kq.TongCot6);
    }

    [Fact]
    public void Hai_cach_hieu_lech_dung_6_trieu_tren_100_trieu_doanh_thu()
    {
        var b = Mau01Part2Calculator.Tinh([DongViDu()], Mau01Column6Mode.ReducedRateTax);
        var a = Mau01Part2Calculator.Tinh([DongViDu()], Mau01Column6Mode.ReductionAmount);

        Assert.Equal(6_000_000m, b.TongCot6 - a.TongCot6);
    }

    [Fact]
    public void Cau1b_A_PhanII_CHI_liet_ke_hang_thuoc_dien_duoc_giam()
    {
        var rows = new[]
        {
            new Mau01Part2InputRow("Hàng 8%", 100_000_000m, 0.10m, 0.08m, ThuocDienGiam: true),
            new Mau01Part2InputRow("Hàng 10%", 50_000_000m, 0.10m, 0.10m, ThuocDienGiam: false),
            new Mau01Part2InputRow("Hàng 5%", 30_000_000m, 0.05m, 0.05m, ThuocDienGiam: false)
        };

        var kq = Mau01Part2Calculator.Tinh(rows, Mau01Column6Mode.ReducedRateTax);

        Assert.Single(kq.Rows);                       // CHỈ hàng 8%
        Assert.Equal("Hàng 8%", kq.Rows[0].TenHangHoa);
        Assert.Equal(100_000_000m, kq.TongCot3);      // tổng cột (3) KHÔNG gồm hàng 10%/5%
    }

    [Fact]
    public void STT_danh_lai_tu_1_lien_tuc()
    {
        var rows = new[]
        {
            new Mau01Part2InputRow("A", 1m, 0.10m, 0.08m, true),
            new Mau01Part2InputRow("Bo qua", 1m, 0.10m, 0.10m, false),
            new Mau01Part2InputRow("B", 1m, 0.10m, 0.08m, true)
        };

        var kq = Mau01Part2Calculator.Tinh(rows, Mau01Column6Mode.ReducedRateTax);

        Assert.Equal([1, 2], kq.Rows.Select(r => r.Stt));
    }

    [Fact]
    public void PhanIII_09_bang_08_tru_06_khi_dung_cach_B()
    {
        var kq = Mau01Part2Calculator.Tinh([DongViDu()], Mau01Column6Mode.ReducedRateTax);

        // [08] = thuế theo quy định = 100tr × 10% = 10.000.000
        Assert.Equal(10_000_000m, kq.TongThueSuatTheoQuyDinh);
        // [09] = [08] − [06] = 10.000.000 − 8.000.000 = 2.000.000  ⇒ ĐÚNG số thuế được giảm
        Assert.Equal(2_000_000m, kq.ChenhLechPhanIII);
    }

    [Fact]
    public void VOI_cach_A_thi_KHONG_tra_09_de_tranh_tru_hai_lan()
    {
        var kq = Mau01Part2Calculator.Tinh([DongViDu()], Mau01Column6Mode.ReductionAmount);

        // (6) đã LÀ số được giảm ⇒ [08] − [06] sẽ trừ hai lần ⇒ phải là null.
        Assert.Null(kq.ChenhLechPhanIII);
    }

    [Fact]
    public void Lam_tron_0_chu_so_thap_phan_nua_len()
    {
        // 416.666.668,75 × 8% = 33.333.333,5 → làm tròn NỬA LÊN (AwayFromZero) = 33.333.334
        var rows = new[] { new Mau01Part2InputRow("Lẻ", 416_666_668.75m, 0.10m, 0.08m, true) };

        var kq = Mau01Part2Calculator.Tinh(rows, Mau01Column6Mode.ReducedRateTax);

        Assert.Equal(33_333_334m, kq.Rows[0].Cot6);
    }

    [Fact]
    public void Danh_sach_rong_khong_lam_vo()
    {
        var kq = Mau01Part2Calculator.Tinh([], Mau01Column6Mode.ReducedRateTax);

        Assert.Empty(kq.Rows);
        Assert.Equal(0m, kq.TongCot3);
        Assert.Equal(0m, kq.TongCot6);
        Assert.Equal(0m, kq.ChenhLechPhanIII);
    }

    [Fact]
    public void Mac_dinh_la_cach_B_theo_quyet_dinh_cua_ke_toan()
    {
        var khongTruyenMode = Mau01Part2Calculator.Tinh([DongViDu()]);

        Assert.Equal(8_000_000m, khongTruyenMode.TongCot6);   // mặc định = ReducedRateTax (đáp án B)
    }
}
