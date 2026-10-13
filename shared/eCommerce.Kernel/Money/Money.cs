using Volo.Abp;

namespace eCommerce.Kernel.Money;

/// <summary>
/// **Khoản tiền** = số lượng + loại tiền tệ. Bất biến (immutable).
///
/// 🚨 VÌ SAO PHẢI CÓ LỚP NÀY (chứ không dùng `decimal` trần như code hiện tại):
///   1. **`decimal` trần mất loại tiền** ⇒ cộng 100 USD với 100.000 VND vẫn ra 100.100 mà **không lỗi**.
///      Đây là **lỗi im lặng** nguy hiểm nhất của miền tiền tệ.
///   2. **`decimal` trần giữ scale lạ** (`0.10m * 100m = 10.00m`) ⇒ dễ in ra "10.00" thay vì "10"
///      *(đúng lỗi tôi đã gặp thật khi viết bộ xuất Mẫu 01 — xem `Mau01CsvExporter`)*.
///   3. **Không có chỗ để áp chính sách làm tròn** theo số chữ số thập phân của loại tiền.
///
/// 🔒 QUY Ước DÙNG: trong code nghiệp vụ hãy so sánh bằng <see cref="CompareTo(Money)"/> hoặc
/// <see cref="SoSanh"/>; phép <c>==</c> ở đây **đã** kiểm cả loại tiền nên an toàn, nhưng **không** được
/// viết `amount1 == amount2` trên `decimal` trần.
///
/// 📌 Làm tròn **KHÔNG** tự động: mọi phép toán trả về giá trị **chính xác**; muốn làm tròn phải gọi
/// <see cref="IRoundingPolicy"/> một cách tường minh (chính sách làm tròn là **quyết định quản trị** —
/// xem `XC-001` §6, `TT 91/2026` không quy định).
/// </summary>
public sealed class Money : IEquatable<Money>, IComparable<Money>
{
    public decimal Amount { get; }

    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Tạo khoản tiền. **Không** tự làm tròn — xem <see cref="IRoundingPolicy"/>.</summary>
    public static Money Cua(decimal amount, Currency currency)
        => new(amount, currency ?? throw new BusinessException(MoneyErrorCodes.CurrencyMismatch));

    public static Money Vnd(decimal amount) => new(amount, Currency.Vnd);
    public static Money Usd(decimal amount) => new(amount, Currency.Usd);

    public static Money Zero(Currency currency) => new(0m, currency);

    public bool IsZero => Amount == 0m;
    public bool IsNegative => Amount < 0m;
    public bool IsPositive => Amount > 0m;

    /// <summary>Kiểm tra cùng loại tiền; ném lỗi nếu khác — dùng trước mọi phép toán hai ngôi.</summary>
    private void YeuCauCungLoaiTien(Money other)
    {
        if (!ReferenceEquals(Currency, other.Currency) && Currency != other.Currency)
        {
            throw new BusinessException(MoneyErrorCodes.CurrencyMismatch)
                .WithData("currencyA", Currency.Code)
                .WithData("currencyB", other.Currency.Code);
        }
    }

    // ── Phép toán ───────────────────────────────────────────────────────────

    public static Money operator +(Money a, Money b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        a.YeuCauCungLoaiTien(b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        a.YeuCauCungLoaiTien(b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator -(Money a) => new(-a.Amount, a.Currency);

    /// <summary>Nhân với hệ số (số lượng, tỷ lệ). Kết quả **chưa** làm tròn.</summary>
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor, a.Currency);

    public static Money operator *(decimal factor, Money a) => a * factor;

    /// <summary>Chia theo hệ số. Chia cho 0 ⇒ ném lỗi (không trả `Infinity`/`NaN`).</summary>
    public static Money operator /(Money a, decimal divisor)
    {
        if (divisor == 0m)
        {
            throw new BusinessException(MoneyErrorCodes.InvalidDivisor).WithData("divisor", divisor);
        }

        return new Money(a.Amount / divisor, a.Currency);
    }

    /// <summary>Tỷ lệ `a / b` (cùng loại tiền) — dùng cho biểu đồ/báo cáo.</summary>
    public static decimal TyLe(Money a, Money b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        a.YeuCauCungLoaiTien(b);

        if (b.IsZero)
        {
            throw new BusinessException(MoneyErrorCodes.InvalidDivisor).WithData("divisor", 0);
        }

        return a.Amount / b.Amount;
    }

    // ── So sánh ─────────────────────────────────────────────────────────────

    public int CompareTo(Money? other)
    {
        if (other is null)
        {
            return 1;
        }

        YeuCauCungLoaiTien(other);
        return Amount.CompareTo(other.Amount);
    }

    public bool Equals(Money? other)
        => other is not null
           && (ReferenceEquals(Currency, other.Currency) || Currency == other.Currency)
           && Amount == other.Amount;

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => HashCode.Combine(Currency.Code, Amount);

    public static bool operator ==(Money? a, Money? b)
        => a is null ? b is null : a.Equals(b);

    public static bool operator !=(Money? a, Money? b) => !(a == b);

    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;

    public override string ToString()
        => $"{Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)} {Currency.Code}";
}
