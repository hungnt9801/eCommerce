using System.Text.RegularExpressions;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **KIẾN TRÚC MODULE `Sales`** (`P0 #1`, 24/10/2026 — lát cắt 1: Domain + Contracts + Application).
/// ⚠️ Module tham chiếu `Inventory.Domain`/`Pricing.Domain` (dịch vụ miền) — nợ có ý thức, ghi ở `audit-gd1`.
/// 🚧 Mapping EF **còn ở host** — lát cắt kế tiếp.
/// </summary>
public class SalesModuleKienTrucTests
{
    private static string GocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);
        while (thuMuc is not null)
        {
            if (File.Exists(Path.Combine(thuMuc.FullName, "eCommerce.slnx"))) return thuMuc.FullName;
            thuMuc = thuMuc.Parent;
        }
        throw new DirectoryNotFoundException("KHÔNG TÌM THẤY `eCommerce.slnx` ⇒ phép kiểm VO NGHIA.");
    }

    [Theory]
    [InlineData("eCommerce.Sales.Domain")]
    [InlineData("eCommerce.Sales.Application.Contracts")]
    [InlineData("eCommerce.Sales.Application")]
    [InlineData("eCommerce.Sales.EntityFrameworkCore")]
    public void Module_Sales_PHAI_co_du_4_project(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "modules", "Sales", ten, $"{ten}.csproj");
        Assert.True(File.Exists(duongDan), $"KHÔNG TÌM THẤY project `{ten}`.");
    }

    [Fact]
    public void Module_Sales_KHONG_duoc_tham_chieu_src_Domain()
    {
        var thuMuc = Path.Combine(GocRepo(), "modules", "Sales");
        var viPham = Directory.GetFiles(thuMuc, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(tep => Regex.Matches(File.ReadAllText(tep), "ProjectReference Include=\"([^\"]+)\"")
                .Select(m => new { Tep = Path.GetFileName(tep), DuongDan = m.Groups[1].Value }))
            .Where(x => x.DuongDan.Contains("src\\eCommerce.Domain\\", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("src/eCommerce.Domain/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0, "module Sales tham chiếu host (CẤM):\n  "
            + string.Join("\n  ", viPham.Select(x => $"{x.Tep}: {x.DuongDan}")));
    }

    [Theory]
    [InlineData("Order.cs")]
    [InlineData("OrderStatusHistory.cs")]
    [InlineData("MarketplaceEventLog.cs")]
    public void Entity_da_chuyen_KHONG_con_trong_host(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.Domain", "Entities", "Orders", ten);
        Assert.False(File.Exists(duongDan), $"`{ten}` vẫn ở host.");
    }

    [Fact]
    public void Host_DbContext_KHONG_con_mapping_Orders()
    {
        // 🔒 Lát cắt 2 (24/10/2026): mapping đơn hàng/sàn phải nằm trong MODULE.
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.EntityFrameworkCore", "eCommerceDbContext.cs");
        var noiDung = File.ReadAllText(duongDan);

        Assert.DoesNotContain("builder.Entity<Orders.Order>", noiDung);
        Assert.DoesNotContain("builder.Entity<Orders.MarketplaceEventLog>", noiDung);
        Assert.Contains("ConfigureSales(builder)", noiDung);
    }

    [Fact]
    public void Module_Sales_KHONG_tham_chieu_Domain_module_khac()
    {
        // ✅ 26/10/2026 — đã gỡ nợ: dùng `IStockReservationGateway` + `IPriceCalculationAppService` (Contracts).
        var thuMuc = Path.Combine(GocRepo(), "modules", "Sales");
        var viPham = Directory.GetFiles(thuMuc, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(tep => Regex.Matches(File.ReadAllText(tep), "ProjectReference Include=\"([^\"]+)\"")
                .Select(m => new { Tep = Path.GetFileName(tep), DuongDan = m.Groups[1].Value }))
            .Where(x => x.DuongDan.Contains("Inventory.Domain", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("Pricing.Domain", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("Customers.Domain", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("ChannelHub.Domain", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("PaymentHub.Domain", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0, "module Sales còn tham chiếu Domain module khác:\n  "
            + string.Join("\n  ", viPham.Select(x => $"{x.Tep}: {x.DuongDan}")));
    }
}