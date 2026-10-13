using System.Globalization;
using System.Text;
using Volo.Abp.DependencyInjection;

namespace eCommerce.Tax.Mau01;

/// <summary>Dữ liệu đầu vào để xuất Mẫu 01 — Phần II.</summary>
public sealed record Mau01ExportRequest(
    string KyKeKhai,
    string MaSoThue,
    string TenNguoiNopThue,
    Mau01Part2Result KetQua,
    Mau01Column6Mode Mode = Mau01Column6Mode.ReducedRateTax);

/// <summary>
/// 🔌 **LỚP ĐỊNH DẠNG XUẤT** — tách khỏi lớp TÍNH SỐ (<see cref="Mau01Part2Calculator"/>).
///
/// 🎯 VÌ SAO TÁCH: kế toán chốt **Q9 = A** (cần "file nộp được luôn" của HTKK) **nhưng chưa cung cấp
/// file mẫu**. Định dạng HTKK **đổi theo phiên bản phần mềm**; nếu trộn logic tính số với logic ghi file
/// thì mỗi lần HTKK đổi định dạng phải sửa cả phần tính số — rất dễ làm **sai số** trong lúc sửa định dạng.
/// Nay: số liệu tính bằng **một hàm thuần dùng chung**, định dạng là **thành phần cắm thêm được**.
/// </summary>
public interface IMau01Exporter
{
    /// <summary>Tên định dạng (ví dụ <c>csv-mau01-p2</c>) — ghi vào tệp xuất để TRUY VẾT kỳ nào dùng định dạng nào.</summary>
    string TenDinhDang { get; }

    /// <summary>Phần mở rộng tệp (không có dấu chấm).</summary>
    string PhanMoRong { get; }

    byte[] Xuat(Mau01ExportRequest yeuCau);
}

/// <summary>
/// Xuất **Mẫu 01 — Phần II** ra **CSV (UTF-8 có BOM)**.
///
/// ✅ QUYẾT ĐỊNH CỦA TÔI (13/10/2026 — sẽ chỉnh khi kế toán cung cấp file mẫu HTKK):
///   1. **CSV thay vì XLSX**: CSV **không cần thêm gói NuGet nào** và Excel mở được ngay ⇒ không kéo
///      rủi ro dependency vào dự án trước khi biết chắc định dạng đích.
///   2. **BOM UTF-8**: thiếu BOM thì Excel mở CSV sẽ **hỏng dấu tiếng Việt** (lỗi rất hay gặp).
///   3. **Số KHÔNG dùng dấu phân cách nghìn** (`100000000`, không phải `100.000.000`): dấu `.`/`,` sẽ bị
///      hiểu là dấu thập phân khi import ⇒ **sai số 1000 lần**. Số dạng thô là an toàn nhất cho MỌI đường import.
///   4. **Thuế suất ghi dạng PHẦN TRĂM** (`10`, `8`) để khớp cách biểu mẫu trình bày — KHÔNG ghi `0.1`.
///   5. **CHỈ 6 cột của Phần II** (Q8 "Câu 1b = A"): chỉ hàng thuộc diện giảm 8%; không có dòng 10%/5%.
/// </summary>
public sealed class Mau01CsvExporter : IMau01Exporter, ITransientDependency
{
    public const string TenDinhDangCsv = "csv-mau01-p2";

    /// <summary>Ký tự phân tách: dấu phẩy theo chuẩn CSV. Ghi rõ để đổi sang `;` nếu môi trường cần.</summary>
    public const char PhanTach = ',';

    public string TenDinhDang => TenDinhDangCsv;

    public string PhanMoRong => "csv";

