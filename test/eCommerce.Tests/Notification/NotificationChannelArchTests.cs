using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using eCommerce.Kernel.Observability;
using eCommerce.Notification;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `NOT-002` (`WP-23`, `W23-2` F4 QA) — **quét kiến trúc** bằng tệp nguồn thật: `TC-ARCH-01…04`
/// (`NOT-002-arch.md` §13.1). Mỗi phép kiểm đều **đi lên gốc repo** (mốc `eCommerce.slnx`) và khẳng định
/// **có ít nhất một tệp để quét** — nếu không tìm thấy thì ném, không "xanh giả" trên tập rỗng.
/// </summary>
public class NotificationChannelArchTests
{
    private static string GocRepo()
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

        throw new DirectoryNotFoundException(
            "KHÔNG TÌM THẤY `eCommerce.slnx` ⇒ phép kiểm kiến trúc VÔ NGHĨA. Từ: " + AppContext.BaseDirectory);
    }

    private static string ThuMucModule(string duongDanTuongDoi)
        => Path.Combine(GocRepo(), "modules", "Notification", duongDanTuongDoi);

    private static string ThuMucChannels()
        => ThuMucModule(Path.Combine("eCommerce.Notification.Application", "Channels"));

    private static readonly string[] GoiNcc =
    [
        "SendGrid", "MailKit", "MimeKit", "Twilio", "Vonage", "Nexmo",
        "ESms", "Zalo", "Mailgun", "Postmark", "Brevo", "Sendinblue", "SmtpClient"
    ];

    // ── TC-ARCH-01: 0 gói NuGet nhà cung cấp ───────────────────────────────────────────────────

    [Fact]
    public void TC_ARCH_01_khong_co_goi_NuGet_nha_cung_cap_trong_solution_va_module()
    {
        var props = Path.Combine(GocRepo(), "Directory.Packages.props");
        Assert.True(File.Exists(props), "Không tìm thấy `Directory.Packages.props` ⇒ phép kiểm VÔ NGHĨA.");

        var noiDungProps = File.ReadAllText(props);
        foreach (var goi in GoiNcc)
        {
            Assert.DoesNotContain(goi, noiDungProps, StringComparison.OrdinalIgnoreCase);
        }

        var csprojs = Directory.GetFiles(ThuMucModule(string.Empty), "*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(csprojs);

        var viPham = new List<string>();
        foreach (var tep in csprojs)
        {
            var noiDung = File.ReadAllText(tep);
            viPham.AddRange(GoiNcc
                .Where(goi => noiDung.Contains(goi, StringComparison.OrdinalIgnoreCase))
                .Select(goi => $"{Path.GetFileName(tep)}: {goi}"));
        }

        Assert.True(viPham.Count == 0,
            "Module Notification tham chiếu SDK/Gói nhà cung cấp (D-200 cấm):\n  " + string.Join("\n  ", viPham));
    }

    // ── TC-ARCH-02: sender không tự mở kết nối mạng ────────────────────────────────────────────

    [Fact]
    public void TC_ARCH_02_NotificationSender_khong_dung_HttpClient_SmtpClient_System_Net_Mail()
    {
        var tep = Path.Combine(ThuMucChannels(), "NotificationSender.cs");
        Assert.True(File.Exists(tep), "Không tìm thấy `NotificationSender.cs` ⇒ phép kiểm VÔ NGHĨA.");

        var noiDung = File.ReadAllText(tep);

        foreach (var cam in new[] { "HttpClient", "SmtpClient", "System.Net.Mail", "System.Net.Http" })
        {
            Assert.DoesNotContain(cam, noiDung, StringComparison.Ordinal);
        }
    }

    // ── TC-ARCH-03: không gọi app service của module khác ──────────────────────────────────────

    [Fact]
    public void TC_ARCH_03_Channels_khong_tham_chieu_IAppService_module_khac()
    {
        var files = Directory.GetFiles(ThuMucChannels(), "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var mauAppService = new Regex(@"\bI[A-Z]\w*AppService\b", RegexOptions.CultureInvariant);
        var viPham = new List<string>();

        foreach (var tep in files)
        {
            foreach (var dong in File.ReadLines(tep))
            {
                var cau = dong.Trim();

                if (cau.StartsWith("using eCommerce.", StringComparison.Ordinal)
                    && !cau.StartsWith("using eCommerce.Notification", StringComparison.Ordinal)
                    && !cau.StartsWith("using eCommerce.Kernel.", StringComparison.Ordinal))
                {
                    viPham.Add($"{Path.GetFileName(tep)}: {cau}");
                }

                if (mauAppService.IsMatch(cau))
                {
                    viPham.Add($"{Path.GetFileName(tep)}: {cau}");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "Tầng kênh gửi tham chiếu app service module khác / `using` ngoài module (BR-016):\n  "
            + string.Join("\n  ", viPham));
    }

    // ── TC-ARCH-04: 0 endpoint mới + 0 metric mới ngoài registry ───────────────────────────────

    [Fact]
    public void TC_ARCH_04_khong_endpoint_AllowAnonymous_va_metric_dung_ngoai_registry()
    {
        // (a) Không endpoint/`[AllowAnonymous]` mới trong tầng kênh gửi + job bàn giao.
        var thuMucQuet = new[]
        {
            ThuMucChannels(),
            ThuMucModule(Path.Combine("eCommerce.Notification.Application", "Dispatch"))
        };

        var files = thuMucQuet
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories))
            .ToList();
        Assert.NotEmpty(files);

        string[] dauHieuEndpoint =
        [
            "[AllowAnonymous]", "[HttpGet", "[HttpPost", "[HttpPut", "[HttpDelete",
            "[Route(", "ControllerBase", "IApplicationService"
        ];

        var viPham = new List<string>();
        foreach (var tep in files)
        {
            var noiDung = File.ReadAllText(tep);
            viPham.AddRange(dauHieuEndpoint
                .Where(dh => noiDung.Contains(dh, StringComparison.Ordinal))
                .Select(dh => $"{Path.GetFileName(tep)}: {dh}"));
        }

        Assert.True(viPham.Count == 0,
            "WP-23 không được mở endpoint/quyền mới (D-202):\n  " + string.Join("\n  ", viPham));

        // (b) Metric dùng trong `NotificationSender` phải nằm trong BẢNG ĐĂNG KÝ `eCommerceMetrics`.
        var sender = File.ReadAllText(Path.Combine(ThuMucChannels(), "NotificationSender.cs"));
        var dung = Regex.Matches(sender, @"_metrics\.(?<ten>[A-Za-z_][A-Za-z0-9_]*)\.Add")
            .Select(m => m.Groups["ten"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(dung); // non-vacuous: hiện có `NotificationQueueFailed`
        Assert.Contains("NotificationQueueFailed", dung);

        var hopLe = typeof(eCommerceMetrics)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(Counter<long>))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(dung, ten => Assert.Contains(ten, hopLe));

        // (c) Module không tạo `Meter`/counter riêng (mọi metric phải qua registry chung — ADR-07).
        var applicationDir = ThuMucModule("eCommerce.Notification.Application");
        var filesApplication = Directory.GetFiles(applicationDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(filesApplication);
        Assert.All(filesApplication, tep =>
            Assert.DoesNotContain("new Meter(", File.ReadAllText(tep), StringComparison.Ordinal));
    }
}
