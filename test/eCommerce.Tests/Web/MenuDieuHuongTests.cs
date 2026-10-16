using System.Text.RegularExpressions;

namespace eCommerce.Tests.Web;

/// <summary>
/// 🚨 **MENU ĐIỀU HƯỚNG LỌC THEO QUYỀN** (`WEB-001`) — kiểm bằng **QUÉT NGUỒN**.
///
/// **VÌ SAO QUÉT NGUỒN (không `ProjectReference` tới `eCommerce.Web`):**
/// `eCommerce.Tests` là unit test **không** tham chiếu app web — kéo `eCommerce.Web` vào sẽ mang theo
/// cả host Razor/DI ⇒ test nặng, dễ vỡ vì lý do **không liên quan**. 📌 Đây **đúng khuôn đã dùng** ở
/// `AllowlistCongKhaiTests` (quét `[AllowAnonymous]`) và `BatBienPhanQuyenTests`.
///
/// 🔑 **Ba bất biến then chốt (mỗi cái đều có thể gây lỗi IM LẶNG):**
/// <list type="number">
///   <item>🚨 **MỌI `Url` trong menu PHẢI có controller thật** — mục menu trỏ tới route không tồn tại biến
///     **404** thành *"tính năng trông như đã có"*: người dùng bấm vào, thấy lỗi, và **không ai** phân biệt
///     được *"chưa làm"* với *"hệ thống hỏng"*. 📌 Chính tôi đã **suýt** khai `/KhachHang`, `/ThanhToan`,
///     `/HanMucKenh` (API của chúng **đã có thật**) trong khi **ba trang đó chưa tồn tại**.</item>
///   <item>**MỌI quyền khai trong menu PHẢI là quyền CÓ THẬT** — khai sai tên ⇒ `IAuthorizationService`
///     **luôn** từ chối ⇒ mục **vĩnh viễn ẩn** với **mọi** người kể cả admin, và **không có lỗi nào**
///     (cùng họ `F-403`/`XC-004` D2).</item>
///   <item>**Lõi lọc phải là hàm THUẦN** (`BoLocDieuHuong.Loc`) và **fail-closed** — kiểm bằng quét nguồn
///     vì lõi đó **không** được phép phụ thuộc ABP/Razor (đó là điều kiện để test được **mọi** nhánh).</item>
/// </list>
/// </summary>
public class MenuDieuHuongTests
{
    private static string GocRepo()
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

    private static string Doc(string duongDanTuongDoi)
    {
        var day = Path.Combine(GocRepo(), duongDanTuongDoi.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(day), $"Không tìm thấy tệp: {duongDanTuongDoi}");

        return File.ReadAllText(day);
    }

    private static string NguonMenu() => Doc("src/eCommerce.Web/Navigation/MenuDieuHuong.cs");

    private static string NguonBoLoc() => Doc("src/eCommerce.Web/Navigation/BoLocDieuHuong.cs");

