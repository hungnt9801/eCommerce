using System.Linq.Expressions;

namespace eCommerce.Customers;

/// <summary>
/// 🗂️ **BỘ ĐỊNH NGHĨA 5 TAB PHÂN KHÚC** của màn DS khách — `WP-07` (`07 §WP-07`: *"FE: DS khách 5 tab phân khúc"*).
///
/// 🚨 **VÌ SAO TAB LÀ DỮ LIỆU (không phải 5 chuỗi `if` trong page):** cùng lý do `D-41` của đơn hàng —
/// **tab đếm** và **tab lọc** phải dùng **cùng một** định nghĩa. Nếu không thì *"tab Còn nợ đếm 7, bấm vào
/// ra 5"*, và badge là thứ duy nhất khiến nhân viên nhìn vào danh sách.
/// 📌 `Expression` (không `Func`) để EF dịch được sang SQL ⇒ đếm/lọc ở **server** (20.000 khách không thể
/// tải hết rồi lọc ở client — đúng yêu cầu *"lọc server-side, không tin client"*).
/// </summary>
public static class CustomerTabs
{
    /// <summary>Một tab: khoá URL · nhãn · điều kiện (dịch được sang SQL).</summary>
    public sealed record Tab(string Key, string Label, Expression<Func<Customer, bool>> Loc);

    /// <summary>**ĐÚNG 5 tab** theo kế hoạch — không thêm tab "sắp có" (menu/badge nói dối).</summary>
    public static readonly IReadOnlyList<Tab> DanhSach =
    [
        new("all", "Tất cả", _ => true),

        new("retail", "Khách lẻ", x => x.Type == CustomerType.Retail),

        new("wholesale", "Khách buôn", x => x.Type == CustomerType.Wholesale),

        new("agency", "Đại lý", x => x.Type == CustomerType.Agency),

        // 🔑 "Còn nợ" là tab **nghiệp vụ** quan trọng nhất: nó là hàng đợi đi thu tiền.
        // 📌 Điều kiện là `DebtBalance > 0` — KHÔNG phải `DebtLimit > 0` (hạn mức khác dư nợ).
        new("debt", "Còn nợ", x => x.DebtBalance > 0m)
    ];

    /// <summary>Tìm tab theo khoá; **khoá lạ ⇒ `null`** (chỗ gọi báo lỗi, KHÔNG im lặng trả "tất cả").</summary>
    public static Tab? Tim(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return DanhSach.FirstOrDefault(t => string.Equals(t.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
