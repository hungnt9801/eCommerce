using System.Text;
using eCommerce.Tax.Mau01;

namespace eCommerce.Tests.Tax;

/// <summary>
/// Test cho **lớp xuất Mẫu 01 Phần II** (`Q9 = A`, tự chọn định dạng CSV có BOM).
///
/// 🎯 Vì sao cần test: đây là **tệp kế toán dùng để kê khai**. Ba lỗi im lặng nguy hiểm nhất được test chặn:
///   1. **Thiếu BOM** ⇒ Excel mở ra **hỏng dấu tiếng Việt** (nhìn thấy ngay nhưng rất dễ bị bỏ qua).
///   2. **Số có dấu phân cách nghìn** ⇒ import hiểu sai **1000 lần** (không có cảnh báo nào).
///   3. **Lẫn dòng 10%/5% vào Phần II** ⇒ sai chỉ tiêu (Q8 "Câu 1b = A" cấm điều này).
/// </summary>
public class Mau01CsvExporterTests
{
    private static readonly byte[] BomUtf8 = [0xEF, 0xBB, 0xBF];

    private static Mau01ExportRequest YeuCau(
        Mau01Column6Mode mode = Mau01Column6Mode.ReducedRateTax,
        params Mau01Part2InputRow[] rows)
    {
        var ketQua = Mau01Part2Calculator.Tinh(rows, mode);
        return new Mau01ExportRequest("10/2026", "0312345678", "CÔNG TY TNHH THỬ NGHIỆM", ketQua, mode);
    }

    private static string Doc(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static string[] CacDong(byte[] bytes)
        => Doc(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToArray();

    [Fact]
    public void Ten_dinh_dang_va_phan_mo_rong()
    {
        var exporter = new Mau01CsvExporter();

        Assert.Equal(Mau01CsvExporter.TenDinhDangCsv, exporter.TenDinhDang);
        Assert.Equal("csv", exporter.PhanMoRong);
    }

    [Fact]
    public void Co_BOM_UTF8_de_Excel_mo_dung_dau_tieng_Viet()
    {
        var bytes = new Mau01CsvExporter().Xuat(YeuCau(rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true)));

        Assert.Equal(BomUtf8, bytes.Take(3).ToArray());
    }

    [Fact]
    public void Du_6_cot_theo_bieu_mau()
    {
        var dong = CacDong(new Mau01CsvExporter().Xuat(
            YeuCau(rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true))));

        var tieuDe = dong.Single(l => l.StartsWith("(1) STT,", StringComparison.Ordinal));

        foreach (var cot in new[] { "(1)", "(2)", "(3)", "(4)", "(5)", "(6)" })
        {
            Assert.Contains(cot, tieuDe);
        }
    }

    [Fact]
    public void Cot6_theo_dap_an_B_la_8000000()
    {
        var dong = CacDong(new Mau01CsvExporter().Xuat(
            YeuCau(rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true))));

        // Dòng dữ liệu: 1,Hàng A,100000000,10,8,8000000
        var duLieu = dong.Single(l => l.StartsWith("1,", StringComparison.Ordinal));

        Assert.Equal("1,Hàng A,100000000,10,8,8000000", duLieu);
    }

    [Fact]
    public void So_KHONG_co_dau_phan_cach_nghin()
    {
        var text = Doc(new Mau01CsvExporter().Xuat(
            YeuCau(rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true))));

        // Không được xuất hiện '100.000.000' (dấu chấm) hay '100,000,000' (dấu phẩy) trong CỘT SỐ.
        Assert.DoesNotContain("100.000.000", text);
        Assert.DoesNotContain("100,000,000", text);
        Assert.Contains("100000000", text);
    }

    [Fact]
    public void Chi_liet_ke_hang_thuoc_dien_giam_8_phan_tram()
    {
        var bytes = new Mau01CsvExporter().Xuat(YeuCau(rows:
        [
            new Mau01Part2InputRow("Hàng 8%", 100_000_000m, 0.10m, 0.08m, true),
            new Mau01Part2InputRow("Hàng 10%", 50_000_000m, 0.10m, 0.10m, false),
            new Mau01Part2InputRow("Hàng 5%", 30_000_000m, 0.05m, 0.05m, false)
        ]));

        var text = Doc(bytes);

        Assert.Contains("Hàng 8%", text);
        Assert.DoesNotContain("Hàng 10%", text);
        Assert.DoesNotContain("Hàng 5%", text);
    }

    [Fact]
    public void Co_dong_TONG_va_chi_tieu_08_09()
    {
        var text = Doc(new Mau01CsvExporter().Xuat(
            YeuCau(rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true))));

        Assert.Contains("TỔNG,", text);
        Assert.Contains("[08]", text);
        Assert.Contains("[09]", text);
        Assert.Contains("10000000", text);   // [08] = 100tr × 10%
        Assert.Contains("2000000", text);    // [09] = [08] − [06]
    }

    [Fact]
    public void Khi_dung_cach_A_thi_KHONG_xuat_chi_tieu_09()
    {
        var text = Doc(new Mau01CsvExporter().Xuat(YeuCau(
            mode: Mau01Column6Mode.ReductionAmount,
            rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true))));

        Assert.Contains("[09] KHÔNG xuất", text);
        Assert.Contains("TRỪ HAI LẦN", text);
    }

    [Fact]
    public void Ghi_ro_dinh_dang_va_cach_hieu_cot_6_de_truy_vet()
    {
        var text = Doc(new Mau01CsvExporter().Xuat(
            YeuCau(rows: new Mau01Part2InputRow("Hàng A", 100_000_000m, 0.10m, 0.08m, true))));

        Assert.Contains(Mau01CsvExporter.TenDinhDangCsv, text);
        Assert.Contains("10/2026", text);
        Assert.Contains("0312345678", text);
        Assert.Contains("đáp án B", text);
    }

    [Fact]
    public void Ten_hang_co_dau_phay_thi_duoc_bao_trong_nhay_kep()
    {
        var dong = CacDong(new Mau01CsvExporter().Xuat(
            YeuCau(rows: new Mau01Part2InputRow("Hàng A, loại đặc biệt", 1_000m, 0.10m, 0.08m, true))));

        var duLieu = dong.Single(l => l.StartsWith("1,", StringComparison.Ordinal));

        Assert.Contains("\"Hàng A, loại đặc biệt\"", duLieu);
    }

    [Fact]
    public void Danh_sach_rong_van_xuat_duoc_tep_hop_le()
    {
        var bytes = new Mau01CsvExporter().Xuat(YeuCau());
        var text = Doc(bytes);

        Assert.Equal(BomUtf8, bytes.Take(3).ToArray());
        Assert.Contains("TỔNG,", text);
    }
}
