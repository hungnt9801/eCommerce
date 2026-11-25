using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🚨 **BẤT BIẾN DẢI MÃ LỖI CỦA PRICING** (`PRC-002` `-arch` §3.4) — *"append cuối dải, không cấp lại số cũ"*.
///
/// Mã lỗi là **hợp đồng với client/handler**: đổi số hoặc cấp trùng số làm client bắt sai nhánh xử lý —
/// và **không có lỗi biên dịch nào** chỉ ra. Mọi `const string` trong `PricingErrorCodes` phải:
///   (a) bắt đầu `eCommerce:Pricing:`;
///   (b) **không trùng** giá trị;
///   (c) hậu tố **4 chữ số**, liên tục từ `0001` trong dải đã dùng (không nhảy số, không quay lại số cũ).
/// </summary>
public class PricingErrorCodesTests
{
    private const string TienTo = "eCommerce:Pricing:";

    private static readonly string[] CacMa = typeof(PricingErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Phai_co_ma_loi__khong_duoc_rong()
    {
        // Nếu reflection không thấy hằng nào ⇒ ba ca dưới "xanh vô nghĩa". Chặn trước.
        Assert.NotEmpty(CacMa);
    }

    [Fact]
    public void Moi_ma_bat_dau_bang_eCommerce_Pricing()
    {
        Assert.All(CacMa, ma =>
            Assert.StartsWith(TienTo, ma, StringComparison.Ordinal));
    }

    [Fact]
    public void Khong_co_hai_hang_so_cung_gia_tri()
    {
        Assert.Equal(CacMa.Length, CacMa.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Hau_to_la_4_chu_so_tang_lien_tuc_tu_0001_khong_nhay()
    {
        var hauTo = CacMa.Select(ma => ma[TienTo.Length..]).ToArray();

        Assert.All(hauTo, s => Assert.Matches(new Regex("^[0-9]{4}$"), s));

        var so = hauTo.Select(int.Parse).OrderBy(x => x).ToArray();

        // 0001..N liên tục — thiếu một số nghĩa là "đã từng có mã bị xoá/đổi số" (cấm cấp lại).
        Assert.Equal(Enumerable.Range(1, so.Length).ToArray(), so);
    }
}
