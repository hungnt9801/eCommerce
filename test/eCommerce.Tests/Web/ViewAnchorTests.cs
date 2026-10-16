using System.Text.RegularExpressions;

namespace eCommerce.Tests.Web;

/// <summary>
/// 🚨 **LƯỚI AN TOÀN: MỌI VIEW PHẢI CÓ "NEO HIỂN THỊ" NẰM NGOÀI CHÚ THÍCH RAZOR** (`WEB-001`).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// **LỖI SẢN PHẨM THẬT MÀ FILE NÀY TỒN TẠI ĐỂ CHẶN (đo được ở lượt 20):**
///
/// `/KhachHang` render **"Dữ liệu cá nhân."** nhưng **KHÔNG** có **`91/2025/QH15`** — vì chuỗi đó **chỉ**
/// nằm trong khối chú thích `@* … *@`, mà **Razor KHÔNG render chú thích**.
///
/// 🔒 **VÌ SAO ĐÓ LÀ LỖI NGHIÊM TRỌNG, KHÔNG PHẢI CHUYỆN NHỎ:** người dùng **và người kiểm toán**
/// **không thấy căn cứ pháp lý nào**, trong khi đó là **LÝ DO** màn hình bị giới hạn trường.
/// 📌 Một khiếu nại *"tại sao các anh giới hạn dữ liệu của tôi"* cần câu trả lời **nằm trong sản phẩm**,
/// **không phải trong mã nguồn**.
/// ⚠️ **Và nó không có triệu chứng nào:** trang `200`, vẫn render, chỉ **thiếu một câu** — nên
/// **1101 unit test + 11 ca probe** đều **không** thấy.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔑 **CÁCH LÀM — và vì sao "neo" phải là ASCII THUẦN:**
///
/// Mỗi view khai một **neo ASCII** (ví dụ `[HANMUCKENH-MAN-HINH]`) trong **văn bản hiển thị**.
/// Test này **cắt hết chú thích Razor** rồi khẳng định neo **vẫn còn**.
///
/// 📌 **VÌ SAO NEO PHẢI LÀ ASCII:**
/// <list type="bullet">
///   <item>**Razor escape ký tự tiếng Việt thành ENTITY SỐ** (`Đ` → `&#x110;`) ⇒ khẳng định **chữ có dấu**
///     trên HTML sẽ **thất bại dù chuỗi CÓ** (bài học **hai lần** trong dự án: `sal-005` §19.5, `web-001` §97.6);</item>
///   <item>Neo ASCII **không** phụ thuộc cách mã hoá ⇒ dùng được cho **cả** test đọc **mã nguồn**
///     **lẫn** probe đọc **HTML**.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** test này kiểm **neo**, **không** kiểm **cả câu**. 📌 Nghĩa là nó bảo đảm
/// *"view có một dòng hiển thị được"*, **không** bảo đảm *"câu đó đúng"*. 🔑 Đó là đánh đổi **có chủ ý**:
/// kiểm nội dung câu là việc của **review người**, còn kiểm *"có hiển thị hay không"* là việc **máy**.
/// </summary>
public class ViewAnchorTests
{
    /// <summary>Bảng neo: **đường dẫn view** → **neo bắt buộc phải HIỂN THỊ** (ngoài chú thích).</summary>
    private static readonly (string Tep, string Neo)[] BangNeo =
    [
        ("Views/HanMucKenh/Index.cshtml", "[HANMUCKENH-MAN-HINH]"),
        ("Views/KhachHang/Index.cshtml", "[KHACHHANG-MAN-HINH]"),
        ("Views/DoiSoatThanhToan/Index.cshtml", "[DOISOATTHANHTOAN-MAN-HINH]"),
        ("Views/Loi/ChanQuyen.cshtml", "[LOI-403-THIEP-QUYEN]"),
        ("Views/Loi/KhongTimThay.cshtml", "[LOI-404-KHONG-CO-MAN-HINH]")
    ];

    [Fact]
    public void MOI_view__co_NEO_HIEN_THI__nam_NGOAI_chu_thich_Razor()
    {
        var goc = TimGocRepo();
        var viPham = new List<string>();

        foreach (var (tep, neo) in BangNeo)
        {
            var duongDan = Path.Combine(goc, "src", "eCommerce.Web", tep.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(duongDan))
            {
                viPham.Add($"{tep}: KHÔNG TÌM THẤY TỆP");
                continue;
            }

            var khongChuThich = CatChuThichRazor(File.ReadAllText(duongDan));

            if (!khongChuThich.Contains(neo, StringComparison.Ordinal))
            {
                viPham.Add($"{tep}: neo `{neo}` KHÔNG nằm trong phần HIỂN THỊ");
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 VIEW THIẾU NEO HIỂN THỊ ⇒ thứ đáng lẽ người dùng PHẢI thấy lại **chỉ nằm trong chú thích**:\n  "
            + string.Join("\n  ", viPham)
            + "\n\n⇒ Razor **KHÔNG** render `@* … *@`. 📌 Đây đúng lỗi đã xảy ra với `91/2025/QH15`"
            + "\n   (căn cứ pháp lý của `CUS-001`) — người dùng VÀ người kiểm toán không thấy gì."
            + "\n🔒 Sửa: đưa câu/neo đó vào **văn bản hiển thị** (ngoài khối chú thích).");
    }

    [Fact]
    public void Bang_neo__khong_duoc_RONG__va_phai_tro_toi_tep_THAT()
    {
        // 🛡️ Lưới an toàn cho CHÍNH lưới an toàn: nếu bảng neo rỗng (ai đó xoá) thì test trên **XANH VÔ NGHĨA**.
        Assert.NotEmpty(BangNeo);

        // 🔒 Và neo phải là **ASCII THUẦN** — xem chú thích lớp để biết vì sao (Razor escape tiếng Việt).
        foreach (var (tep, neo) in BangNeo)
        {
            Assert.True(neo.All(c => c <= 127),
                $"Neo `{neo}` trong `{tep}` chứa ký tự NGOÀI ASCII ⇒ khẳng định trên HTML sẽ HỎNG vì Razor escape.");
        }

        var tatCaView = Directory
            .EnumerateFiles(Path.Combine(TimGocRepo(), "src", "eCommerce.Web", "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !Path.GetFileName(p).StartsWith('_'))
            .ToList();

        Assert.True(tatCaView.Count >= 5,
            $"Chỉ thấy {tatCaView.Count} view ⇒ phép quét hỏng (hoặc view bị chuyển chỗ).");
    }

    /// <summary>
    /// **CẮT CHÚ THÍCH RAZOR** (`@* … *@`) — 🔑 chính việc này làm test **có giá trị**.
    /// 📌 Dùng regex **non-greedy** (`.*?` với `Singleline`) — ⚠️ và **đúng** như vậy ở đây vì ta **muốn**
    /// khớp từng khối một. 🔒 **Razor KHÔNG hỗ trợ chú thích lồng nhau** (bài học `RZ1003` ở lượt 20),
    /// nên non-greedy là **khớp đúng ngữ nghĩa của Razor**.
    /// </summary>
    private static string CatChuThichRazor(string cshtml)
        => Regex.Replace(cshtml, @"@\*.*?\*@", " ", RegexOptions.Singleline);

    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (Directory.Exists(Path.Combine(thuMuc.FullName, "src", "eCommerce.Web")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy `src/eCommerce.Web` khi đi lên từ " + AppContext.BaseDirectory);
    }
}
