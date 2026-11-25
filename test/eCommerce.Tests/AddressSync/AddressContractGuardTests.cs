using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.Address;
using eCommerce.Address.Carrier;
using eCommerce.EntityFrameworkCore;
using eCommerce.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` — bất biến "kiểm bằng máy"** (reflection + quét mã): các luật mà không có lỗi biên dịch nào
/// bắt được nếu ai đó vô tình nới:
///   • **TC-05/TC-17** (BR-002): mọi truy vấn tra cứu phải có `schemaVersion`, **trừ đúng một** ngoại lệ
///     `SoSanhMaXaAsync` (trả DTO mang `SchemaVersion` cho mỗi dòng);
///   • **TC-12** (BR-007): danh tính ĐVHC bất biến — `Code/Level/SchemaVersion/ParentCode/Name` **không**
///     public setter; `DoiTen` bắt buộc tham số căn cứ (không default — `D-239`);
///   • **TC-13** (BR-012): đủ **6** `DbSet` module `Address` trên DbContext hợp nhất;
///   • **TC-18/TC-19** (BR-013/NFR-010): `ImportDatasetAsync` có permission riêng `Addresses.Catalog.Import`,
///     **không** `[AllowAnonymous]`, định nghĩa side `Host`;
///   • **TC-20** (NFR-009): 3 metric mới nằm trong `eCommerceMetricNames.All`;
///   • **TC-25** (`D-82`): khoá `address:sync-catalog` đúng giá trị, không tiền tố, literal xuất hiện đúng 1 lần
///     trong `modules/Address` (sau khi gỡ chú thích).
/// </summary>
public class AddressContractGuardTests
{
    private static readonly string[] NgoaiLeCoLyDo = [nameof(AdministrativeUnitAppService.SoSanhMaXaAsync)];

    // ── TC-05: mọi method tra cứu công khai phải có schemaVersion ────────────

    [Fact]
    public void TC05_moi_truy_van_tra_cuu_phai_co_schemaVersion()
    {
        var thieu = typeof(AdministrativeUnitAppService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m => !NgoaiLeCoLyDo.Contains(m.Name))
            .Where(m => m.GetParameters().All(p =>
                !string.Equals(p.Name, "schemaVersion", StringComparison.OrdinalIgnoreCase)
                && !p.ParameterType.GetProperties().Any(x => x.Name == "SchemaVersion")))
            .Select(m => m.Name)
            .ToList();

        Assert.True(
            thieu.Count == 0,
            "Truy vấn tra cứu thiếu `schemaVersion` (nguy cơ lẫn v1/v2 — BR-002):\n" + string.Join("\n", thieu));
    }

    [Fact]
    public void TC17_ngoai_le_SoSanhMaXa_phai_tra_dong_co_SchemaVersion()
    {
        var kieuDong = typeof(AdministrativeUnitAppService)
            .GetMethod(nameof(AdministrativeUnitAppService.SoSanhMaXaAsync))!
            .ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];

        Assert.Contains(kieuDong.GetProperties(), p => p.Name == "SchemaVersion");

        // Ngoại lệ phải nằm trong danh sách TƯỜNG MINH — thêm method khác vào đây cần D-xx.
        Assert.Equal([nameof(AdministrativeUnitAppService.SoSanhMaXaAsync)], NgoaiLeCoLyDo);
    }

    // ── TC-12: danh tính bất biến + DoiTen bắt buộc căn cứ ─────────────────

    [Theory]
    [InlineData(nameof(AdministrativeUnit.Code))]
    [InlineData(nameof(AdministrativeUnit.Level))]
    [InlineData(nameof(AdministrativeUnit.SchemaVersion))]
    [InlineData(nameof(AdministrativeUnit.ParentCode))]
    [InlineData(nameof(AdministrativeUnit.Name))]
    public void TC12_khong_co_public_setter_tren_danh_tinh_va_ten(string tenThuocTinh)
    {
        var p = typeof(AdministrativeUnit).GetProperty(tenThuocTinh);
        Assert.NotNull(p);
        Assert.False(p!.SetMethod is { IsPublic: true }, $"{tenThuocTinh} có public setter ⇒ phá bất biến danh tính (D-77).");
    }

    [Fact]
    public void TC12_DoiTen_bat_buoc_tham_so_can_cu_khong_co_default()
    {
        var m = typeof(AdministrativeUnit).GetMethod(nameof(AdministrativeUnit.DoiTen))!;
        var canCu = m.GetParameters().Single(p => p.Name == "legalBasis");

        Assert.False(canCu.HasDefaultValue, "`DoiTen` không được có giá trị mặc định cho căn cứ (D-239 ⇒ 0009).");
    }

    // ── TC-13: đủ 6 DbSet module Address trên DbContext hợp nhất ────────────

    [Fact]
    public void TC13_du_SAU_DbSet_module_Address_tren_DbContext_hop_nhat()
    {
        var ten = typeof(eCommerceDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] phaiCo =
        [
            "AdministrativeUnits",
            "CatalogSyncRuns",
            "CarrierAddressMappings",
            "CarrierAddressSyncRuns",
            "CarrierAddressSyncRunUnmappedCodes",
            "Addresses"
        ];

        foreach (var t in phaiCo)
        {
            Assert.True(ten.Contains(t), $"Thiếu DbSet `{t}` ⇒ `IRepository<T>` không đăng ký (BR-012).");
        }
    }

    // ── TC-18/TC-19: permission riêng + không AllowAnonymous ───────────────

    [Fact]
    public void TC18_TC19_ImportDatasetAsync_co_permission_rieng_khong_AllowAnonymous()
    {
        var m = typeof(AdministrativeUnitAppService)
            .GetMethod(nameof(AdministrativeUnitAppService.ImportDatasetAsync))!;

        var authorize = m.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(eCommerceConsts.Permissions.Addresses.CatalogImport, authorize!.Policy);
        Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>());

        // NFR-010/BR-013: quyền nạp lô KHÔNG tái dùng `Addresses.Manage` của ADDR-004.
        Assert.NotEqual(eCommerceConsts.Permissions.Addresses.Manage, authorize.Policy);
    }

    [Fact]
    public void TC19_class_AdministrativeUnitAppService_co_Authorize_cap_class()
    {
        var attr = typeof(AdministrativeUnitAppService).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        // Không có [AllowAnonymous] trên class.
        Assert.Null(typeof(AdministrativeUnitAppService).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void TC18_permission_CatalogImport_dinh_nghia_o_Host()
    {
        Assert.Equal("eCommerce.Addresses.Catalog.Import", eCommerceConsts.Permissions.Addresses.CatalogImport);

        var boiCanh = new PermissionDefinitionContext(new ServiceCollection().BuildServiceProvider());
        new eCommercePermissionDefinitionProvider().Define(boiCanh);

        var dinhNghia = boiCanh.Groups.Values
            .SelectMany(g => g.Permissions)
            .SelectMany(LayCaCay)
            .Single(p => p.Name == eCommerceConsts.Permissions.Addresses.CatalogImport);

        Assert.Equal(MultiTenancySides.Host, dinhNghia.MultiTenancySide);
    }

    // ── TC-20: metric mới trong bảng đăng ký ───────────────────────────────

    [Fact]
    public void TC20_ba_metric_dia_chi_nam_trong_eCommerceMetricNames_All()
    {
        string[] phaiCo =
        [
            "address_catalog_sync_total",
            "address_catalog_cache_hits_total",
            "address_catalog_cache_misses_total"
        ];

        foreach (var ten in phaiCo)
        {
            Assert.Contains(ten, ECommerceMetricNamesAll());
        }
    }

    private static IReadOnlyList<string> ECommerceMetricNamesAll()
        => (IReadOnlyList<string>)typeof(eCommerce.Kernel.Observability.eCommerceMetrics).Assembly
            .GetType("eCommerce.Kernel.Observability.eCommerceMetricNames")!
            .GetField("All", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    // ── TC-25: khoá phân tán một nguồn, đúng giá trị, không tiền tố ─────────

    [Fact]
    public void TC25_khoa_CatalogImport_dung_gia_tri_va_khong_tien_to()
    {
        Assert.Equal("address:sync-catalog", AddressLockKeys.CatalogImport);
        Assert.False(AddressLockKeys.CatalogImport.StartsWith("ecommerce:", StringComparison.Ordinal));
    }

    [Fact]
    public void TC25_literal_khoa_xuat_hien_DUNG_MOT_LAN_trong_modules_Address()
    {
        var thuMuc = Path.Combine(GocRepo(), "modules", "Address");

        var soLan = Directory.EnumerateFiles(thuMuc, "*.cs", SearchOption.AllDirectories)
            .Where(p => !LaTepBuild(p))
            .Select(File.ReadAllText)
            .Select(GoChuThich)
            .Sum(noiDung => noiDung.Split("\"address:sync-catalog\"").Length - 1);

        Assert.Equal(1, soLan);
    }

    // ── tiện ích ────────────────────────────────────────────────────────────

    private static IEnumerable<PermissionDefinition> LayCaCay(PermissionDefinition goc)
    {
        yield return goc;

        foreach (var con in goc.Children)
        {
            foreach (var chau in LayCaCay(con))
            {
                yield return chau;
            }
        }
    }

    private static string GoChuThich(string noiDung)
    {
        var khongBlock = Regex.Replace(noiDung, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return string.Join('\n', khongBlock.Split('\n').Select(dong =>
        {
            var viTri = dong.IndexOf("//", StringComparison.Ordinal);
            return viTri >= 0 ? dong[..viTri] : dong;
        }));
    }

    private static bool LaTepBuild(string duongDan)
        => duongDan.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || duongDan.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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

        throw new DirectoryNotFoundException("KHÔNG TÌM THẤY `eCommerce.slnx` ⇒ phép kiểm VÔ NGHĨA.");
    }
}
