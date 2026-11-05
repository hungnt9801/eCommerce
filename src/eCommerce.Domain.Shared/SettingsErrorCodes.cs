namespace eCommerce.Settings;

/// <summary>
/// **Mã lỗi của CẤU HÌNH TỔ CHỨC** (`WP-07`) — dải riêng `eCommerce:Settings:0xxx`.
///
/// 📌 **VÌ SAO CẦN DẢI RIÊNG:** hai lỗi dưới đây là lỗi **cấu hình của người vận hành**, không phải lỗi
/// nghiệp vụ của khách/đơn. 🔑 Nếu mượn mã miền khác (ví dụ `Customers:0023`) thì CSKH đọc *"mã đã tồn tại"*
/// trong khi sự thật là **permission/vai trò không tồn tại** — đúng bài học `INV-003` `0042`.
/// ⚠️ Mã là **hợp đồng với client**: đổi mã = đổi API.
/// </summary>
public static class SettingsErrorCodes
{
    private const string Nhom = "eCommerce:Settings:";

    /// <summary>Permission **không tồn tại** trong định nghĩa (gõ sai tên, hoặc FE đang giữ danh sách cũ).</summary>
    public const string QuyenKhongTonTai = Nhom + "0001";

    /// <summary>Vai trò **không tồn tại** trong `AbpRoles` (gõ sai tên, hoặc vai trò đã bị xoá).</summary>
    public const string VaiTroKhongTonTai = Nhom + "0002";
}
