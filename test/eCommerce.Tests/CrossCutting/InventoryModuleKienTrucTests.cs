using System.Text.RegularExpressions;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **KIẾN TRÚC MODULE `Inventory`** (`P0 #1`, 24/10/2026) — khoá việc tách bằng test.
/// 📌 Cùng khuôn `CatalogModuleKienTrucTests`; phần **đối soát kho** cố ý còn ở host (sẽ sang `Reconciliation`).
/// </summary>
public class InventoryModuleKienTrucTests
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
    [InlineData("eCommerce.Inventory.Domain")]
    [InlineData("eCommerce.Inventory.Application.Contracts")]
    [InlineData("eCommerce.Inventory.Application")]
    [InlineData("eCommerce.Inventory.EntityFrameworkCore")]
    public void Module_Inventory_PHAI_co_du_4_project(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "modules", "Inventory", ten, $"{ten}.csproj");
        Assert.True(File.Exists(duongDan), $"KHÔNG TÌM THẤY project `{ten}`.");
    }

    [Fact]
    public void Module_Inventory_KHONG_duoc_tham_chieu_src_Domain()
    {
        var thuMuc = Path.Combine(GocRepo(), "modules", "Inventory");
        var viPham = Directory.GetFiles(thuMuc, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(tep => Regex.Matches(File.ReadAllText(tep), "ProjectReference Include=\"([^\"]+)\"")
                .Select(m => new { Tep = Path.GetFileName(tep), DuongDan = m.Groups[1].Value }))
            .Where(x => x.DuongDan.Contains("src\\eCommerce.Domain\\", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("src/eCommerce.Domain/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0, "module Inventory tham chiếu `src/eCommerce.Domain` (CẤM):\n  "
            + string.Join("\n  ", viPham.Select(x => $"{x.Tep}: {x.DuongDan}")));
    }

    [Theory]
    [InlineData("StockLedger.cs")]
    [InlineData("StockReservationService.cs")]
    [InlineData("StockDocument.cs")]
    public void Entity_da_chuyen_KHONG_con_trong_host(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.Domain", "Entities", "Inventory", ten);
        Assert.False(File.Exists(duongDan), $"`{ten}` vẫn ở host ⇒ entity Inventory đã bị đặt lại.");
    }

    [Fact]
    public void Host_DbContext_KHONG_con_mapping_Inventory_core()
    {
        // 🔒 Lát cắt 2 (24/10/2026): mapping tồn kho phải nằm trong MODULE. Recon (`ReconRun`…) còn ở host
        // cho tới khi tách `Reconciliation` ⇒ **không** kiểm nó ở đây (tránh test nói dối về phạm vi).
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.EntityFrameworkCore", "eCommerceDbContext.cs");
        var noiDung = File.ReadAllText(duongDan);

        Assert.DoesNotContain("builder.Entity<Inventory.InventoryItem>", noiDung);
        Assert.DoesNotContain("builder.Entity<Inventory.StockLedgerEntry>", noiDung);
        Assert.Contains("ConfigureInventory(builder)", noiDung);
    }
}