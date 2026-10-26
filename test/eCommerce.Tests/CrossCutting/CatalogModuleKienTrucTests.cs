using System.Text.RegularExpressions;

namespace eCommerce.Tests.CrossCutting;

/// <summary>
/// 🚨 **KIẾN TRÚC MODULE `Catalog`** (`P0 #1`, 24/10/2026) — khoá việc tách bằng **test**, không bằng lời.
///
/// 🔑 **VÌ SAO CẦN:** tách module là việc **một lần**, nhưng *"để nó tái phát"* là việc **mỗi ngày**:
/// ai đó thêm một tệp Catalog mới vào `src/eCommerce.Domain` (vì "tiện"), hoặc thêm `ProjectReference` từ
/// module về `src/eCommerce.Domain` để dùng một entity của host. 📌 Cả hai đều **build xanh**, test nghiệp vụ
/// **xanh**, và biên giới module **biến mất im lặng** — đúng loại lỗi đã xảy ra với `Customers.Application`
/// (module → `src/eCommerce.Domain`).
///
/// 🔒 Test này kiểm **văn bản csproj** + **sự tồn tại của thư mục** (không cần DB/DI).
/// </summary>
public class CatalogModuleKienTrucTests
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

        throw new DirectoryNotFoundException("KHÔNG TÌM THẤY `eCommerce.slnx` ⇒ phép kiểm kiến trúc VO NGHIA.");
    }

    [Theory]
    [InlineData("eCommerce.Catalog.Domain")]
    [InlineData("eCommerce.Catalog.Application.Contracts")]
    [InlineData("eCommerce.Catalog.Application")]
    [InlineData("eCommerce.Catalog.EntityFrameworkCore")]
    public void Module_Catalog_PHAI_co_du_4_project(string ten)
    {
        var duongDan = Path.Combine(GocRepo(), "modules", "Catalog", ten, $"{ten}.csproj");

        Assert.True(File.Exists(duongDan), $"KHÔNG TÌM THẤY project `{ten}` ⇒ module Catalog chưa đủ 4 tầng.");
    }

    [Fact]
    public void Module_Catalog_KHONG_duoc_tham_chieu_src_Domain()
    {
        // 🔒 L2 của kế hoạch tách module: module → `src/eCommerce.Domain` là **CẤM**. Cho phép `Domain.Shared`
        // và `Kernel` (thư viện dùng chung), không cho phép `src/eCommerce.Domain`.
        var thuMucModule = Path.Combine(GocRepo(), "modules", "Catalog");

        var viPham = Directory
            .GetFiles(thuMucModule, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(tep => Regex.Matches(File.ReadAllText(tep), "ProjectReference Include=\"([^\"]+)\"")
                .Select(m => new { Tep = Path.GetFileName(tep), DuongDan = m.Groups[1].Value }))
            .Where(x => x.DuongDan.Contains("src\\eCommerce.Domain\\", StringComparison.OrdinalIgnoreCase)
                        || x.DuongDan.Contains("src/eCommerce.Domain/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(viPham.Count == 0,
            "🚨 module Catalog tham chiếu `src/eCommerce.Domain` (module → host là CẤM):\n  "
            + string.Join("\n  ", viPham.Select(x => $"{x.Tep}: {x.DuongDan}")));
    }

    [Fact]
    public void Thu_muc_Catalog_cu_PHAI_da_bi_xoa()
    {
        var goc = GocRepo();

        Assert.False(
            Directory.Exists(Path.Combine(goc, "src", "eCommerce.Domain", "Entities", "Catalog")),
            "🚨 `src/eCommerce.Domain/Entities/Catalog` vẫn tồn tại ⇒ entity Catalog đã bị đặt lại vào host.");
    }

    [Fact]
    public void Host_DbContext_KHONG_con_mapping_Catalog()
    {
        // 🔒 Lát cắt 2 (24/10/2026): mapping EF phải nằm trong MODULE. Nếu ai đó chép block
        // `builder.Entity<Catalog.*>` trở lại host thì hai nơi cùng cấu hình ⇒ dễ lệch và migration sẽ
        // sinh thay đổi ngoài ý muốn.
        var duongDan = Path.Combine(GocRepo(), "src", "eCommerce.EntityFrameworkCore", "eCommerceDbContext.cs");
        var noiDung = File.ReadAllText(duongDan);

        Assert.DoesNotContain("builder.Entity<Catalog.", noiDung);
        Assert.Contains("ConfigureCatalog(builder)", noiDung);
    }
}