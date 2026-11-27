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

    // ── TC-ARCH-02: tầng kênh gửi + job không tự mở kết nối mạng ───────────────────────────────

    [Fact]
    public void TC_ARCH_02_NotificationSender_va_job_khong_dung_HttpClient_SmtpClient_System_Net_Mail()
    {
        // BR-001: CẤM app service/job gọi SDK/HTTP nhà cung cấp trực tiếp. Quét CẢ tầng kênh gửi
        // VÀ job bàn giao — không chỉ một tệp (nếu job tự gọi mạng, sender trung lập bị vòng qua).
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

        var cam = new[] { "HttpClient", "SmtpClient", "System.Net.Mail", "System.Net.Http" };
        var viPham = new List<string>();

        foreach (var tep in files)
        {
            var noiDung = File.ReadAllText(tep);
            viPham.AddRange(cam
                .Where(c => noiDung.Contains(c, StringComparison.Ordinal))
                .Select(c => $"{Path.GetFileName(tep)}: {c}"));
        }

        Assert.True(viPham.Count == 0,
            "Tầng kênh gửi / job bàn giao tham chiếu SDK/HTTP nhà cung cấp trực tiếp (BR-001):\n  "
            + string.Join("\n  ", viPham));
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

    // ── TC-ARCH-05 (BR-011, phần đã hiện thực): hợp đồng bắt buộc `CancellationToken` ──────────

    [Fact]
    public void TC_ARCH_05_hop_dong_adapter_bat_buoc_CancellationToken_cho_timeout()
    {
        // BR-011 yêu cầu MỌI lời gọi nhà cung cấp phải huỷ được theo thời hạn. Phần đã hiện thực ở WP-23 là
        // hợp đồng: adapter kênh chỉ có ĐÚNG một `SendAsync(...)` với tham số cuối `CancellationToken`.
        // Bỏ tham số này ⇒ không thể áp timeout hữu hạn ⇒ job gọi không huỷ được (test PHẢI đỏ).
        var lenhKenh = typeof(INotificationChannelSender)
            .GetMethods()
            .Where(m => m.Name == "SendAsync")
            .ToList();

        var guiKenh = Assert.Single(lenhKenh);
        Assert.Equal(typeof(Task<SendOutcome>), guiKenh.ReturnType);

        var thamSoKenh = guiKenh.GetParameters();
        Assert.Equal(2, thamSoKenh.Length);
        Assert.Equal(typeof(SendCommand), thamSoKenh[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), thamSoKenh[1].ParameterType);
        Assert.True(thamSoKenh[1].HasDefaultValue, "`CancellationToken` phải có mặc định `default`.");

        // Hợp đồng nội module cũng phải nhận `CancellationToken` để job truyền huỷ xuống adapter.
        var lenhNoiModule = typeof(INotificationSender)
            .GetMethods()
            .Where(m => m.Name == "SendAsync")
            .ToList();

        var guiNoiModule = Assert.Single(lenhNoiModule);
        Assert.Equal(typeof(CancellationToken), guiNoiModule.GetParameters()[^1].ParameterType);
    }
}
