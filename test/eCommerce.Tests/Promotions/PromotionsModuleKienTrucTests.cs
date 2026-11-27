using System.Text.RegularExpressions;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// 🚨 **KHOÁ CẤU TRÚC NẠP MODULE Ở HAI TIẾN TRÌNH** (`WP-24`/`W24-5`, `D-293` ③ + `D-294` ④).
///
/// ═════════════════════════════════════════════════════════════════════════════════════════════════════
/// 🔥 **LỖI THẬT ĐÃ ĐO (N98, G6 `-DayDu` đỏ):** `CheckoutAppService` (module `Sales`) tiêm `IPromotionEngine`
/// từ `0ebc110` (`W24-2`), NHƯNG `src/eCommerce.Web` **chỉ** thêm `ProjectReference` và **quên** nạp
/// `PromotionsApplicationModule` vào `[DependsOn]` ⇒ `GET /DatHang` trả **`HTTP 500`**
/// (*"Cannot resolve parameter 'eCommerce.Promotions.Contracts.IPromotionEngine'"*); **build xanh** và
/// **2461 unit test xanh** — E2E mới bắt được. Vá xong thêm `Replace(IAbpDistributedLock → LocalAbpDistributedLock)`
/// (`D-294`) vì nạp `PromotionsApplicationModule` kéo `AbpDistributedLockingModule` (Medallion cần Redis) ⇒ Web
/// **không boot**.
///
/// 🔑 **VÌ SAO TEST NÀY QUÉT MÃ NGUỒN (không dựng DI/boot):** `[DependsOn]` chỉ là **dữ liệu của module khởi động**;
/// muốn kiểm bằng reflection phải dựng cả Host/Web (cần config + DB, dễ flaky — `D-294` ④ cấm test boot riêng).
/// Khuôn `Catalog/CatalogMediaModuleTests.cs` (`CAT-003` — đã trả giá 2 lần cho **đúng** họ lỗi này).
/// 📌 Boot thật được đo bằng G6 `-DayDu` (`GET /DatHang` 200, E2E 150/150); test này là **lưới chặn tái diễn**.
///
/// 🔒 **Quét sau khi BỎ CHÚ THÍCH `//` và `/* */`** (`D-294` ③(b)): chú thích ở `eCommerceWebModule.cs` có nhắc
/// `CatalogMediaModule`/`LocalAbpDistributedLock` ⇒ nếu không bỏ, gỡ dòng mã thật vẫn "xanh giả".
/// </summary>
public class PromotionsModuleKienTrucTests
{
    private const string TepHost = "src/eCommerce.HttpApi.Host/eCommerceHttpApiHostModule.cs";

    private const string TepWeb = "src/eCommerce.Web/eCommerceWebModule.cs";

    private const string TepWebCsproj = "src/eCommerce.Web/eCommerce.Web.csproj";

    // ── (1) 🚨 6 MODULE PHẢI NẰM TRONG `[DependsOn]` CỦA **CẢ** HOST **VÀ** WEB (`D-293` ③(a)) ──────

    [Theory]
    // 📌 Host phục vụ `/api/v1/**`; Web chạy **in-process** cho trang mặt tiền. Mỗi tiến trình có đồ thị module
    // RIÊNG ⇒ thiếu ở **một** nơi thì chức năng hỏng **chỉ ở tiến trình đó** (khó tìm hơn hỏng cả hai).
    // 🔑 2 dòng đầu là module Web mà `/DatHang`/storefront cần ngay lúc này (`D-293`/`D-294`); 4 dòng còn lại là
    // các module Web **đang** nạp (giữ xanh — chứng minh phép quét không chỉ bắt một chỗ).
    [InlineData("PromotionsApplicationModule", TepHost)]
    [InlineData("PromotionsApplicationModule", TepWeb)]
    [InlineData("CatalogMediaModule", TepHost)]
    [InlineData("CatalogMediaModule", TepWeb)]
    [InlineData("PricingApplicationModule", TepHost)]
    [InlineData("PricingApplicationModule", TepWeb)]
    [InlineData("CustomersApplicationModule", TepHost)]
    [InlineData("CustomersApplicationModule", TepWeb)]
    [InlineData("PaymentHubApplicationModule", TepHost)]
    [InlineData("PaymentHubApplicationModule", TepWeb)]
    [InlineData("AddressApplicationModule", TepHost)]
    [InlineData("AddressApplicationModule", TepWeb)]
    public void Module_PHAI_nam_trong_DependsOn_cua_CA_Host_VA_Web(string tenModule, string duongDanTuongDoi)
    {
        // 🚨 BÀI HỌC `CAT-003`/`Pricing`/`Customers`/`WEB-001` (đã trả giá nhiều lần): **chỉ thêm
        // `ProjectReference` là KHÔNG ĐỦ** — thiếu `typeof(...)` trong `[DependsOn]` thì assembly/module KHÔNG nằm
        // trong đồ thị ⇒ ABP không quét, không đăng ký ⇒ `HTTP 500` *"Cannot resolve parameter"* **dù build xanh**.
        var noiDung = DocVaBoChuThich(duongDanTuongDoi);

        Assert.True(noiDung.Contains(tenModule, StringComparison.Ordinal),
            $"🚨 `{duongDanTuongDoi}` KHÔNG có `typeof(…{tenModule})` trong `[DependsOn]`.\n"
            + "⇒ Module KHÔNG nằm trong đồ thị của tiến trình đó ⇒ DI không dựng được controller/app service "
            + "dùng nó ⇒ **`HTTP 500`** (*\"Cannot resolve parameter\"*) — và **build + unit test vẫn XANH**.\n"
            + "⇒ Nạp đúng quy tắc: Web/Host dùng app service của module nào ⇒ thêm `ProjectReference` **VÀ** "
            + "`typeof(<Module>)` vào `[DependsOn]` của **cả hai** tiến trình.");
    }

