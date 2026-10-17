namespace eCommerce.Tax.Mau01;

/// <summary>Một dòng hàng hoá/dịch vụ bán ra đưa vào tính Mẫu 01 — Phần II.</summary>
/// <param name="TenHangHoa">Cột (2) — tên hàng hoá, dịch vụ.</param>
/// <param name="DoanhThuChuaThue">Cột (3) — giá trị hàng hoá/dịch vụ chưa có thuế GTGT (VND).</param>
/// <param name="ThueSuatTheoQuyDinh">Cột (4) — thuế suất theo quy định, dạng **phân số** (10% = 0.10m).</param>
/// <param name="ThueSuatSauGiam">Cột (5) — thuế suất sau giảm, dạng **phân số** (8% = 0.08m). Mẫu in sẵn: `(5) = (4) × 80%`.</param>
/// <param name="ThuocDienGiam">
/// Có thuộc diện được giảm (8%) hay không.
/// 🔒 <b>Q8 "Câu 1b = A"</b>: Phần II **CHỈ liệt kê hàng thuộc diện được giảm** ⇒ dòng `false` bị loại khỏi Phần II
/// (dù vẫn có thể xuất hiện ở báo cáo khác).
/// </param>
public sealed record Mau01Part2InputRow(
    string TenHangHoa,
    decimal DoanhThuChuaThue,
    decimal ThueSuatTheoQuyDinh,
    decimal ThueSuatSauGiam,
    bool ThuocDienGiam);

/// <summary>Một dòng của Phần II sau khi tính — ánh xạ 1-1 với 6 cột in trong biểu mẫu.</summary>
public sealed record Mau01Part2Row(
    int Stt,
    string TenHangHoa,
    decimal DoanhThuChuaThue,
    decimal ThueSuatTheoQuyDinh,
    decimal ThueSuatSauGiam,
    decimal Cot6);

/// <summary>Kết quả Phần II + tổng để đối chiếu với các chỉ tiêu của biểu mẫu.</summary>
/// <param name="Rows">Các dòng đã tính (STT đánh lại từ 1).</param>
/// <param name="TongCot3">Tổng cột (3) — doanh thu chịu thuế của hàng thuộc diện giảm.</param>
/// <param name="TongCot6">Tổng cột (6).</param>
/// <param name="TongThueSuatTheoQuyDinh">
/// Tổng thuế nếu tính **theo thuế suất quy định** = `Σ (3) × (4)` — tức chỉ tiêu **[08]** *(số thuế GTGT đầu ra theo quy định)*.
/// Không phải cột nào trong biểu mẫu; cần để tính Phần III.
/// </param>
/// <param name="ChenhLechPhanIII">
/// Phần III `[09] = [08] − [06]` — **số thuế GTGT được giảm**.
/// ⚠️ Chỉ có nghĩa khi `mode = ReducedRateTax`. Nếu `mode = ReductionAmount` thì (6) **đã là** số được giảm
/// và công thức này sẽ **trừ hai lần** ⇒ trả về `null` để nơi gọi không dùng nhầm.
/// </param>
public sealed record Mau01Part2Result(
    IReadOnlyList<Mau01Part2Row> Rows,
    decimal TongCot3,
    decimal TongCot6,
    decimal TongThueSuatTheoQuyDinh,
    decimal? ChenhLechPhanIII);

/// <summary>
/// Tính **Mẫu 01 — Phần II (hàng hoá, dịch vụ bán ra trong kỳ)**.
///
/// 🎯 Đây là **hàm thuần** (pure function): không DI, không DB, không thời gian hệ thống ⇒ **test được toàn bộ**
/// và **dùng chung** cho mọi định dạng xuất (bảng số liệu, HTKK, eTax). Nhờ vậy đổi **định dạng xuất**
/// không đụng tới **số liệu**.
///
/// 📌 Căn cứ pháp lý & quyết định: xem `docs/features/cross-cutting/XC-006-phan-loai-thue-gtgt.md` §6.3 và §15.1.5.
/// </summary>
public static class Mau01Part2Calculator
{
    /// <summary>
    /// Số chữ số thập phân của số tiền VND khi in Mẫu 01.
    /// 📌 TT 91/2026 không quy định rõ việc làm tròn ⇒ đây là **quyết định quản trị** (cần kế toán trưởng duyệt).
    /// Tạm dùng **0 chữ số thập phân, làm tròn nửa lên** (`AwayFromZero`) — đúng thông lệ kê khai VND.
    /// </summary>
    public const int SoChuSoThapPhanTien = 0;

    public static Mau01Part2Result Tinh(
        IEnumerable<Mau01Part2InputRow> rows,
        Mau01Column6Mode mode = Mau01Column6Mode.ReducedRateTax)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // 🔒 Q8 "Câu 1b = A": Phần II CHỈ liệt kê hàng THUỘC DIỆN ĐƯỢC GIẢM.
        var duocGiam = rows.Where(r => r.ThuocDienGiam).ToList();

        var ketQua = new List<Mau01Part2Row>(duocGiam.Count);
        var stt = 1;

        foreach (var r in duocGiam)
        {
            var cot6 = mode == Mau01Column6Mode.ReducedRateTax
                ? r.DoanhThuChuaThue * r.ThueSuatSauGiam              // (3) × (5)  — theo TÊN CỘT
                : r.DoanhThuChuaThue * (r.ThueSuatTheoQuyDinh - r.ThueSuatSauGiam); // (3) × [(4) − (5)] — theo công thức in trong mẫu

            ketQua.Add(new Mau01Part2Row(
                Stt: stt++,
                TenHangHoa: r.TenHangHoa,
                DoanhThuChuaThue: r.DoanhThuChuaThue,
                ThueSuatTheoQuyDinh: r.ThueSuatTheoQuyDinh,
                ThueSuatSauGiam: r.ThueSuatSauGiam,
                Cot6: LamTron(cot6)));
        }

        var tongCot3 = LamTron(duocGiam.Sum(r => r.DoanhThuChuaThue));
        var tongCot6 = LamTron(ketQua.Sum(r => r.Cot6));
        var tongTheoQuyDinh = LamTron(duocGiam.Sum(r => r.DoanhThuChuaThue * r.ThueSuatTheoQuyDinh));

        // ⚠️ Chỉ trả [09] khi công thức có nghĩa — tránh nơi gọi trừ hai lần.
        var chenhLech = mode == Mau01Column6Mode.ReducedRateTax
            ? tongTheoQuyDinh - tongCot6
            : (decimal?)null;

        return new Mau01Part2Result(ketQua, tongCot3, tongCot6, tongTheoQuyDinh, chenhLech);
    }

    private static decimal LamTron(decimal value)
        => decimal.Round(value, SoChuSoThapPhanTien, MidpointRounding.AwayFromZero);
}
