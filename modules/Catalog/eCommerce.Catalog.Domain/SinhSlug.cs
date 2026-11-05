using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **SINH & CHUẨN HOÁ SLUG SEO — HÀM THUẦN** (`CAT-003` **ADR-04**).
///
/// **VÌ SAO TÁCH RIÊNG VÀ LÀ HÀM THUẦN:** slug là **khoá định danh công khai** trên URL ⇒ sai một ký tự là
/// **404 với khách** hoặc **hai sản phẩm cùng một URL**. Đây là chỗ **dễ sai** và **không cần DB**
/// ⇒ tách ra để **kiểm được bằng unit test** trước khi tin nó trong đường ghi.
///
/// 🔒 **Vì sao TẤT ĐỊNH (không `slug-{guid}`):** `CAT-003` ADR-04 đã loại `Guid` vì **không có giá trị SEO**
/// (`WEB-002` ADR-03) — URL phải đọc được và chứa từ khoá.
///
/// ⚠️ **Tiếng Việt là phần khó nhất** (và là lý do phải có test riêng):
/// <list type="bullet">
///   <item><c>đ</c>/<c>Đ</c> **không** phân rã được bằng NFD (nó là **một ký tự** `U+0111`) ⇒ **bắt buộc** xử lý tay,
///     nếu không `đường` thành <c>ng-</c> hoặc mất hẳn ký tự;</item>
///   <item>các dấu còn lại (`ư`, `ơ`, `ả`, `ệ`…) thì NFD **tách được** thành *ký tự gốc + dấu tổ hợp*
///     ⇒ lọc `NonSpacingMark` là xong.</item>
/// </list>
/// 📌 Đây là **cái bẫy thật**: nếu chỉ lọc `NonSpacingMark` mà quên `đ` thì *"Đường"* ⇒ <c>"ng"</c>
/// và **không có lỗi nào** — URL vẫn hợp lệ, chỉ là **sai chữ**.
/// </summary>
public static class SinhSlug
{
    /// <summary>Độ dài tối đa của slug — khớp `HasMaxLength(512)` của `CatProducts.Slug`.</summary>
    public const int DoDaiToiDa = 512;

    private static readonly Regex NhieuGach = new("-{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Sinh slug từ tên sản phẩm: **bỏ dấu tiếng Việt → chữ thường → chỉ giữ `[a-z0-9-]` → gộp `-` → cắt 2 đầu**.
    ///
    /// 🔒 **Trả về CHUỖI RỖNG khi không còn ký tự dùng được** (ví dụ tên toàn ký tự đặc biệt).
    /// 📌 Cố ý **KHÔNG** bịa ra slug từ `Guid`: hàm thuần **không được** che dữ liệu vào vô nghĩa —
    /// chỗ gọi phải **quyết định** (ném lỗi, hay để `Slug = null` ⇒ sản phẩm **chưa có** URL công khai).
    /// </summary>
    public static string Tu(string? ten)
    {
        if (string.IsNullOrWhiteSpace(ten))
        {
            return string.Empty;
        }

        // ① `đ`/`Đ` TRƯỚC khi phân rã Unicode — xem ghi chú ở đầu lớp (đây là cái bẫy thật).
        var thay = ten.Replace('đ', 'd').Replace('Đ', 'D');

        // ② Tách dấu tổ hợp (NFD) rồi BỎ các dấu đó ⇒ `ư` → `u`, `ả` → `a`, `ệ` → `e`.
        var nfd = thay.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);

        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var khongDau = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // ③ Chỉ giữ `[a-z0-9]`, mọi thứ khác thành `-`.
        var chi = new StringBuilder(khongDau.Length);
        foreach (var c in khongDau)
        {
            chi.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        }

        // ④ Gộp `-` liên tiếp + cắt `-` ở hai đầu (nếu không: `"a  b"` ⇒ `"a--b"`, `" a "` ⇒ `"-a-"`).
        var slug = NhieuGach.Replace(chi.ToString(), "-").Trim('-');

        return slug.Length <= DoDaiToiDa ? slug : slug[..DoDaiToiDa].Trim('-');
    }

    /// <summary>
    /// Thêm **hậu tố số** để phá trùng: `-2`, `-3`… (`CAT-003` **ADR-04**).
    ///
    /// 📌 **VÌ SAO HẬU TỐ SỐ (không phải `Guid` ngắn):** người đọc vẫn hiểu URL, và thứ tự *"cái đầu tiên giữ
    /// bản sạch"* ⇒ **KHÔNG** làm hỏng URL đã được Google index của bản cũ (bài học ADR-05: đổi slug = mất SEO).
    /// 🔒 `lanThu &lt; 2` ⇒ trả **nguyên** slug (lần thử đầu **không** được có hậu tố).
    /// </summary>
    public static string ThemHauTo(string slug, int lanThu)
    {
        if (string.IsNullOrWhiteSpace(slug) || lanThu < 2)
        {
            return slug;
        }

        var hauTo = "-" + lanThu.ToString(CultureInfo.InvariantCulture);

        // ⚠️ Phải CẮT BỚT phần đầu nếu tổng vượt trần — nếu không, slug vượt `HasMaxLength(512)` sẽ bị DB chặn
        // (hoặc tệ hơn: nếu cột là `text` thì âm thầm dài ra và vỡ `canonical`).
        if (slug.Length + hauTo.Length <= DoDaiToiDa)
        {
            return slug + hauTo;
        }

        return slug[..(DoDaiToiDa - hauTo.Length)].Trim('-') + hauTo;
    }

    /// <summary>
    /// Sinh danh sách **ứng viên slug theo thứ tự thử** — hàm **THUẦN, ĐỒNG BỘ** (không chạm DB).
    /// 📌 **VÌ SAO TRẢ `IEnumerable` (không nhận callback async):** lần đầu tôi viết
    /// `ChuaTrung(ten, Func<string,bool>)` rồi ở app service phải gọi
    /// <c>CoSlugKhacDungAsync(s).GetAwaiter().GetResult()</c> — **chặn luồng** trong đường async
    /// (dễ deadlock, và nhiều nhà cung cấp DB **cấm** gọi đồng bộ). Cách đúng: hàm thuần **sinh ra**
    /// ứng viên, chỗ gọi **await** việc kiểm trùng rồi **dừng ở ứng viên đầu tiên chưa dùng**.
    /// 🔒 Nhờ vậy hàm này **test được không cần DB** mà vẫn không ép tầng gọi dùng `.Result`.
    /// </summary>
    /// <param name="ten">Tên sản phẩm (hoặc slug người dùng nhập — đã chuẩn hoá).</param>
    /// <param name="toiDa">Số ứng viên tối đa (mặc định 100).</param>
    public static IEnumerable<string> CacUngVien(string? ten, int toiDa = 100)
    {
        if (toiDa < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(toiDa), toiDa, "Số ứng viên phải >= 1.");
        }

        var goc = Tu(ten);

        if (goc.Length == 0)
        {
            yield break;
        }

        for (var lan = 1; lan <= toiDa; lan++)
        {
            yield return ThemHauTo(goc, lan);
        }
    }
}
