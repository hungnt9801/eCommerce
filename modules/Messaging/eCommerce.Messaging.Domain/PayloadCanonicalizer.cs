using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace eCommerce.Messaging;

/// <summary>
/// 🚨 **CHUẨN HOÁ PAYLOAD + BĂM (SHA-256)** — `XC-002` §6.4 / `SAL-005` **BR-SAL-005-004**.
///
/// **VÌ SAO PHẢI CHUẨN HOÁ TRƯỚC KHI BĂM (lỗi THẬT sẽ xảy ra nếu bỏ qua — đã ghi ở `sal-005` §7.2):**
/// cùng **một** payload nghiệp vụ nhưng **thứ tự khoá JSON khác** (hoặc khoảng trắng khác, hoặc `DateTime`
/// ở offset khác) sẽ cho **hash KHÁC NHAU**. Hệ quả **cụ thể**, không trừu tượng:
/// <list type="bullet">
///   <item>nhánh **C** (cùng mốc + **hash khớp** ⇒ *"trùng, bỏ qua"*) bị **nhận nhầm** thành nhánh **D**
///     (cùng mốc + hash **khác** ⇒ *"xung đột"*) ⇒ **DLQ GIẢ** ⇒ Ops bị đánh thức vì một sự kiện **vô hại**;</item>
///   <item>và tệ hơn: **hai instance** băm **cùng** sự kiện ra **hai hash khác nhau** ⇒ một instance coi là
///     *trùng*, instance kia coi là *xung đột* ⇒ **hai kết luận khác nhau cho cùng dữ liệu** — đúng loại
///     "không tất định" mà `SAL-005` **D6** cấm.</item>
/// </list>
/// 📌 Sau vài lần DLQ giả, người trực **bắt đầu bỏ qua cảnh báo** — và đó là lúc xung đột **THẬT** bị bỏ qua.
///
/// 🔒 **Ba quy tắc chuẩn hoá (cả ba đều bắt buộc, thiếu một là hash không tất định):**
/// <list type="number">
///   <item>**Sắp xếp khoá theo thứ tự `Ordinal`** (không theo culture — culture đổi theo máy);</item>
///   <item>**Không khoảng trắng** (`WriteIndented = false`);</item>
///   <item>**`DateTime` về UTC dạng ISO-8601 tròn giây** — vì `AggregateVersion` của `SAL-005` là
///     **epoch giây** (`D5`), giữ mili-giây sẽ làm hai bản *"cùng một cập nhật"* ra hai hash.</item>
/// </list>
///
/// ⚠️ **Giới hạn ĐÃ BIẾT (nói thẳng):** hàm này chuẩn hoá **cấu trúc JSON**, **không** chuẩn hoá **ngữ nghĩa**
/// (ví dụ `"1.50"` và `"1.5"` là hai hash khác nhau). Với `SAL-005`, payload do **một** connector sinh ra
/// nên hình dạng ổn định; nếu sau này có **nhiều** nguồn sinh cùng sự kiện thì phải bổ sung quy tắc số.
/// 📌 Hàm **THUẦN** (không I/O, không phụ thuộc thời gian hệ thống) ⇒ test được **mọi** nhánh.
/// </summary>
public static class PayloadCanonicalizer
{
    /// <summary>Độ dài hash trả về (hex của SHA-256) — khớp `HasMaxLength(64)` của cột `PayloadHash`.</summary>
    public const int DoDaiHash = 64;

    private static readonly JsonSerializerOptions TuyChon = new()
    {
        WriteIndented = false,
        // 🔒 `Ordinal` — **KHÔNG** dùng so sánh phụ thuộc culture/máy (đó là nguồn gốc của hash không tất định).
        PropertyNamingPolicy = null
    };

