using System.Diagnostics.CodeAnalysis;
using Volo.Abp;

namespace eCommerce.Kernel.Money;

/// <summary>
/// Loại tiền tệ — **dữ liệu**, không phải `enum`, để thêm loại tiền mới không phải sửa code nghiệp vụ.
///
/// 🔒 BẤT BIẾN (`XC-001` BR-001): **số chữ số thập phân là thuộc tính của LOẠI TIỀN**, không phải của
/// từng khoản tiền. `VND` có **0** chữ số thập phân ⇒ `1234.5 VND` là **sai** (phải làm tròn trước).
/// Đây là lý do phải có lớp làm tròn tường minh thay vì để `decimal` tự do.
///
/// 📌 Vì sao không dùng `string` trần cho loại tiền: so sánh `"vnd"` với `"VND"` bằng `==` sẽ **im lặng sai**.
/// Dùng <see cref="Currency"/> thì luôn chuẩn hoá về chữ HOA và so sánh theo **mã**.
/// </summary>
public sealed record Currency
{
    /// <summary>Việt Nam đồng — 0 chữ số thập phân.</summary>
    public static readonly Currency Vnd = new("VND", 0, "₫", "Việt Nam đồng");

    /// <summary>Đô la Mỹ — 2 chữ số thập phân (dùng cho nhập khẩu/đối tác nước ngoài).</summary>
    public static readonly Currency Usd = new("USD", 2, "$", "Đô la Mỹ");

    private static readonly Dictionary<string, Currency> DaDangKy = new(StringComparer.OrdinalIgnoreCase)
    {
        [Vnd.Code] = Vnd,
        [Usd.Code] = Usd
    };

    /// <summary>Mã ISO 4217, luôn CHỮ HOA (ví dụ <c>VND</c>).</summary>
    public string Code { get; }

    /// <summary>Số chữ số thập phân hợp lệ của loại tiền này.</summary>
    public int DecimalDigits { get; }

    /// <summary>Ký hiệu hiển thị (chỉ dùng cho giao diện, KHÔNG dùng để tính toán).</summary>
    public string Symbol { get; }

    /// <summary>Tên đọc được, dùng cho thông báo lỗi.</summary>
    public string Name { get; }

    private Currency(string code, int decimalDigits, string symbol, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3 || code != code.ToUpperInvariant())
        {
            throw new BusinessException(MoneyErrorCodes.InvalidAmount)
                .WithData("code", code);
        }

        if (decimalDigits is < 0 or > 4)
        {
            throw new BusinessException(MoneyErrorCodes.InvalidAmount)
                .WithData("decimalDigits", decimalDigits);
        }

        Code = code;
        DecimalDigits = decimalDigits;
        Symbol = symbol;
        Name = name;
    }

    /// <summary>
    /// Lấy loại tiền theo mã (không phân biệt chữ hoa/thường).
    /// 🚨 Ném lỗi khi chưa hỗ trợ — **KHÔNG** im lặng trả về mặc định, vì như vậy hoá đơn ngoại tệ
    /// sẽ bị tính như VND mà không ai biết.
    /// </summary>
    public static Currency FromCode(string code)
    {
        if (!string.IsNullOrWhiteSpace(code) && DaDangKy.TryGetValue(code.Trim(), out var currency))
        {
            return currency;
        }

        throw new BusinessException(MoneyErrorCodes.UnsupportedCurrency)
            .WithData("code", code);
    }

    public static bool TryFromCode(string? code, [NotNullWhen(true)] out Currency? currency)
    {
        currency = null;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return DaDangKy.TryGetValue(code.Trim(), out currency);
    }

    /// <summary>Đăng ký thêm loại tiền (dùng khi mở rộng; gọi một lần lúc khởi động).</summary>
    public static void DangKy(Currency currency) => DaDangKy[currency.Code] = currency;

    public override string ToString() => Code;
}
