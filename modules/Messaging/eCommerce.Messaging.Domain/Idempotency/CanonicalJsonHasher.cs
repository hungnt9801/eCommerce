using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Volo.Abp;

namespace eCommerce.Messaging.Idempotency;

/// <summary>
/// Băm payload theo **JSON chuẩn hoá** (canonical JSON) để so trùng một cách **ổn định**.
///
/// 🚨 VÌ SAO KHÔNG BĂM CHUỖI THÔ: cùng một nội dung có thể được serialize theo nhiều cách —
/// thứ tự thuộc tính khác nhau, có/không khoảng trắng, `1` vs `1.0`. Băm chuỗi thô sẽ cho ra **hash khác nhau**
/// cho **cùng một nội dung** ⇒ hệ thống coi đó là **xung đột payload** và **từ chối yêu cầu hợp lệ**
/// (hoặc tệ hơn: coi là yêu cầu mới và **xử lý trùng**). Canonical hoá loại bỏ toàn bộ khác biệt vô nghĩa.
///
/// 🔒 GẮN PHIÊN BẢN (`XC-002` Q9): phiên bản băm được đưa **vào chính đầu vào băm** ⇒ sau khi nâng cấp
/// thuật toán, hash mới **khác** hash cũ một cách tường minh, nên **không** sinh ra "xung đột khoá GIẢ"
/// cho dữ liệu cũ.
/// </summary>
public static class CanonicalJsonHasher
{
    /// <summary>Phiên bản thuật toán băm hiện hành. Đổi thuật toán ⇒ TĂNG số này.</summary>
    public const int PhienBanHienHanh = 1;

    /// <summary>Băm payload JSON. Ném lỗi nếu payload không phải JSON hợp lệ.</summary>
    public static string Bam(string? payloadJson, int phienBan = PhienBanHienHanh)
    {
        if (payloadJson is null)
        {
            // null và "null" phải khác nhau — nếu coi bằng nhau thì hai payload khác nghĩa lại trùng hash.
            return BamChuoi("<null>", phienBan);
        }

        JsonNode? node;

        try
        {
            node = JsonNode.Parse(payloadJson);
        }
        catch (JsonException ex)
        {
            throw new BusinessException(MessagingErrorCodes.PayloadKhongHopLe).WithData("loi", ex.Message);
        }

        var chuanHoa = ChuanHoa(node);
        return BamChuoi(chuanHoa, phienBan);
    }

    /// <summary>Chuỗi canonical: không khoảng trắng thừa, thuộc tính đối tượng sắp xếp theo tên.</summary>
    public static string ChuanHoa(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject obj => ChuanHoaDoiTuong(obj),
        JsonArray arr => "[" + string.Join(",", arr.Select(ChuanHoa)) + "]",
        JsonValue val => ChuanHoaGiaTri(val),
        _ => "null"
    };

    private static string ChuanHoaDoiTuong(JsonObject obj)
    {
        // 🔒 Sắp xếp theo tên thuộc tính (Ordinal) ⇒ {"a":1,"b":2} và {"b":2,"a":1} cho CÙNG kết quả.
        var phanTu = obj
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{JsonSerializer.Serialize(kv.Key)}:{ChuanHoa(kv.Value)}");

        return "{" + string.Join(",", phanTu) + "}";
    }

    private static string ChuanHoaGiaTri(JsonValue value)
    {
        // Số: đưa về dạng chuẩn để 1, 1.0, 1.00 cho cùng hash.
        // 🚨 BẪY ĐÃ SẬP (test bắt được): `decimal.ToString()` **giữ nguyên scale** ⇒ `1.0m` in ra "1.0".
        // Dùng "G29" — định dạng chuẩn để **bỏ số 0 thập phân dư**.
        if (value.TryGetValue<decimal>(out var so))
        {
            return so.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<bool>(out var b))
        {
            return b ? "true" : "false";
        }

        if (value.TryGetValue<string>(out var s))
        {
            return JsonSerializer.Serialize(s);
        }

        return value.ToJsonString();
    }

    private static string BamChuoi(string dauVao, int phienBan)
    {
        // 🔒 Phiên bản nằm TRONG đầu vào băm (xem ghi chú đầu class).
        var bytes = Encoding.UTF8.GetBytes($"v{phienBan}|{dauVao}");
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexStringLower(hash);
    }
}
