using System.Text.RegularExpressions;

namespace eCommerce.Tests.Authorization;

/// <summary>
/// 🚨 **CƠ CHẾ: endpoint CÔNG KHAI (`[AllowAnonymous]`) PHẢI ĐƯỢC DUYỆT TRONG ALLOWLIST.**
///
/// **VÌ SAO CẦN (bài học Phụ lục C của gói bàn giao: *ghi quy tắc KHÔNG ĐỦ — cần CƠ CHẾ*):**
/// `XC-004` **BR-004** quy định *"endpoint công khai phải nằm trong allowlist"*, nhưng tới 16/10/2026
/// allowlist đó **chỉ tồn tại trong TÀI LIỆU** — **không** có gì trong mã chặn việc thêm
/// `[AllowAnonymous]` mới. Mà mỗi lần thêm là mỗi lần **mở một cửa ra Internet**:
/// <list type="bullet">
///   <item>`health-status` — cần cho probe (đã có, có lý do);</item>
///   <item>`sitemap.xml` / `robots.txt` (`CAT-003`) — **bắt buộc** công khai vì **máy tìm kiếm không đăng nhập**;</item>
///   <item>bất kỳ endpoint **dữ liệu** nào lọt vào đây ⇒ **rò rỉ dữ liệu không cần xác thực**, và
///     `[Authorize]` ở cấp class của controller đó **không** cứu được (thuộc tính trên method **thắng**).</item>
/// </list>
/// ⇒ Test này buộc người thêm `[AllowAnonymous]` phải **sửa danh sách dưới đây** (hành động **có ý thức**,
/// có review) thay vì thêm âm thầm.
///
/// **CÁCH LÀM — QUÉT NGUỒN (không tham chiếu project `eCommerce.Web`):**
/// 📌 **VÌ SAO KHÔNG DÙNG REFLECTION NHƯ `BatBienPhanQuyenTests`:** project test **cố ý** không tham chiếu
/// `eCommerce.Web`; tham chiếu nó kéo theo `FrameworkReference Microsoft.AspNetCore.App` ⇒ phát sinh
/// **cảnh báo `NU1510`** cho hai gói test đã **ghim để vá lỗ hổng bảo mật**
/// (`System.Security.Cryptography.Xml/Pkcs` 10.0.13) vì chúng *"có sẵn trong framework"*.
/// Dự án bật `TreatWarningsAsErrors` ⇒ cảnh báo đó **chặn build**.
/// 🔒 **Quyết định: KHÔNG đánh đổi bản vá bảo mật lấy tiện lợi của test** ⇒ quét **nguồn**, đúng cách
/// `RangBuocDbChuaAnhXaTests` đã làm với `Assets/schema.sql`.
/// ⚠️ **Hệ quả đã biết (ghi rõ, không giấu):** phép quét đọc **tệp `.cs`**, nên nếu ai đó **sinh** endpoint
/// công khai bằng cách khác (map tay trong `Program`/module, hoặc controller sinh động) thì test này
/// **không thấy**. Vì vậy các cửa map tay được khai **riêng** ở <see cref="MapTayCongKhai"/>.
/// </summary>
public class AllowlistCongKhaiTests
{
    /// <summary>
    /// Endpoint công khai **ĐÃ ĐƯỢC DUYỆT** — `<TênKiểuĐầyĐủ>` hoặc `<TênKiểuĐầyĐủ>.<Method>`.
    /// 🔒 Thêm dòng ở đây = **mở cửa ra Internet** ⇒ phải nêu lý do ngay tại dòng đó.
    /// 📌 **CHỈ khai CẤP CLASS khi `[AllowAnonymous]` đặt ở cấp class** — method **thừa hưởng** nên khai thêm
    /// là **dòng chết** (test này bắt được: lần đầu tôi khai cả 3 `.Get` và nó báo *"Allowlist có dòng KHÔNG còn
    /// endpoint tương ứng"* — đúng như thiết kế: allowlist phải **khớp chính xác** nguồn, không "khai dư cho chắc").
    /// </summary>
    private static readonly string[] Allowlist =
    [
        // `XC-005` BR-012 — điểm đo thống nhất cho MỌI tiến trình. Chỉ trả `{status:"Healthy"}`,
        // KHÔNG đọc DB, KHÔNG lộ chi tiết ⇒ mức rủi ro thấp nhất trong các endpoint công khai.
        "eCommerce.Web.Controllers.HealthStatusController",

        // `CAT-003` — `sitemap.xml` + `robots.txt`: 🚨 **BẮT BUỘC** công khai vì máy tìm kiếm KHÔNG đăng nhập.
        // 🔒 Cả hai CHỈ đọc dữ liệu **đã công khai** (SKU đang niêm yết giá + URL công khai), **không** nhận
        // tham số ⇒ không có đường dò dữ liệu riêng tư qua chúng. Xem ghi chú phạm vi ở đầu mỗi controller.
        "eCommerce.Web.Controllers.SitemapController",
        "eCommerce.Web.Controllers.RobotsController"
    ];

