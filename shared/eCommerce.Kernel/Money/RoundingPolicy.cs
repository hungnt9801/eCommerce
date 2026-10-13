namespace eCommerce.Kernel.Money;

/// <summary>
/// **Chính sách làm tròn** — điểm cắm để đổi quy tắc mà không sửa nơi tính.
///
/// 🔒 VÌ SAO PHẢI LÀ ĐIỂM CẮM TƯỜNG MINH (`XC-001` §6):
///   `TT 91/2026/TT-BTC` đã được kiểm tra toàn văn (164.296 ký tự) và **KHÔNG** quy định việc làm tròn
///   (`"làm tròn"` = **0** lần, `"chữ số thập phân"` = **0** lần). ⇒ Làm tròn là **quyết định quản trị**,
///   cần **kế toán trưởng phê duyệt**. Vì vậy nó phải là **cấu hình**, không được hard-code rải rác.
/// </summary>
public interface IRoundingPolicy
{
    /// <summary>Tên chính sách — ghi vào nhật ký/tệp xuất để **truy vết** đã dùng quy tắc nào.</summary>
    string Ten { get; }

    /// <summary>Làm tròn về số chữ số thập phân hợp lệ của loại tiền.</summary>
    Money Round(Money value);
}

/// <summary>
/// Làm tròn **nửa lên** (away from zero) theo số chữ số thập phân của loại tiền.
/// ✅ **Mặc định đang dùng** cho VND: 0 chữ số thập phân, `1000,5 ⇒ 1001`.
/// 📌 Đây là thông lệ kê khai ở VN, nhưng **vẫn là quyết định quản trị** — cần kế toán trưởng xác nhận.
/// </summary>
public sealed class RoundHalfAwayFromZeroPolicy : IRoundingPolicy
{
    public string Ten => "half-away-from-zero";

    public Money Round(Money value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Money.Cua(
            decimal.Round(value.Amount, value.Currency.DecimalDigits, MidpointRounding.AwayFromZero),
            value.Currency);
    }
}

/// <summary>
/// **Không** làm tròn — giữ nguyên giá trị chính xác (dùng cho tính toán trung gian, đối soát).
/// 🔒 Bất biến: đây là chính sách dùng ở **mọi bước trung gian**; chỉ làm tròn ở bước ghi sổ/xuất.
/// </summary>
public sealed class NoRoundingPolicy : IRoundingPolicy
{
    public string Ten => "none";

    public Money Round(Money value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
}