    public byte[] Xuat(Mau01ExportRequest yeuCau)
    {
        ArgumentNullException.ThrowIfNull(yeuCau);

        var sb = new StringBuilder();

        // ── Khối thông tin kỳ kê khai (giúp tệp tự mô tả, không phải tra ngược) ──
        sb.AppendLine("MẪU SỐ 01 — PHỤ LỤC III — PHẦN II: HÀNG HOÁ, DỊCH VỤ BÁN RA TRONG KỲ");
        sb.AppendLine(Ghi("Kỳ kê khai", yeuCau.KyKeKhai));
        sb.AppendLine(Ghi("Mã số thuế", yeuCau.MaSoThue));
        sb.AppendLine(Ghi("Tên người nộp thuế", yeuCau.TenNguoiNopThue));
        sb.AppendLine(Ghi("Định dạng xuất", TenDinhDang));
        sb.AppendLine(Ghi("Cách hiểu cột (6)", yeuCau.Mode == Mau01Column6Mode.ReducedRateTax
            ? "(6) = (3) × thuế suất SAU GIẢM (đáp án B của kế toán)"
            : "(6) = (3) × [(4) − (5)] — số thuế ĐƯỢC GIẢM (công thức in trong mẫu)"));
        sb.AppendLine();

        // ── Dòng tiêu đề 6 cột, ghi kèm SỐ THỨ TỰ CỘT như trong biểu mẫu ──
        sb.AppendLine(string.Join(PhanTach,
            Ghi("(1) STT"),
            Ghi("(2) Tên hàng hoá, dịch vụ"),
            Ghi("(3) Doanh thu chưa có thuế GTGT"),
            Ghi("(4) Thuế suất theo quy định (%)"),
            Ghi("(5) Thuế suất sau giảm (%)"),
            Ghi("(6) Thuế GTGT của hàng hoá, dịch vụ bán ra")));

        // ── Các dòng hàng hoá ──
        foreach (var dong in yeuCau.KetQua.Rows)
        {
            sb.AppendLine(string.Join(PhanTach,
                dong.Stt.ToString(CultureInfo.InvariantCulture),
                Ghi(dong.TenHangHoa),
                So(dong.DoanhThuChuaThue),
                PhanTram(dong.ThueSuatTheoQuyDinh),
                PhanTram(dong.ThueSuatSauGiam),
                So(dong.Cot6)));
        }

        // ── Dòng TỔNG ──
        sb.AppendLine(string.Join(PhanTach,
            Ghi("TỔNG"),
            string.Empty,
            So(yeuCau.KetQua.TongCot3),
            string.Empty,
            string.Empty,
            So(yeuCau.KetQua.TongCot6)));

        sb.AppendLine();

        // ── Chỉ tiêu Phần III để đối chiếu (chỉ có nghĩa khi dùng cách B) ──
        sb.AppendLine(Ghi("[08] Thuế GTGT đầu ra theo quy định", So(yeuCau.KetQua.TongThueSuatTheoQuyDinh)));

        if (yeuCau.KetQua.ChenhLechPhanIII is { } chenhLech)
        {
            sb.AppendLine(Ghi("[09] = [08] − [06] — Thuế GTGT được giảm", So(chenhLech)));
        }
        else
        {
            sb.AppendLine(Ghi("[09] KHÔNG xuất",
                "đang dùng cách hiểu A: cột (6) ĐÃ LÀ số thuế được giảm ⇒ [08] − [06] sẽ TRỪ HAI LẦN"));
        }

        // UTF-8 CÓ BOM: bắt buộc để Excel mở CSV đúng dấu tiếng Việt.
        // 🚨 BẪY ĐÃ SẬP (test bắt được): `UTF8Encoding.GetBytes()` **KHÔNG** kèm BOM —
        // chỉ `GetPreamble()` mới trả về BOM. Phải tự ghép BOM vào trước nội dung.
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(sb.ToString());

        var ketQua = new byte[preamble.Length + body.Length];
        preamble.CopyTo(ketQua, 0);
        body.CopyTo(ketQua, preamble.Length);
        return ketQua;
    }

    private static string So(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Đổi phân số (0.08) sang phần trăm hiển thị theo biểu mẫu (8).
    /// 🚨 BẪY ĐÃ SẬP (test bắt được): `decimal` **giữ nguyên scale** khi nhân ⇒ `0.10m * 100m = 10.00m`
    /// ⇒ in ra "10.00" thay vì "10", lệch với cách biểu mẫu trình bày. Phải bỏ số 0 thập phân dư.
    /// </summary>
    private static string PhanTram(decimal phanSo)
    {
        var phanTram = phanSo * 100m;

        if (phanTram == decimal.Truncate(phanTram))
        {
            return decimal.Truncate(phanTram).ToString(CultureInfo.InvariantCulture);
        }

        return phanTram.ToString(CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    /// <summary>Bọc trường CSV: nhân đôi dấu nháy kép và bao trong nháy kép khi cần.</summary>
    private static string Ghi(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny([PhanTach, '"', '\n', '\r']) >= 0 ? $"\"{escaped}\"" : escaped;
    }

    private static string Ghi(string nhan, string giaTri) => $"{Ghi(nhan)}{PhanTach}{Ghi(giaTri)}";
}