    /// <summary>Cửa công khai map TAY (không có `[AllowAnonymous]`) — cũng phải khai tường minh.</summary>
    private static readonly string[] MapTayCongKhai =
    [
        "eCommerce.HttpApi.Host: /health-status (endpoints.MapHealthChecks)"
    ];

    [Fact]
    public void MOI_AllowAnonymous_phai_nam_trong_allowlist__va_nguoc_lai()
    {
        var trongMa = CacAllowAnonymous();
        var daDuyet = Allowlist.ToHashSet(StringComparer.Ordinal);

        var chuaDuyet = trongMa.Where(x => !daDuyet.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var duyetThua = daDuyet.Where(x => !trongMa.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(
            chuaDuyet.Count == 0,
            "🚨 CÓ `[AllowAnonymous]` CHƯA ĐƯỢC DUYỆT ⇒ endpoint vừa được MỞ RA INTERNET mà không ai review:\n  "
            + string.Join("\n  ", chuaDuyet)
            + "\n\n⇒ Nếu là CHỦ Ý: thêm vào `Allowlist` của test này + ghi vào allowlist `XC-004` §5.4 + NÊU LÝ DO."
            + "\n⇒ Nếu KHÔNG phải chủ ý: XOÁ `[AllowAnonymous]` (dùng `[Authorize]` như mọi endpoint khác).");

        Assert.True(
            duyetThua.Count == 0,
            "🚨 Allowlist có dòng KHÔNG còn endpoint tương ứng (dòng chết ⇒ che mất việc cần duyệt lại):\n  "
            + string.Join("\n  ", duyetThua));
    }

    [Fact]
    public void Phep_quet_KHONG_rong__neu_khong_thi_test_xanh_vo_nghia()
    {
        // 🛡️ Lưới an toàn cho CHÍNH phép quét: nếu đổi cấu trúc thư mục/đổi cách viết thuộc tính thì
        // `CacAllowAnonymous()` trả rỗng ⇒ test trên **xanh vô nghĩa** — đúng loại "cảm giác an toàn giả"
        // mà Phụ lục C cảnh báo. Vì vậy khoá **sàn**.
        var trongMa = CacAllowAnonymous();

        // 🔒 Ngưỡng SÀN = 3 controller công khai hiện có (`HealthStatus` · `Sitemap` · `Robots`).
        Assert.True(trongMa.Count >= 3,
            $"Chỉ đọc được {trongMa.Count} mục `[AllowAnonymous]` ⇒ phép quét HỎNG (mong đợi >= 3).");
        Assert.Contains("eCommerce.Web.Controllers.HealthStatusController", trongMa);
        Assert.Contains("eCommerce.Web.Controllers.SitemapController", trongMa);
    }

    [Fact]
    public void Endpoint_map_TAY_cong_khai__phai_duoc_khai_tuong_minh()
    {
        // 📌 `MapHealthChecks("/health-status")` KHÔNG có `[AllowAnonymous]` (không đi qua MVC) ⇒ phép quét
        // **không thấy** nó. Ghi ra để người sau biết allowlist này **không** bao trùm hết các cửa công khai.
        Assert.Contains(MapTayCongKhai, x => x.Contains("health-status", StringComparison.Ordinal));

        // 🔒 Chốt SỐ LƯỢNG: thêm một cửa map tay mới ⇒ test này ĐỎ ⇒ buộc cập nhật + review.
        Assert.Single(MapTayCongKhai);
    }

    // ── Quét NGUỒN ───────────────────────────────────────────────────────────────────────────────

    // 🔒 Khối thuộc tính NGAY TRƯỚC một khai báo: `[A]`, `[A][B]`, `[A(...)]` — có thể xuống dòng.
    // 📌 VÌ SAO BẮT ĐẦU TỪ KHỐI THUỘC TÍNH (thay vì quét "400 ký tự phía trước"): cách quét thô sẽ **lây**
    // thuộc tính của khai báo TRƯỚC sang khai báo SAU (ví dụ `[AllowAnonymous] class A` rồi `public class B`
    // ngay dưới ⇒ B bị coi là công khai). Khối thuộc tính neo vào **chính** khai báo nên không lây.
    //
    // ⚠️ **BẪY ĐÃ VẤP (ghi lại để không lặp):** lớp ký tự `[^\[\]]*` **KHÔNG** khớp được thuộc tính có **đối số
    // mảng** (ví dụ `[InlineData(new[] { 1 })]`) ⇒ cả khối bị bỏ ⇒ method **không** vào danh sách ⇒ test báo
    // "dòng chết" và lưới an toàn báo "phép quét HỎNG". Dùng khối "không chứa `]`" là đủ cho tệp controller.
    private const string KhoiThuocTinh = @"(?<tt>(?:\s*\[[^\]]*\]\s*)*)";

    private static readonly Regex KieuController = new(
        @"(?m)^[ \t]*" + KhoiThuocTinh + @"(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+(?<ten>\w+)\s*:\s*(?<coSo>[^\r\n{]+)",
        RegexOptions.Compiled);

    private static readonly Regex MethodMvc = new(
        @"(?m)^[ \t]*" + KhoiThuocTinh + @"(?:public|internal)\s+(?:async\s+)?[\w<>,\?\[\]\s\.]+\s+(?<ten>\w+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex KhaiBaoNamespace = new(@"(?m)^\s*namespace\s+(?<ns>[\w\.]+)\s*[;{]", RegexOptions.Compiled);

    /// <summary>Mọi `[AllowAnonymous]` **trong 2 tiến trình có endpoint HTTP** (Host + Web) ⇒ `TênKiểu[.Method]`.</summary>
    private static List<string> CacAllowAnonymous()
    {
        var ket = new List<string>();
        var goc = TimGocRepo();

        foreach (var tm in ThuMucCanQuet(goc))
        {
            if (!Directory.Exists(tm))
            {
                continue;
            }

            foreach (var tep in Directory.EnumerateFiles(tm, "*.cs", SearchOption.AllDirectories))
            {
                if (LaTepSinhRa(tep))
                {
                    continue;
                }

                var noiDung = File.ReadAllText(tep);
                var ns = KhaiBaoNamespace.Match(noiDung) is { Success: true } m ? m.Groups["ns"].Value : string.Empty;

                // ① `[AllowAnonymous]` ở cấp CLASS.
                foreach (Match k in KieuController.Matches(noiDung))
                {
                    if (!k.Groups["coSo"].Value.Contains("Controller", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var tenKieu = TenDayDu(ns, k.Groups["ten"].Value);

                    if (CoAllowAnonymous(k.Groups["tt"].Value))
                    {
                        ket.Add(tenKieu);
                    }
                }

                // ② `[AllowAnonymous]` ở cấp METHOD MVC.
                // 🔒 **VÌ SAO KHÔNG CẦN BIẾT METHOD THUỘC KIỂU NÀO (và vì sao đây là cách ĐÚNG):**
                // bản đầu tôi còn đếm ngoặc nhọn để biết method nằm trong kiểu nào — phép đếm đó **sai** khi
                // khối thân kiểu bắt đầu sau một thuộc tính (đã đo: nó cắt khối thân **quá sớm** ⇒ method bị
                // coi là ngoài kiểu ⇒ test báo *"dòng chết"* + *"phép quét HỎNG"*).
                // 📌 Trong tệp controller, `[AllowAnonymous]` trên một **action MVC** là dấu hiệu **đủ**:
                // mỗi tệp controller chỉ chứa **một** kiểu controller là chủ thể (các kiểu phụ nếu có thì
                // đều **không** phải controller). Nếu ai thêm `[AllowAnonymous]` cho một action của kiểu phụ,
                // tên sẽ xuất hiện với namespace — vẫn **đỏ** ⇒ vẫn được review.
                var tenKieuChinh = KieuChinhTrongTep(noiDung, ns);

                if (tenKieuChinh is null)
                {
                    continue;
                }

                foreach (Match p in MethodMvc.Matches(noiDung))
                {
                    var tt = p.Groups["tt"].Value;

                    // 🔒 CHỈ method của MVC (`[Http*]`/`[Route]`) — bỏ hàm trợ giúp ⇒ mỗi dòng là **một cửa HTTP thật**.
                    if (!tt.Contains("[Http", StringComparison.Ordinal) && !tt.Contains("[Route", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (CoAllowAnonymous(tt))
                    {
                        ket.Add(tenKieuChinh + "." + p.Groups["ten"].Value);
                    }
                }
            }
        }

        return ket.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>Thư mục chứa endpoint HTTP: **Web controllers** + **Host**.</summary>
    private static string[] ThuMucCanQuet(string goc) =>
    [
        Path.Combine(goc, "src", "eCommerce.Web", "Controllers"),
        Path.Combine(goc, "src", "eCommerce.HttpApi.Host")
    ];

    /// <summary>Tên đầy đủ của kiểu controller **chủ thể** trong tệp (`null` nếu tệp không có controller).</summary>
    private static string? KieuChinhTrongTep(string noiDung, string ns)
    {
        foreach (Match k in KieuController.Matches(noiDung))
        {
            if (k.Groups["coSo"].Value.Contains("Controller", StringComparison.Ordinal))
            {
                return TenDayDu(ns, k.Groups["ten"].Value);
            }
        }

        return null;
    }

    private static string TenDayDu(string ns, string ten)
        => string.IsNullOrEmpty(ns) ? ten : ns + "." + ten;

    private static bool LaTepSinhRa(string duongDan)
        => duongDan.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
           || duongDan.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static bool CoAllowAnonymous(string khoiThuocTinh)
        => khoiThuocTinh.Contains("[AllowAnonymous", StringComparison.Ordinal);

    // ── Tìm gốc repo (test chạy từ `bin/…` nên phải đi ngược lên) ────────────────────────────────

    private static string TimGocRepo()
        => TimThuMuc(Path.Combine("src", "eCommerce.Web", "Controllers"));

    private static string TimThuMuc(string duongDanTuongDoi)
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            var ungVien = Path.Combine(thuMuc.FullName, duongDanTuongDoi);

            if (Directory.Exists(ungVien))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Không tìm thấy '{duongDanTuongDoi}' khi đi ngược từ '{AppContext.BaseDirectory}'. " +
            "Test này cần gốc repo (chạy `dotnet test` từ trong repo).");
    }
}
