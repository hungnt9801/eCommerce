namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — NGUỒN SINH DÒNG GIÁ VỐN** (`PRC-001-arch` §4.1 S4).
///
/// 🚨 **Enum lưu DB dạng SỐ** ⇒ **CHỈ THÊM Ở CUỐI DẢI, KHÔNG ĐỔI SỐ ĐÃ DÙNG** (`D-17`). Đây là **dải mới**
/// (không đổi số của enum cũ nào).
///
/// 📌 `NULL` (cột trống) = dòng có **trước** `W17-2` ⇒ **coi như thủ công** (`ThuCong`); đường đọc không phải
/// phân biệt vì cả hai đều là giá vốn "của người/kế toán", không phải do động cơ sinh.
/// </summary>
public enum CostPriceSource
{
    /// <summary>Giá vốn **nhập/duyệt thủ công** (đường ghi `ICostPriceAdminAppService` hoặc dữ liệu cũ).</summary>
    ThuCong = 0,

    /// <summary>Giá vốn do **động cơ bình quân gia quyền di động** tính ra (`CostEngineService`).</summary>
    DongCo = 1
}