    // ── (2) 🚨 QUÉT GENERIC: REFERENCE MỚI MÀ QUÊN `DependsOn` ⇒ ĐỎ (`D-293` ③(b)) ──────────────────

    [Fact]
    public void MOI_ProjectReference_toi_modules_Application__PHAI_co_typeof_trong_DependsOn_cua_Web()
    {
        // 🔑 Không chỉ khoá 6 tên ở trên: nếu sau này ai thêm `<ProjectReference>` tới một module Application mới
        // mà quên `typeof(...)` trong `[DependsOn]` của Web, test này bắt **ngay** (đúng họ lỗi `D-293`).
        var duongDanCsproj = Path.Combine(TimGocRepo(), TepWebCsproj.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(duongDanCsproj), $"Không tìm thấy `{TepWebCsproj}` — đường dẫn test đã lệch.");

        var noiDungCsproj = File.ReadAllText(duongDanCsproj);

        // Mọi `ProjectReference` trỏ `modules/<Nhóm>/eCommerce.<X>.Application/eCommerce.<X>.Application.csproj`.
        var cacProjectApplication = Regex
            .Matches(noiDungCsproj, "<ProjectReference\\s+Include=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .Where(p => Regex.IsMatch(
                p,
                @"(^|/)modules/[^/]+/eCommerce\.[^/]+\.Application/eCommerce\.[^/]+\.Application\.csproj$",
                RegexOptions.IgnoreCase))
            .ToList();

        // 🛡️ KHOÁ SÀN — phép quét rỗng thì test dưới XANH VÔ NGHĨA (`D-293` ⑥(iii), khuôn `Phep_quet_KHONG_rong`).
        // 📌 Hôm nay có **5** ref module Application (Pricing/Customers/Address/PaymentHub/Promotions); `CatalogMediaModule`
        // nằm ở `modules/Catalog/eCommerce.Catalog.Domain` nên đi **bắc cầu** qua `eCommerce.Application` — vẫn
        // được khoá bởi 12 ca theory ở trên cùng cặp `(module, tệp)`. Tổng số cặp kiểm = 12 + 5 ≥ 6.
        const int sanSoCapToiThieu = 5;
        Assert.True(cacProjectApplication.Count >= sanSoCapToiThieu,
            $"🚨 Phép quét chỉ thấy {cacProjectApplication.Count} `ProjectReference` tới `modules/**/*.Application` "
            + $"(sàn {sanSoCapToiThieu}). Regex/đường dẫn đã lệch ⇒ test generic XANH VÔ NGHĨA. "
            + "Sửa phép quét TRƯỚC khi tin kết quả.");

        var noiDungWeb = DocVaBoChuThich(TepWeb);

        var thieu = new List<string>();

        foreach (var duongDanProject in cacProjectApplication)
        {
            var tenModule = TimTenModuleCuaProject(duongDanProject);

            Assert.False(string.IsNullOrWhiteSpace(tenModule),
                $"Không đọc được tên class `AbpModule` của `{duongDanProject}` ⇒ phép quét không kết luận được.");

            // 🚨 ĐÂY LÀ CẶP PHẢI KHỚP: có reference ⇒ phải có `typeof(<Module>)` trong `[DependsOn]` của Web.
            if (!noiDungWeb.Contains(tenModule, StringComparison.Ordinal))
            {
                thieu.Add($"`{duongDanProject}` ⇒ thiếu `typeof(…{tenModule})` trong `[DependsOn]` của `{TepWeb}`");
            }
        }