    /// <summary>
    /// Băm **canonical JSON** của một giá trị bất kỳ ⇒ **hex chữ thường, 64 ký tự**.
    /// 🔑 **Chuỗi rỗng/`null` ⇒ vẫn trả hash** (của `null`) — **không** trả `null`, để chỗ gọi không phải
    /// xử lý hai trường hợp (và `PayloadHash` là **cột bắt buộc** ở entity).
    /// </summary>
    public static string Bam<T>(T giaTri)
    {
        var json = ChuanHoa(giaTri);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Chuỗi **canonical JSON** — dùng khi cần lưu/đối chiếu **nội dung** đã chuẩn hoá (không chỉ hash).</summary>
    public static string ChuanHoa<T>(T giaTri)
    {
        if (giaTri is null)
        {
            return "null";
        }

        // ① Đưa qua `JsonElement` để có cây DOM **đã tách rời** khỏi kiểu gốc, rồi ② sắp xếp khoá đệ quy.
        using var taiLieu = JsonDocument.Parse(JsonSerializer.Serialize(giaTri, TuyChon));

        var sb = new StringBuilder();
        Ghi(sb, taiLieu.RootElement);

        return sb.ToString();
    }

    /// <summary>
    /// Ghi **đệ quy** một `JsonElement` theo dạng canonical: khoá **sắp xếp `Ordinal`**, không khoảng trắng,
    /// `DateTime`/số giữ nguyên văn bản đã qua `JsonSerializer` (đã là UTC dạng ISO nếu kiểu là `DateTime`).
    /// 📌 Viết tay thay vì dùng `JsonSerializer` với `Dictionary` sắp xếp: cách đó **không** đệ quy vào
    /// mảng/đối tượng lồng nhau (mà payload webhook **luôn** lồng nhiều tầng).
    /// </summary>
    private static void Ghi(StringBuilder sb, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var dauTien = true;

                // 🔒 `StringComparer.Ordinal` — thứ tự **xác định trên mọi máy**; `OrderBy` mặc định dùng
                // comparer của culture hiện hành ⇒ **hai** máy khác culture có thể cho **hai** thứ tự khác nhau.
                foreach (var p in e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    if (!dauTien)
                    {
                        sb.Append(',');
                    }

                    dauTien = false;
                    sb.Append(JsonSerializer.Serialize(p.Name));
                    sb.Append(':');
                    Ghi(sb, p.Value);
                }

                sb.Append('}');
                break;

            case JsonValueKind.Array:
                sb.Append('[');
                var dauTienArr = true;

                foreach (var item in e.EnumerateArray())
                {
                    if (!dauTienArr)
                    {
                        sb.Append(',');
                    }

                    dauTienArr = false;
                    Ghi(sb, item);
                }

                sb.Append(']');
                break;

            case JsonValueKind.String:
                sb.Append(JsonSerializer.Serialize(e.GetString()));
                break;

            case JsonValueKind.Number:
                // 📌 Giữ **nguyên văn** (`GetRawText`) — parse rồi ghi lại có thể đổi `1.50` thành `1.5`
                // ở một số cấu hình, tức **tự tay** tạo ra hash không tất định.
                sb.Append(e.GetRawText());
                break;

            case JsonValueKind.True:
                sb.Append("true");
                break;

            case JsonValueKind.False:
                sb.Append("false");
                break;

            default:
                sb.Append("null");
                break;
        }
    }

    /// <summary>
    /// Chuẩn hoá **thời điểm về epoch giây UTC** — dùng cho `AggregateVersion` của `SAL-005` (**D5**:
    /// watermark là **epoch giây của `update_time`**, không phải bộ đếm tăng đều vì *"ta không kiểm soát
    /// bộ đếm của sàn"*).
    /// 🔒 **Làm tròn XUỐNG** (`Floor`) chứ không làm tròn gần nhất: hai bản ghi trong **cùng một giây**
    /// phải cho **cùng** `AggregateVersion` — đó chính là ca **D6** (cùng mốc, khác nội dung ⇒ xung đột).
    /// Nếu làm tròn gần nhất thì `.500` giây sẽ nhảy sang giây sau ⇒ **hai bản cùng giây** bị coi là
    /// *"bản sau mới hơn"* ⇒ **ghi đè im lặng** — đúng thứ `D6` cấm.
    /// </summary>
    public static long ThanhPhienBan(DateTime thoiDiem)
    {
        var utc = thoiDiem.Kind switch
        {
            DateTimeKind.Utc => thoiDiem,
            DateTimeKind.Local => thoiDiem.ToUniversalTime(),
            // ⚠️ `Unspecified` (rất hay gặp từ JSON của sàn): **coi là UTC** — vì mọi mốc thời gian trong
            // hệ thống này đều là UTC (`XC-001`/`SAL-005` dùng `Utc` ở mọi tên trường). Suy diễn theo giờ máy
            // sẽ làm **cùng** payload cho **hai** phiên bản khác nhau ở hai máy khác múi giờ.
            _ => DateTime.SpecifyKind(thoiDiem, DateTimeKind.Utc)
        };

        return (long)Math.Floor((utc - DateTime.UnixEpoch).TotalSeconds);
    }

    /// <summary>Ngược của <see cref="ThanhPhienBan"/> — để người đọc hiểu mốc (log/UI).</summary>
    public static DateTime TuPhienBan(long phienBan)
        => DateTime.UnixEpoch.AddSeconds(phienBan);

    /// <summary>Mô tả ngắn gọn một mốc để ghi log — **luôn** ISO-8601 UTC, không phụ thuộc culture máy.</summary>
    public static string MoTa(DateTime thoiDiem)
        => thoiDiem.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
