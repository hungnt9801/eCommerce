namespace eCommerce.Kernel.Money;

/// <summary>
/// Mã lỗi của miền **tiền tệ** — theo quy ước `eCommerce:Money:nnnn` (`XC-001`, §8).
/// 🔒 Do CHÍNH Kernel sở hữu (quy ước: module/thư viện sở hữu hằng số của mình).
/// </summary>
public static class MoneyErrorCodes
{
    /// <summary>Cộng/trừ/so sánh hai khoản tiền KHÁC loại tiền tệ.</summary>
    public const string CurrencyMismatch = "eCommerce:Money:0001";

    /// <summary>Chia cho 0 hoặc cho giá trị không hợp lệ.</summary>
    public const string InvalidDivisor = "eCommerce:Money:0002";

    /// <summary>Mã tiền tệ chưa được hỗ trợ (chưa đăng ký trong <see cref="Currency"/>).</summary>
    public const string UnsupportedCurrency = "eCommerce:Money:0003";

    /// <summary>Tỷ lệ phân bổ không hợp lệ (âm, hoặc tổng bằng 0).</summary>
    public const string InvalidAllocationRatio = "eCommerce:Money:0004";

    /// <summary>Giá trị tiền không hợp lệ (ví dụ số chữ số thập phân vượt quá loại tiền cho phép).</summary>
    public const string InvalidAmount = "eCommerce:Money:0005";
}