    /// <summary>Các cặp `(Url, QuyenCanCo)` khai trong `MenuDieuHuong.cs`, **theo thứ tự nguồn**.</summary>
    private static List<(string Url, string? Quyen)> CacMucMenu()
    {
        var nguon = NguonMenu();
        var ketQua = new List<(string Url, string? Quyen)>();

        // Mỗi mục là một `new() { ... Url = "...", ... QuyenCanCo = <biểu thức>, ... }`.
        //
        // 🚨 **BẪY ĐÃ VẤP (lần chạy đầu test ĐỎ với "không tìm thấy mục menu nào"):** phiên bản đầu dùng
        // `new\(\)\s*\{(?<than>.*?)\}` với `RegexOptions.Singleline`. Nó **sai** vì trong tệp có **COMMENT**
        // chứa dấu `{` và `}` (ví dụ `// ... `{`/`}` ...`). Ở chế độ `Singleline`, `.*?` **ăn xuyên dòng**,
        // nên `(?<than>.*?)` dừng ở dấu `}` **ĐẦU TIÊN** — mà dấu đó nằm **trong comment** ⇒ phần "thân" bị
        // **cắt vụn** ⇒ không tìm thấy `Url` ⇒ **0 mục**.
        // 🔒 Cách sửa: khớp **theo DÒNG** (`[^\r\n]*`) cho tới dấu `}` **đầu tiên ở CUỐI DÒNG**
        // (`\}\s*$` với `Multiline`) — đúng hình dạng khai của tệp (mỗi mục kết thúc bằng `}` trên dòng riêng).
        //
        // 🚨 **BẪY THỨ HAI (test vẫn ĐỎ sau khi sửa lần 1):** tệp khai
        //     `new()`
        //     `{`
        //     `    Nhan = "...",`
        // ⇒ `new()` và `{` nằm **KHÁC DÒNG**. Mẫu cũ đòi `new\(\)\s*\{` liền nhau ⇒ **0 mục**.
        // 🔒 Nay cho phép xuống dòng giữa `new()` và `{`, **và** phần thân bắt đầu từ dòng **sau** `{`.
        // 📌 Và nếu regex vẫn hỏng thì **lưới an toàn** bên dưới (`mucMenu.Count > 0`) sẽ **đỏ**, chứ không
        // để test xanh vô nghĩa — đúng bài học "cơ chế bị vô hiệu hoá im lặng thì tệ hơn không có".
        var khuonMuc = new Regex(
            @"new\(\)\s*\{\s*\r?\n(?<than>(?:[^\r\n]*\r?\n)*?[^\r\n]*\}\s*$)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        var khuonUrl = new Regex(@"Url\s*=\s*""(?<url>[^""]+)""", RegexOptions.Compiled);
        var khuonQuyen = new Regex(
            @"QuyenCanCo\s*=\s*(?<quyen>eCommerceConsts\.[A-Za-z0-9_.]+|null)",
            RegexOptions.Compiled);

        foreach (Match m in khuonMuc.Matches(nguon))
        {
            var than = m.Groups["than"].Value;
            var url = khuonUrl.Match(than);

            if (!url.Success)
            {
                continue;
            }

            var quyen = khuonQuyen.Match(than);

            ketQua.Add((url.Groups["url"].Value, quyen.Success ? quyen.Groups["quyen"].Value : null));
        }

        return ketQua;
    }

    /// <summary>
    /// 🚨 **BỘ QUÉT MỤC CON (menu HAI TẦNG)** — lượt 19/40 (`WEB-001`).
    ///
    /// 📌 **VÌ SAO PHẢI CÓ HÀM RIÊNG (không dùng lại regex của `CacMucMenu`):** regex đó khớp tới dấu `}`
    /// **ĐẦU TIÊN ở cuối dòng** — đúng cho mục **phẳng**, nhưng với mục **có con** thì dấu `}` đầu tiên là
    /// của **CON** ⇒ thân bị **cắt trước** `Url`/`QuyenCanCo` của cha.
    /// 🔑 Và hậu quả **nguy hiểm**: cha bị đọc thành mục **không có `Url`** ⇒ `continue` ⇒ **phép quét bỏ qua
    /// im lặng** cả nhánh con ⇒ lưới an toàn *"mọi `Url` phải có controller thật"* **ngừng bảo vệ** nhánh đó
    /// mà **vẫn xanh** — đúng loại *"cơ chế trông có mà thực tế không"*.
    ///
    /// 🔒 **Cách làm:** đi theo **độ sâu ngoặc** (`{` tăng, `}` giảm) để tìm dấu `}` **cân bằng** của từng mục.
    /// ⚠️ Cách này **thô** (không hiểu chuỗi ký tự), nhưng **an toàn theo hướng chặt hơn**: đọc sai chỉ làm
    /// luật **dễ ĐỎ**, không làm nó **lỏng đi**. 📌 Và **lưới an toàn** `mucMenu.Count > 0` bên dưới vẫn canh.
    /// </summary>
    private static List<(string Url, string? Quyen)> CacMucMenuKeCaCon()
    {
        var nguon = NguonMenu();
        var dong = nguon.Split('\n');

        var khuonUrl = new Regex(@"Url\s*=\s*""(?<url>[^""]+)""", RegexOptions.Compiled);
        var khuonQuyen = new Regex(
            @"QuyenCanCo\s*=\s*(?<quyen>eCommerceConsts\.[A-Za-z0-9_.]+|null)",
            RegexOptions.Compiled);

        var ketQua = new List<(string Url, string? Quyen)>();

        for (var i = 0; i < dong.Length; i++)
        {
            // Mục bắt đầu bằng `new()` (có thể xuống dòng trước `{`).
            if (dong[i].Trim() != "new()")
            {
                continue;
            }

            var j = i;
            while (j < dong.Length && dong[j].Trim() != "{")
            {
                j++;
            }

            if (j >= dong.Length)
            {
                continue;
            }

            var sau = 0;
            var than = new List<string>();

            for (; j < dong.Length; j++)
            {
                var d = dong[j];

                foreach (var ch in d)
                {
                    if (ch == '{')
                    {
                        sau++;
                    }
                    else if (ch == '}')
                    {
                        sau--;

                        if (sau == 0)
                        {
                            break;
                        }
                    }
                }

                than.Add(d);

                if (sau == 0)
                {
                    break;
                }
            }

            var noiDung = string.Join("\n", than);
            var url = khuonUrl.Match(noiDung);

            if (!url.Success)
            {
                continue;
            }

            var quyen = khuonQuyen.Match(noiDung);

            ketQua.Add((url.Groups["url"].Value, quyen.Success ? quyen.Groups["quyen"].Value : null));
        }

        return ketQua;
    }

