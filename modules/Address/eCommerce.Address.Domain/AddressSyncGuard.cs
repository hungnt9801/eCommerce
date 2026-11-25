namespace eCommerce.Address;

/// <summary>
/// **GUARD KHỞI ĐỘNG chống gọi API công khai ngoài môi trường cho phép** (`D-78`, BR-010).
///
/// 🚨 VÌ SAO PHẢI LÀ HÀM THUẦN VÀ GỌI **VÔ ĐIỀU KIỆN** LÚC BUILD SERVICE PROVIDER:
///   Nếu đặt trong lambda `AddHttpClient(…, http => …)` thì lambda **chỉ chạy khi typed client được
///   resolve lần đầu** ⇒ host không dùng client (hoặc resolve muộn) sẽ **không chạy guard** — đúng kiểu
///   lỗi im lặng mà `D-78` cảnh báo. Guard là hàm thuần để test được bảng ca `TC-22` không cần host.
///
/// 🔒 Quy tắc:
///   • `enabled == false` ⇒ thoát (job không chạy ⇒ không có rủi ro).
///   • Host của `baseUrl` hiệu lực (rỗng ⇒ host công khai mặc định) **khác** nguồn công khai ⇒ thoát.
///   • Ngược lại, `environmentName` **không** thuộc `{Development, Testing}` (so `OrdinalIgnoreCase`;
///     rỗng/không đọc được ⇒ coi như NGOÀI allowlist) ⇒ **NÉM** `InvalidOperationException` ⇒ host không start.
///   • **Không** có cờ vượt, **không** đọc từ DB.
/// </summary>
public static class AddressSyncGuard
{
    /// <summary>Host nguồn công khai mặc định của `provinces.open-api.vn` (chỉ dùng cho dev/test).</summary>
    public const string HostCongKhaiMacDinh = "provinces.open-api.vn";

    private static readonly string[] MoiTruongChoPhep = ["Development", "Testing"];

    public static void KiemTra(bool enabled, string? baseUrl, string? environmentName)
    {
        if (!enabled)
        {
            return;
        }

        var host = LayHost(baseUrl);

        if (!string.Equals(host, HostCongKhaiMacDinh, StringComparison.OrdinalIgnoreCase))
        {
            // Nguồn KHÔNG phải nguồn công khai ⇒ guard không có ý kiến (lỗ hổng ghi nhận `TC-22` ca g:
            // `D-93` không đổi guard, giảm thiểu bằng `D-77` — giữ `Address:Sync:Enabled=false` ở production).
            return;
        }

        if (string.IsNullOrWhiteSpace(environmentName)
            || !MoiTruongChoPhep.Contains(environmentName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "BR-010 (D-78): Address:Sync:Enabled=true và nguồn danh mục ĐVHC là API CÔNG KHAI " +
                $"({HostCongKhaiMacDinh}) nhưng môi trường hiện tại là '{environmentName ?? "(rỗng)"}' — " +
                "chỉ Development/Testing được phép. Production phải nạp danh mục bằng đường file đã duyệt " +
                "(D-93); nếu chưa có nguồn file, giữ Address:Sync:Enabled=false.");
        }
    }

    private static string? LayHost(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return HostCongKhaiMacDinh;
        }

        return Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            ? uri.Host
            : null;
    }
}
