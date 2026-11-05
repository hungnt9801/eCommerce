using System.Text.RegularExpressions;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **KIẾN TRÚC MODULE `Reconciliation`** (`P0 #1`, 24/10/2026).
/// ⚠️ Module này **được phép** tham chiếu `Inventory.Domain` (đọc tồn kho/sổ cái) — **đánh đổi có ý thức**,
/// ghi ở `audit-gd1.md`. Nhưng **cấm** tham chiếu `src/eCommerce.Domain` (host).
/// </summary>
public class ReconciliationModuleKienTrucTests
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
    [InlineData("eCommerce.Reconciliation.Domain")]
    [InlineData("eCommerce.Reconciliation.Application.Contracts")]
    [InlineData("eCommerce.Reconciliation.Application")]
    [InlineData("eCommerce.Reconciliation.EntityFrameworkCore")]
    public void Module_Reconciliation_PHAI_co_du_4_project(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "modules", "Reconciliation", ten, $"{ten}.csproj");
        Assert.True(File.Exists(duongDan), $"KHÔNG TÌM THẤY project `{ten}`.");
    }

    [Fact]
    public void Module_Reconciliation_KHONG_duoc_tham_chieu_src_Domain()
    {
        var thuMuc = Path.Combine(GocRepo(), "modules", "Reconciliation");
        var viPham = Directory.GetFiles(thuMuc, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(tep => Regex.Matches(File.ReadAllText(tep), "ProjectReference Include=\"([^\"]+)\"")
                .Select(m => new { Tep = Path.GetFileName(tep), DuongDan = m.Groups[1].Value }))
            .Where(x => x.DuongDan.Contains("src\\eCommerce.Domain\\", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("src/eCommerce.Domain/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0, "module Reconciliation tham chiếu host (CẤM):\n  "
            + string.Join("\n  ", viPham.Select(x => $"{x.Tep}: {x.DuongDan}")));
    }

    [Fact]
    public void Host_DbContext_KHONG_con_mapping_Recon()
    {
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.EntityFrameworkCore", "eCommerceDbContext.cs");
        var noiDung = File.ReadAllText(duongDan);

        Assert.DoesNotContain("builder.Entity<Inventory.ReconRun>", noiDung);
        Assert.Contains("ConfigureReconciliation(builder)", noiDung);
    }

    [Theory]
    [InlineData("ReconRun.cs")]
    [InlineData("DoiSoatTonKho.cs")]
    public void Entity_da_chuyen_KHONG_con_trong_host(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.Domain", "Entities", "Inventory", ten);
        Assert.False(File.Exists(duongDan), $"`{ten}` vẫn ở host.");
    }

    [Fact]
    public void Module_Reconciliation_KHONG_tham_chieu_Inventory()
    {
        // ✅ 26/10/2026 — đã gỡ nợ: chỉ dùng `IStockBalanceReader`/`IWarehouseDirectory` (host hiện thực).
        var thuMuc = Path.Combine(GocRepo(), "modules", "Reconciliation");
        var viPham = Directory.GetFiles(thuMuc, "*.csproj", SearchOption.AllDirectories)
            .Where(tep => File.ReadAllText(tep).Contains("Inventory", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(viPham.Count == 0,
            "module Reconciliation còn tham chiếu Inventory:\n  " + string.Join("\n  ", viPham));
    }
}