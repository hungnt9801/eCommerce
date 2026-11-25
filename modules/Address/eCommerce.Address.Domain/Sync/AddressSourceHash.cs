using System.Security.Cryptography;
using System.Text;

namespace eCommerce.Address.Sync;

/// <summary>
/// **Băm danh mục ĐVHC đã CHUẨN HOÁ** (`D-78` §7.3, BR-008/BR-009).
///
/// 🎯 Đầu vào (theo đúng câu `D-78`):
/// <c>"v{SchemaVersion}\n" + SourceName.Trim() + "\n" + các dòng</c>, dòng sắp theo `(LevelInt, Code)` `Ordinal`,
/// mỗi dòng <c>"{LevelInt}|{Code.Trim()}|{ParentCode?.Trim() ?? ""}|{Name.Trim()}"</c>, nối `"\n"`, UTF-8.
///
/// 🔒 **ỔN ĐỊNH** qua lần chạy lại; **GIỐNG NHAU** giữa đường file và job khi dữ liệu chuẩn hoá giống nhau
/// (`TC-21` ca e); **CẤM** `Guid.NewGuid()`/thời điểm/đường dẫn file. `Name` đổi ⇒ hash đổi (đúng nghĩa
/// "danh mục khác đi"). Hash **không** phải khoá idempotency (không unique index) — idempotency là **nội dung**.
///
/// 📌 Chịu được giá trị `null` (nguồn/tên/mã) — băm phần rỗng thay vì ném: lượt nạp hỏng vì dữ liệu thiếu
/// vẫn phải để lại **dấu vết** (dòng lô `BiChan`/`Loi`), không được biến thành lỗi băm.
/// </summary>
public static class AddressSourceHash
{
    public const int DoDaiHash = 64;

    public static string Tinh(int schemaVersion, string? sourceName, IEnumerable<DonViNguon>? rows)
    {
        var cacDong = (rows ?? [])
            .OrderBy(x => (int)x.Level)
            .ThenBy(x => (x.Code ?? string.Empty).Trim(), StringComparer.Ordinal)
            .Select(x => string.Join(
                '|',
                (int)x.Level,
                (x.Code ?? string.Empty).Trim(),
                (x.ParentCode ?? string.Empty).Trim(),
                (x.Name ?? string.Empty).Trim()));

        var noiDung = $"v{schemaVersion}\n{(sourceName ?? string.Empty).Trim()}\n{string.Join('\n', cacDong)}";
        var bam = SHA256.HashData(Encoding.UTF8.GetBytes(noiDung));

        return Convert.ToHexString(bam).ToLowerInvariant();
    }
}