        Assert.True(thieu.Count == 0,
            "🚨 THÊM `ProjectReference` mà QUÊN `DependsOn` ở `eCommerce.Web` ⇒ DI không đăng ký app service của "
            + "module ⇒ `HTTP 500` lúc chạy (build + unit test vẫn xanh):\n  - " + string.Join("\n  - ", thieu));
    }

    // ── (3) 🚨 WEB PHẢI `Replace` KHOÁ PHÂN TÁN VỀ `LocalAbpDistributedLock` (`D-294` ③(a)) ─────────

    [Fact]
    public void Web_PHAI_Replace_IAbpDistributedLock_bang_LocalAbpDistributedLock__D294()
    {
        // 🚨 `PromotionsApplicationModule` kéo `AbpDistributedLockingModule` ⇒ ABP thay `IAbpDistributedLock` bằng
        // `MedallionAbpDistributedLock`, mà loại này cần `IDistributedLockProvider` (Redis). Web **không** Redis
        // (doc comment `:40-41`) ⇒ **KHÔNG BOOT** (`AbpInitializationException … Cannot resolve parameter
        // 'IDistributedLockProvider'`). Đã đo N98: thêm `Replace(... LocalAbpDistributedLock)` ⇒ boot + `/DatHang` 200.
        // 🔒 Quét mã **sau khi bỏ chú thích** (chú thích có nhắc `LocalAbpDistributedLock` ⇒ nếu không bỏ, gỡ dòng
        // mã thật vẫn xanh giả — `D-294` ③(b)).
        var noiDung = DocVaBoChuThich(TepWeb);

        Assert.True(
            Regex.IsMatch(noiDung, @"Replace\s*\(\s*ServiceDescriptor\.Singleton<IAbpDistributedLock\s*,\s*LocalAbpDistributedLock\s*>"),
            $"🚨 `{TepWeb}` THIẾU `Replace(ServiceDescriptor.Singleton<IAbpDistributedLock, LocalAbpDistributedLock>())`.\n"
            + "⇒ Nạp `PromotionsApplicationModule` kéo `AbpDistributedLockingModule` ⇒ `MedallionAbpDistributedLock` "
            + "cần `IDistributedLockProvider` (Redis) ⇒ **Web KHÔNG khởi động nổi** ⇒ storefront **mất đặt hàng**.\n"
            + "⇒ Web không chạy worker (đã tắt) ⇒ dùng `LocalAbpDistributedLock` có sẵn của ABP: **0** Redis, **0** package.");
    }

    // ── (4) 🚨 WEB VẪN PHẢI TẮT WORKER/JOB (`D-293` ③(c), `web-001`) ──────────────────────────────

    [Fact]
    public void Web_VAN_tat_worker_va_job__vi_nap_Promotions_keo_promotion_expiry_chay_kep()
    {
        // 🚨 Nạp `PromotionsApplicationModule` ở Web kéo theo `PromotionExpiryWorker` (`promotion-expiry`). Web và
        // Host chạy **cùng DB**: nếu Web không tắt worker/job thì hết hạn khuyến mãi chạy **2 lần**/tiến trình
        // (tranh chấp, ghi kép, tiêu quota) — `web-001`. Hai dòng tắt này là **cấu hình**, không phải logic ⇒
        // test thường không thấy; khoá lại để không ai gỡ nhầm khi "dọn" `eCommerceWebModule`.
        var noiDung = DocVaBoChuThich(TepWeb);

        Assert.True(
            Regex.IsMatch(noiDung, @"Configure<AbpBackgroundWorkerOptions>\s*\(\s*\w+\s*=>\s*\{[^}]*IsEnabled\s*=\s*false", RegexOptions.Singleline),
            $"🚨 `{TepWeb}` THIẾU `Configure<AbpBackgroundWorkerOptions>(o => o.IsEnabled = false)`.\n"
            + "⇒ `PromotionExpiryWorker` (và mọi worker khác) chạy **KÉP** ở Host + Web ⇒ tranh chấp, ghi kép, tiêu quota.");

        Assert.True(
            Regex.IsMatch(noiDung, @"Configure<AbpBackgroundJobOptions>\s*\(\s*\w+\s*=>\s*\{[^}]*IsJobExecutionEnabled\s*=\s*false", RegexOptions.Singleline),
            $"🚨 `{TepWeb}` THIẾU `Configure<AbpBackgroundJobOptions>(o => o.IsJobExecutionEnabled = false)`.\n"
            + "⇒ Background job chạy **KÉP** ở Host + Web ⇒ việc nền (gửi hoá đơn, đẩy sàn…) thực thi hai lần.");
    }

    // ── (5) PHÉP QUÉT KHÔNG ĐƯỢC RỖNG (lưới an toàn cho chính test này) ─────────────────────────────

    [Fact]
    public void Phep_quet_KHONG_rong__du_6_module_va_2_tep()
    {
        // 🛡️ Cùng khuôn `CatalogMediaModuleTests.Phep_quet_KHONG_rong`: nếu ai xoá bớt `InlineData` hoặc đổi tên
        // tệp, các test trên vẫn "xanh" mà **không** kiểm gì. Khoá sàn: 6 module × 2 tệp = **12** cặp.
        const int soModulePhaiCo = 6;
        const int soTepPhaiCo = 2;

        var cachKiem = typeof(PromotionsModuleKienTrucTests)
            .GetMethod(nameof(Module_PHAI_nam_trong_DependsOn_cua_CA_Host_VA_Web))!
            .GetCustomAttributes(typeof(Xunit.InlineDataAttribute), inherit: false)
            .Cast<Xunit.InlineDataAttribute>()
            .Select(a =>
            {
                var duLieu = a.GetData(null!).First();
                return (TenModule: (string)duLieu[0]!, Tep: (string)duLieu[1]!);
            })
            .ToList();

        Assert.Equal(soModulePhaiCo * soTepPhaiCo, cachKiem.Count);
        Assert.Equal(soModulePhaiCo, cachKiem.Select(c => c.TenModule).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(soTepPhaiCo, cachKiem.Select(c => c.Tep).Distinct(StringComparer.Ordinal).Count());
        Assert.False(string.IsNullOrWhiteSpace(TimGocRepo()), "Không tìm được gốc repo ⇒ test quét mã nguồn vô nghĩa.");
    }

    // ── HẠ TẦNG ĐỌC TỆP ──────────────────────────────────────────────────────────────────────────

    /// <summary>Đi lên từ thư mục chạy test cho tới khi gặp `eCommerce.slnx` (dấu hiệu gốc repo).</summary>
    private static string TimGocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (File.Exists(Path.Combine(thuMuc.FullName, "eCommerce.slnx")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        return string.Empty;
    }

    private static string DocVaBoChuThich(string duongDanTuongDoi)
    {
        var goc = TimGocRepo();
        Assert.False(string.IsNullOrWhiteSpace(goc), "Không tìm được gốc repo (thiếu `eCommerce.slnx`).");

        var duongDan = Path.Combine(goc, duongDanTuongDoi.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(duongDan), $"Không tìm thấy tệp `{duongDanTuongDoi}` — đường dẫn test đã lệch.");

        return BoChuThichCSharp(File.ReadAllText(duongDan));
    }

    /// <summary>
    /// Bỏ chú thích `/* … */` và `// …` (giữ nguyên chuỗi URL `http://`/`https://`).
    /// 📌 Bắt buộc trước khi quét `Contains`: chú thích của `eCommerceWebModule.cs` nhắc đúng tên module/khoá cần
    /// kiểm ⇒ quét thô sẽ XANH GIẢ khi dòng mã thật bị gỡ (`D-294` ③(b)).
    /// </summary>
    private static string BoChuThichCSharp(string ma)
    {
        var khongKhoi = Regex.Replace(ma, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var dong = khongKhoi.Split('\n');

        for (var i = 0; i < dong.Length; i++)
        {
            var viTri = dong[i].IndexOf("//", StringComparison.Ordinal);

            // `://` là chuỗi URL, không phải chú thích.
            while (viTri >= 0 && viTri > 0 && dong[i][viTri - 1] == ':')
            {
                viTri = dong[i].IndexOf("//", viTri + 2, StringComparison.Ordinal);
            }

            if (viTri >= 0)
            {
                dong[i] = dong[i][..viTri];
            }
        }

        return string.Join("\n", dong);
    }

    /// <summary>
    /// Tên class `AbpModule` của project Application đích: đọc `*Module.cs` cùng thư mục; không thấy thì rơi về
    /// quy ước `eCommerce.&lt;X&gt;.Application` ⇒ `&lt;X&gt;ApplicationModule`.
    /// </summary>
    private static string TimTenModuleCuaProject(string duongDanProjectTuongDoi)
    {
        var goc = TimGocRepo();
        var duongDanProject = Path.Combine(goc, duongDanProjectTuongDoi.Replace('/', Path.DirectorySeparatorChar));
        var thuMuc = Path.GetDirectoryName(duongDanProject);

        if (thuMuc is not null && Directory.Exists(thuMuc))
        {
            foreach (var tep in Directory.EnumerateFiles(thuMuc, "*Module.cs", SearchOption.TopDirectoryOnly))
            {
                var ma = BoChuThichCSharp(File.ReadAllText(tep));
                var khop = Regex.Match(ma, @"class\s+(\w+Module)\s*:\s*AbpModule");

                if (khop.Success)
                {
                    return khop.Groups[1].Value;
                }
            }
        }

        var tenProject = Path.GetFileNameWithoutExtension(duongDanProjectTuongDoi);
        var phan = tenProject.Split('.');

        return phan.Length >= 3 && string.Equals(phan[^1], "Application", StringComparison.Ordinal)
            ? phan[^2] + "ApplicationModule"
            : string.Empty;
    }
}
