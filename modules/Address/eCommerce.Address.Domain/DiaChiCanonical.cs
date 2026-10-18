using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace eCommerce.Address;

/// <summary>
/// **Canonical hoá + băm `ContentHash`** của <see cref="Address"/> — đặc tả ở `ADDR-004` §5.2.
///
/// 🎯 VÌ SAO PHẢI CÓ MỘT CHỖ DUY NHẤT: `ContentHash` là **khoá tự nhiên** của địa chỉ
/// (`ADR-04`: *"một nội dung ⇒ một bản ghi"*). Nếu hai nơi tự tính hash khác nhau thì
/// khử trùng lặp **hỏng âm thầm** — hai bản ghi cho cùng một địa chỉ, không lỗi nào nổi lên.
///
/// 🚨 BA ĐIỀU CẤM (mỗi điều là một lỗi im lặng đã biết — `ADDR-004` §5.2):
///   ① **CẤM** bỏ dấu tiếng Việt: *"Xã Đông Xuân"* và *"Xã Dong Xuan"* là **hai** địa danh có thể
///      cùng tồn tại ⇒ gộp sẽ trộn hai xã khác nhau.
///   ② **CẤM** so sánh `OrdinalIgnoreCase` **không** kèm `Normalize(FormC)`: hai chuỗi **nhìn giống hệt**
///      nhưng khác dạng Unicode (dựng sẵn vs tổ hợp) cho **hai hash khác nhau**.
///   ③ **CẤM** đưa `TenantId` vào hash — tenant đã nằm trong **khoá index**.
/// </summary>
public static class DiaChiCanonical
{
    /// <summary>Độ dài hash hex (SHA-256) — khớp `CHECK "ContentHash" ~ '^[0-9a-f]{64}$'`.</summary>
    public const int DoDaiHash = 64;

    /// <summary>Tiền tố canonical của địa chỉ **đã chuẩn hoá** (có mã ĐVHC).</summary>
    public const string TienToDaChuanHoa = "n";

    /// <summary>Tiền tố canonical của địa chỉ **thô** (chưa có mã ĐVHC).</summary>
    public const string TienToTho = "r";

    // Gộp mọi khoảng trắng liên tiếp (space, tab, CR, LF, form-feed, vertical-tab) thành MỘT dấu cách.
    private static readonly Regex NhieuKhoangTrang = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Chuẩn hoá một chuỗi để đưa vào canonical: `Normalize(FormC)` → `Trim()` → gộp khoảng trắng →
    /// `ToLowerInvariant()`.
    ///
    /// 🔒 **KHÔNG bỏ dấu tiếng Việt.** `ToLowerInvariant` chỉ đổi hoa/thường, giữ nguyên dấu.
    /// </summary>
    public static string Chuan(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return string.Empty;
        }

        var f = s.Normalize(NormalizationForm.FormC).Trim();
        f = NhieuKhoangTrang.Replace(f, " ");
        return f.ToLowerInvariant();
    }

    /// <summary>
    /// Chuỗi canonical của địa chỉ **đã chuẩn hoá**:
    /// <c>"n" + SchemaVersion + "|" + TinhCode + "|" + (HuyenCode ?? "-") + "|" + XaCode + "|" + chuan(SoNhaDuong ?? "")</c>
    /// </summary>
    public static string DungChuoiCanonicalDaChuanHoa(
        int schemaVersion,
        string tinhCode,
        string? huyenCode,
        string xaCode,
        string? soNhaDuong)
    {
        Check.NotNullOrWhiteSpace(tinhCode, nameof(tinhCode));
        Check.NotNullOrWhiteSpace(xaCode, nameof(xaCode));

        // 🔒 Huyện rỗng/khoảng trắng phải quy về `"-"` (tức "không có huyện"), KHÔNG được tạo ra một
        // chuỗi canonical khác. Nếu không: cùng một địa chỉ đi qua hai đường (entity đã Trim vs nơi
        // gọi truyền "   ") sẽ cho HAI hash ⇒ khử trùng lặp hỏng âm thầm. Bảo vệ ở cả hai lớp.
        var huyen = string.IsNullOrWhiteSpace(huyenCode) ? "-" : huyenCode.Trim();

        return string.Concat(
            TienToDaChuanHoa,
            schemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "|", tinhCode.Trim(), "|", huyen, "|", xaCode.Trim(), "|", Chuan(soNhaDuong));
    }

    /// <summary>Chuỗi canonical của địa chỉ **thô**: <c>"r|" + chuan(DiaChiDayDu)</c>.</summary>
    public static string DungChuoiCanonicalTho(string diaChiDayDu)
    {
        Check.NotNullOrWhiteSpace(diaChiDayDu, nameof(diaChiDayDu));
        return string.Concat(TienToTho, "|", Chuan(diaChiDayDu));
    }

    /// <summary>SHA-256 hex **chữ thường** (64 ký tự) của chuỗi canonical.</summary>
    public static string Bam(string chuoiCanonical)
    {
        Check.NotNullOrWhiteSpace(chuoiCanonical, nameof(chuoiCanonical));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(chuoiCanonical));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Tính hash cho địa chỉ **đã chuẩn hoá** (đường chuẩn — dùng trong factory và khi chuẩn hoá).</summary>
    public static string TinhHashDaChuanHoa(
        int schemaVersion, string tinhCode, string? huyenCode, string xaCode, string? soNhaDuong)
        => Bam(DungChuoiCanonicalDaChuanHoa(schemaVersion, tinhCode, huyenCode, xaCode, soNhaDuong));

    /// <summary>Tính hash cho địa chỉ **thô**.</summary>
    public static string TinhHashTho(string diaChiDayDu)
        => Bam(DungChuoiCanonicalTho(diaChiDayDu));
}