    /// <summary>Các mục **mà người dùng thực sự bấm được** (`Url` khác `#`) — mục cha dùng `#` nên bị loại.</summary>
    private static List<(string Url, string? Quyen)> CacMucBamDuoc()
        => CacMucMenuKeCaCon().Where(m => m.Url != "#").ToList();

    // ── (1) 🚨 MỌI Url PHẢI CÓ CONTROLLER THẬT ─────────────────────────────────────────────────

    [Fact]
    public void MOI_Url_trong_menu_PHAI_co_controller_THAT()
    {
        var thuMucController = Path.Combine(GocRepo(), "src", "eCommerce.Web", "Controllers");

        var tenController = Directory
            .EnumerateFiles(thuMucController, "*Controller.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(t => t is not null)
            // 🚨 **BẪY ĐÃ VẤP (test ĐỎ: "cần `DonHangController`" dù tệp CÓ):** phiên bản đầu viết
            // `t!["..".Length..]` để "bỏ hậu tố `Controller`" — nhưng `".."` chỉ dài **2** ký tự (tôi
            // tưởng là 10) ⇒ tên thành `DonHangC` ⇒ **không khớp gì cả**.
            // 🔒 Nay dùng `[..^"Controller".Length]` — **độ dài lấy từ chính chuỗi**, không viết số ma thuật.
            .Select(t => t![..^"Controller".Length])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mucMenu = CacMucMenuKeCaCon();

        // 🛡️ Lưới an toàn cho CHÍNH bộ quét: nếu regex hỏng thì `mucMenu` rỗng và test sẽ **xanh vô nghĩa**
        // (đúng loại "cơ chế bị vô hiệu hoá im lặng"). Ép phải có ít nhất 1 mục.
        Assert.True(mucMenu.Count > 0,
            "🚨 Bộ quét nguồn KHÔNG tìm thấy mục menu nào ⇒ regex đã hỏng, test này sẽ XANH VÔ NGHĨA. " +
            "Kiểm lại hình dạng khai trong `MenuDieuHuong.cs`.");

        var thieu = new List<string>();

        foreach (var (url, _) in mucMenu)
        {
            // 🚨 **BỎ QUA MỤC CHA** (`Url = "#"`): mục cha **không** trỏ tới trang — bấm vào chỉ **mở nhóm**.
            // 📌 Không bỏ qua thì test đòi một `#Controller` **không tồn tại** ⇒ test ĐỎ **sai** (báo lỗi
            // ở chỗ không có lỗi) ⇒ rồi người ta thêm ngoại lệ cho tới khi test **vô nghĩa**.
            // 🔑 Quy tắc thật vẫn được giữ: mọi **CON** (`Url` thật) **vẫn** phải có controller — và bộ quét
            // `CacMucMenuKeCaCon` **có** duyệt vào nhánh con nên bất biến đó không bị nới.
            if (url == "#")
            {
                continue;
            }

            var doan = url.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (doan.Length == 0)
            {
                continue;   // "/" = trang chủ, do `HomeController` phục vụ
            }

            if (!tenController.Contains(doan[0]))
            {
                thieu.Add($"{url} (cần `{doan[0]}Controller`)");
            }
        }

        Assert.True(thieu.Count == 0,
            "🚨 MENU TRỎ TỚI TRANG KHÔNG TỒN TẠI ⇒ người dùng nhận 404 và tưởng 'tính năng đã có':\n  "
            + string.Join("\n  ", thieu)
            + "\n⇒ Làm TRANG trước (controller + view), RỒI mới thêm mục menu.");
    }

    // ── (2) 🚨 MỌI QUYỀN KHAI PHẢI LÀ QUYỀN CÓ THẬT ────────────────────────────────────────────

    [Fact]
    public void MOI_quyen_khai_trong_menu_PHAI_la_quyen_THAT_da_dinh_nghia()
    {
        // 🔒 Neo vào **nguồn thật** — KHÔNG chép lại danh sách quyền vào test: chép lại thì test
        // **tự lặp giá trị nó đang kiểm** và sẽ **luôn xanh** dù hằng số đổi tên.
        //
        // 🚨 **BẪY ĐÃ VẤP (test ĐỎ ở lần chạy đầu):** phiên bản đầu **chỉ** quét `eCommerceConsts.cs`
        // và **chỉ** so **phần tử CUỐI** của tên (`Default`). Sai ở **cả hai** điểm:
        //   • quyền của `Pricing` **không** nằm ở `eCommerceConsts` — nó ở
        //     `eCommerce.Pricing.PricingPermissions` (module sở hữu hằng số của mình — quy ước #2);
        //   • `Default` xuất hiện ở **N** class khác nhau ⇒ so theo tên **trần** là **vô nghĩa**
        //     (nó khớp `Products.Default` dù menu khai `Orders.Default`).
        // 🔒 Cách đúng: quét **MỌI** tệp `*Permissions.cs` **và** `eCommerceConsts.cs`, dựng bảng
        // theo **TÊN CLASS + TÊN CONST** (`PricingPermissions.Default`), rồi đối chiếu **hai** phần tử cuối
        // của biểu thức menu (`PricingPermissions.Default`).
        var giaTri = new Dictionary<string, string>(StringComparer.Ordinal);
        var khuonClass = new Regex(@"class\s+(?<ten>\w+)\s*\{", RegexOptions.Compiled);
        var khuonConst = new Regex(
            @"public\s+const\s+string\s+(?<ten>\w+)\s*=",
            RegexOptions.Compiled);

        var cacTep = new List<string> { "src/eCommerce.Domain.Shared/eCommerceConsts.cs" };

        // Mọi tệp khai quyền của module (tên tệp kết thúc bằng `Permissions.cs`).
        var goc = GocRepo();
        cacTep.AddRange(Directory
            .EnumerateFiles(Path.Combine(goc, "modules"), "*Permissions.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(goc, f).Replace(Path.DirectorySeparatorChar, '/')));

        foreach (var tep in cacTep)
        {
            var nguon = Doc(tep);

            // 📌 Đơn giản hoá CÓ CHỦ Ý: lấy **class GẦN NHẤT phía trước** mỗi `const`.
            // Đúng cho hình dạng hiện tại (`XPermissions { public const string Default = ... }`) và cho
            // `eCommerceConsts` (nhiều class lồng — chỉ lấy tên class **trong cùng**, đủ để phân biệt
            // `Default` của các miền khác nhau). ⚠️ Nếu tệp sau này lồng class 2 cấp thì **phải** nâng bộ quét.
            var moc = new List<(int ViTri, string Ten)>();

            foreach (Match m in khuonClass.Matches(nguon))
            {
                moc.Add((m.Index, m.Groups["ten"].Value));
            }

            foreach (Match m in khuonConst.Matches(nguon))
            {
                var classGanNhat = moc.Where(x => x.ViTri < m.Index).OrderByDescending(x => x.ViTri).FirstOrDefault();

                if (classGanNhat.Ten is not null)
                {
                    giaTri[$"{classGanNhat.Ten}.{m.Groups["ten"].Value}"] = tep;
                }
            }
        }

        Assert.True(giaTri.Count > 0, "Không đọc được hằng số quyền nào ⇒ bộ quét hỏng.");

        var quyenCuaMenu = CacMucMenuKeCaCon()
            .Where(x => x.Quyen is not null && x.Quyen != "null")
            .Select(x => (x.Url, Ten: x.Quyen!))
            .ToList();

        Assert.True(quyenCuaMenu.Count > 0, "🚨 Không mục menu nào khai quyền ⇒ bộ quét hỏng (hoặc menu mất hết quyền).");

        var sai = new List<string>();

        foreach (var (url, ten) in quyenCuaMenu)
        {
            // `eCommerceConsts.Permissions.Orders.Default` ⇒ hai phần tử cuối = `Orders.Default`
            // `eCommerce.Pricing.PricingPermissions.Default` ⇒ hai phần tử cuối = `PricingPermissions.Default`
            var phan = ten.Split('.');
            var khoa = phan.Length >= 2 ? $"{phan[^2]}.{phan[^1]}" : ten;

            if (!giaTri.ContainsKey(khoa))
            {
                sai.Add($"{url} ⇒ `{ten}` (không tìm thấy `{khoa}` trong bất kỳ `*Permissions.cs` nào)");
            }
        }

        Assert.True(sai.Count == 0,
            "🚨 MENU KHAI QUYỀN KHÔNG TỒN TẠI ⇒ mục VĨNH VIỄN ẨN với mọi người (kể cả admin), KHÔNG lỗi nào:\n  "
            + string.Join("\n  ", sai));
    }

    // ── (3) LÕI LỌC PHẢI THUẦN + FAIL-CLOSED ───────────────────────────────────────────────────

    [Fact]
    public void Bo_loc_la_HAM_THUAN__khong_phu_thuoc_ABP_hay_Razor()
    {
        var nguon = NguonBoLoc();

        // 🔒 **VÌ SAO BẤT BIẾN NÀY QUAN TRỌNG:** lõi lọc phải test được **mọi nhánh** không cần HTTP/DB.
        // Nếu ai đó `using Volo.Abp...`/`Microsoft.AspNetCore...` vào lõi (hoặc `[Inject]`), thì:
        //   • unit test hiện có **vẫn xanh** (vì nó chỉ grep chuỗi) NHƯNG lõi **không còn** kiểm được độc lập;
        //   • và người sau sẽ **thêm** phụ thuộc nữa cho tới khi lõi chỉ chạy được trong host Razor.
        var cam = new[] { "using Volo.Abp", "using Microsoft.AspNetCore", "[Inject]", "IAuthorizationService" };

        var viPham = cam.Where(c => nguon.Contains(c, StringComparison.Ordinal)).ToList();

        Assert.True(viPham.Count == 0,
            "🚨 LÕI LỌC MENU ĐÃ MẤT TÍNH THUẦN — nó phải test được không cần ABP/Razor:\n  "
            + string.Join("\n  ", viPham));
    }

    [Fact]
    public void Bo_loc_FAIL_CLOSED__bat_exception_va_AN_muc()
    {
        var nguon = NguonBoLoc();

        // 🔒 Fail-closed là **quyết định an toàn**, nên nó phải hiện diện **trong mã**: có `try`/`catch`
        // quanh lời gọi kiểm quyền, và nhánh `catch` đặt **false** (ẩn) — KHÔNG phải `true`.
        Assert.Contains("try", nguon, StringComparison.Ordinal);
        Assert.Contains("catch", nguon, StringComparison.Ordinal);

        // Nhánh catch phải gán `false`; nếu ai đổi thành `true` (fail-open) thì test này đỏ.
        var khuonCatch = new Regex(@"catch\s*\{(?<than>.*?)\}", RegexOptions.Singleline);
        var thanCatch = khuonCatch.Match(nguon);

        Assert.True(thanCatch.Success, "Không đọc được thân khối `catch` của bộ lọc.");
        Assert.Contains("coQuyen = false", thanCatch.Groups["than"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_phai_doc_tu_MOT_nguon__khong_rai_trong_view()
    {
        // 🔒 Nếu ai đó khai mục menu **thẳng trong `.cshtml`** thì *"màn hình nào tồn tại"* có hai nguồn,
        // và mục khai trong view **thoát** mọi phép kiểm ở trên (không có `Url`/`QuyenCanCo` để quét) —
        // tức nó **hiện cho mọi người** bất kể quyền.
        //
        // 🚨 **HAI LẦN VẤP Ở PHÉP KIỂM NÀY — ghi lại vì cả hai đều là bài học:**
        //   • **Lần 1 (dương tính giả):** mẫu `<li[^>]*>\s*<a[^>]+href=""/...` khớp cả **Razor expression**
        //     ⇒ `_Menu.cshtml` bị báo oan (nó có `href="@muc.Url"`). Sửa: đòi ký tự chữ ngay sau `/`.
        //   • **Lần 2 (vẫn dương tính giả):** `Index.cshtml` **có thật** hai link cứng —
        //     `<li><a href="/Account/Manage">Quản lý tài khoản</a></li>` và `<li><a href="/swagger">…` —
        //     nhưng đó **KHÔNG** phải mục menu (chúng là link phụ trợ trong thẻ nội dung), và
        //     **đúng là** không nên bị cấm.
        // 🔒 **Cách đúng (hẹp và tất định):** chỉ cấm **URL của chính menu** (`MenuDieuHuong.TatCa`)
        // xuất hiện dưới dạng **chuỗi cứng** trong view. Đây là rủi ro **thật**: một mục menu viết cứng
        // trong view sẽ **hiện cho mọi người** vì nó không đi qua `BoLocDieuHuong`.
        // 📌 Không đoán "cái gì là menu" — chỉ kiểm *"URL menu có bị viết cứng không"*.
        var thuMucViews = Path.Combine(GocRepo(), "src", "eCommerce.Web", "Views");

        var urlCuaMenu = CacMucMenuKeCaCon()
            .Select(x => x.Url)
            .Where(u => u.Length > 1)                  // bỏ "/" (trang chủ)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(urlCuaMenu.Count > 0, "🚨 Bộ quét menu không đọc được URL nào ⇒ test này sẽ xanh vô nghĩa.");

        var viPham = new List<string>();

        foreach (var tep in Directory.EnumerateFiles(thuMucViews, "*.cshtml", SearchOption.AllDirectories))
        {
            if (tep.EndsWith("_Menu.cshtml", StringComparison.OrdinalIgnoreCase))
            {
                continue;   // đây CHÍNH LÀ nơi render menu
            }

            var noiDung = File.ReadAllText(tep);

            foreach (var url in urlCuaMenu)
            {
                // Chuỗi cứng: `href="/DonHang"` (KHÔNG phải `href="@muc.Url"`).
                if (noiDung.Contains($"href=\"{url}\"", StringComparison.OrdinalIgnoreCase))
                {
                    viPham.Add($"{Path.GetFileName(tep)} ⇒ href=\"{url}\"");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 URL CỦA MENU BỊ VIẾT CỨNG TRONG VIEW (mục đó HIỆN CHO MỌI NGƯỜI, bỏ qua lọc quyền):\n  "
            + string.Join("\n  ", viPham)
            + "\n⇒ Dùng `<partial name=\"_Menu\" />` để menu đi qua `BoLocDieuHuong`.");
    }
}
