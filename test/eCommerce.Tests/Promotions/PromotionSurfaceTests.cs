using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.Promotions.Contracts;
using Microsoft.EntityFrameworkCore;

namespace eCommerce.Promotions.Tests;

public class PromotionSurfaceTests
{
    private static string Goc()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "eCommerce.slnx")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("Không tìm thấy gốc repo (eCommerce.slnx).");
    }

    private static IEnumerable<string> DocNguon(string thuMuc)
    {
        var goc = Path.Combine(Goc(), thuMuc);
        if (!Directory.Exists(goc))
        {
            return [];
        }

        return Directory.EnumerateFiles(goc, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText);
    }

    [Fact]
    public void Allowlist_IPromotionAppService_dung_10_method__khong_UpdateAsync()
    {
        var ten = typeof(IPromotionAppService).GetMethods()
            .Select(m => m.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        string[] mongDoi =
        [
            "ApproveAsync", "CreateAsync", "EvaluateAsync", "GetAsync", "GetListAsync",
            "GetRedemptionsAsync", "ImportRuleCostsAsync", "SaveTreeAsync", "SetStatusAsync", "ValidateVoucherAsync"
        ];

        Assert.Equal(mongDoi.OrderBy(x => x, StringComparer.Ordinal), ten);
        Assert.DoesNotContain("UpdateAsync", ten);
    }

    [Fact]
    public void AppService_co_Authorize_cap_CLASS_va_method_hep()
    {
        var t = typeof(PromotionAppService);

        var lop = t.GetCustomAttributes(inherit: true)
            .Where(a => a.GetType().Name == "AuthorizeAttribute")
            .ToList();
        Assert.Single(lop);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Default, Policy(lop[0]));

        string[] hep =
        [
            eCommerceConsts.Permissions.Promotions.View,
            eCommerceConsts.Permissions.Promotions.Manage,
            eCommerceConsts.Permissions.Promotions.Approve,
            eCommerceConsts.Permissions.Promotions.Import
        ];

        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal)))
        {
            var attr = m.GetCustomAttributes(inherit: true)
                .Where(a => a.GetType().Name == "AuthorizeAttribute")
                .ToList();
            Assert.Single(attr);
            Assert.Contains(Policy(attr[0]), hep);
        }
    }

    private static string? Policy(object attr)
        => attr.GetType().GetProperty("Policy")?.GetValue(attr) as string;

    [Fact]
    public void Khong_co_AllowAnonymous_trong_Promotions()
    {
        var viPham = new[] { "modules/Promotions/eCommerce.Promotions.Application", "modules/Promotions/eCommerce.Promotions.Domain" }
            .SelectMany(DocNguon)
            .Count(x => Regex.IsMatch(x, @"\[\s*AllowAnonymous"));

        Assert.Equal(0, viPham);
    }

    /// <summary>
    /// `PRO-001-arch` §2.2 (`D-306`) — allowlist **ĐÚNG 2** namespace Messaging mà `Promotions.Application` được
    /// `using`: `EventCoverage` (có sẵn) + `Idempotency` (thêm cho `BR-02`/`D-305`). **Không** wildcard, **không**
    /// allow-all. 🚨 Danh sách này là chốt DUY NHẤT — thêm namespace thứ ba phải sửa tường minh + có lý do.
    /// </summary>
    private static readonly string[] AllowlistMessaging =
    [
        "using eCommerce.Messaging.EventCoverage;",
        "using eCommerce.Messaging.Idempotency;"
    ];

    private static List<string> ViPhamMessaging(IEnumerable<string> dong)
        => dong.Select(l => l.Trim())
            .Where(l => l.StartsWith("using eCommerce.Messaging", StringComparison.Ordinal))
            .Where(l => !AllowlistMessaging.Contains(l, StringComparer.Ordinal))
            .ToList();

    [Fact]
    public void RanhGioi__Promotions_Application_chi_dung_2_namespace_Messaging()
    {
        var goc = Path.Combine(Goc(), "modules/Promotions/eCommerce.Promotions.Application");
        var usings = Directory.EnumerateFiles(goc, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(File.ReadAllLines);

        Assert.Equal(2, AllowlistMessaging.Length);
        Assert.Empty(ViPhamMessaging(usings));
    }

    /// <summary>
    /// 🚨 Chống "nới thành allow-all": allowlist **phải bắt** namespace lạ. Nếu ai đổi sang kiểm
    /// `StartsWith("using eCommerce.Messaging")` (luôn đúng) thì ca này **ĐỎ**.
    /// </summary>
    [Fact]
    public void RanhGioi__allowlist_KHONG_phai_allow_all__namespace_la_bi_bat()
    {
        var viPham = ViPhamMessaging(
        [
            "using eCommerce.Messaging.EventCoverage;",
            "using eCommerce.Messaging.Idempotency;",
            "using eCommerce.Messaging.Outbox;"
        ]);

        Assert.Equal(["using eCommerce.Messaging.Outbox;"], viPham);
    }

    [Fact]
    public void Pos_KHONG_tham_chieu_Promotions__TC38()
    {
        var goc = Path.Combine(Goc(), "modules/Pos");
        var tep = Directory.EnumerateFiles(goc, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".csproj", StringComparison.Ordinal))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        var viPham = tep.Where(f => File.ReadAllText(f).Contains("Promotions", StringComparison.Ordinal)).ToList();
        Assert.Empty(viPham);
    }

    [Fact]
    public void Quyen_Promotions_khai_CA_HAI_NOI()
    {
        var consts = File.ReadAllText(Path.Combine(Goc(), "src/eCommerce.Domain.Shared/eCommerceConsts.cs"));
        var provider = File.ReadAllText(Path.Combine(Goc(), "src/eCommerce.Application.Contracts/Permissions/eCommercePermissionDefinitionProvider.cs"));

        Assert.Contains("class Promotions", consts);
        Assert.Contains("GroupName + \".Promotions\"", consts);

        foreach (var ten in new[] { "Default", "View", "Manage", "Approve", "Import" })
        {
            Assert.Contains($"Permissions.Promotions.{ten}", provider);
        }
    }

    [Fact]
    public void DbSet_du_7_entity_Pro()
    {
        var options = new DbContextOptionsBuilder<eCommerce.EntityFrameworkCore.eCommerceDbContext>()
            .UseNpgsql("Host=khong-ket-noi;Database=khong-ket-noi")
            .Options;

        using var db = new eCommerce.EntityFrameworkCore.eCommerceDbContext(options);

        var coDbSet = typeof(eCommerce.EntityFrameworkCore.eCommerceDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToHashSet();

        Type[] bay =
        [
            typeof(Promotion), typeof(PromotionCondition), typeof(PromotionRuleCost),
            typeof(VoucherCode), typeof(PromotionMapping), typeof(FlashSaleSlot), typeof(PromotionRedemption)
        ];

        foreach (var t in bay)
        {
            Assert.Contains(t, coDbSet);
        }
    }
}
