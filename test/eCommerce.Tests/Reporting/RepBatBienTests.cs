using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.HttpApi.Controllers;
using eCommerce.Reporting;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tests.Reporting;

/// <summary>
/// **BẤT BIẾN KIẾN TRÚC CỦA `REP-001`** — quét mã nguồn + reflection (`REP-001` §8.1, §14.1).
///
/// Các BR dưới đây **không** kiểm được bằng một lời gọi hàm (chúng là luật về **cách viết**):
/// <list type="bullet">
///   <item>**BR-002 / TC-02** — báo cáo **cấm** đọc bảng giao dịch để lấy số đã tổng hợp (allow-list đúng
///     một method `GetTongQuanDonHangAsync`).</item>
///   <item>**BR-001 / TC-13** — không có write endpoint trên bảng fact.</item>
///   <item>**BR-010 / TC-15** — một chiều: module nguồn **không** tham chiếu `Reporting`.</item>
///   <item>**BR-009 / TC-16** — chỉ xoá trong lượt thay thế theo `(ngày, nguồn)`; không xoá lẻ.</item>
///   <item>**BR-003 / TC-20** — 8 entity read-model là `IMultiTenant` + `TenantId` nullable.</item>
///   <item>**BR-015 / TC-21** — không SQL nối chuỗi; tham số nhóm/sắp xếp là tập đóng (enum).</item>
///   <item>**§12** — mọi mã `eCommerce:Reports:0010–0017` map đúng HTTP status ở Host.</item>
/// </list>
/// 📌 Mỗi phép quét có **lưới an toàn**: khẳng định nó **thấy** bằng chứng (nếu không, phép quét hỏng chứ
/// không phải mã sạch).
/// </summary>
public class RepBatBienTests
{
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

    private static readonly string[] BienGiaoDich =
    [
        "_donHang.", "_dongDonHang.", "OrdOrders", "OrdOrderLines",
        "RetailBill", "RetailBillLine", "InventoryItem", "InvItems"
    ];

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-02 / BR-002 — chỉ `GetTongQuanDonHangAsync` đọc bảng giao dịch trong read service
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC02_ReportAppService_chi_GetTongQuanDonHangAsync_dung_bang_giao_dich()
    {
        var goc = TimGocRepo();
        Assert.False(string.IsNullOrEmpty(goc));

        var tep = Path.Combine(
            goc, "modules", "Reporting", "eCommerce.Reporting.Application", "ReportAppService.cs");

        var dong = File.ReadAllLines(tep);
        var methodHienTai = "fields";
        var viPham = new List<string>();
        var thayAllow = 0;

        var khaiBaoMethod = new Regex(
            @"^\s{4}(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[\w<>,\?\[\]\s\.]+\s+(?<ten>\w+)\s*\(",
            RegexOptions.Compiled);

        foreach (var t in dong)
        {
            var m = khaiBaoMethod.Match(t);

            if (m.Success)
            {
                methodHienTai = m.Groups["ten"].Value;
            }

            if (!BienGiaoDich.Any(b => t.Contains(b, StringComparison.Ordinal)))
            {
                continue;
            }

            if (methodHienTai == "GetTongQuanDonHangAsync")
            {
                thayAllow++;
                continue;
            }

            // Cho phép: field/ctor (khai báo + tiêm phụ thuộc) — DI không phải "đọc dữ liệu".
            if (methodHienTai is "fields" or "ReportAppService")
            {
                continue;
            }

            viPham.Add($"[{methodHienTai}] {t.Trim()}");
        }

        // 🔒 Lưới an toàn: phép quét PHẢI thấy ít nhất một lần dùng trong allow-list.
        Assert.True(thayAllow > 0, "Phép quét không thấy `_donHang.` trong GetTongQuanDonHangAsync — phép quét hỏng.");
        Assert.Empty(viPham);
    }

    [Fact]
    public void TC02_hop_dong_Application_Contracts_KHONG_tham_chieu_entity_giao_dich()
    {
        var goc = TimGocRepo();
        var thuMuc = Path.Combine(goc, "modules", "Reporting", "eCommerce.Reporting.Application.Contracts");

        var xau = new List<string>();

        foreach (var tep in Directory.EnumerateFiles(thuMuc, "*.cs", SearchOption.AllDirectories))
        {
            // 🔒 Bỏ DÒNG CHÚ THÍCH: luật là "hợp đồng không THAM CHIẾU kiểu", không cấm nhắc tên bảng trong
            // giải thích (vd. `ReportingDtos.cs` giải thích vì sao `GetTongQuanDonHangAsync` đọc `OrdOrders`).
            var ma = string.Join('\n', File.ReadAllLines(tep).Where(l =>
            {
                var s = l.TrimStart();
                return !s.StartsWith("//", StringComparison.Ordinal)
                       && !s.StartsWith("*", StringComparison.Ordinal)
                       && !s.StartsWith("/*", StringComparison.Ordinal);
            }));

            foreach (var ten in new[] { "RetailBill", "RetailBillLine", "OrdOrders", "OrdOrderLines", "InventoryItem" })
            {
                if (ma.Contains(ten, StringComparison.Ordinal))
                {
                    xau.Add($"{Path.GetFileName(tep)}: {ten}");
                }
            }
        }

        Assert.Empty(xau);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-13 / BR-001 — KHÔNG có write endpoint trên bảng fact
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC13_ReportsController_chi_6_POST_dung_viec_va_KHONG_PUT_PATCH_DELETE()
    {
        var methods = typeof(ReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Empty(methods.SelectMany(m => m.GetCustomAttributes<HttpPutAttribute>()));
        Assert.Empty(methods.SelectMany(m => m.GetCustomAttributes<HttpPatchAttribute>()));
        Assert.Empty(methods.SelectMany(m => m.GetCustomAttributes<HttpDeleteAttribute>()));

        var posts = methods
            .SelectMany(m => m.GetCustomAttributes<HttpPostAttribute>().Select(a => a.Template ?? string.Empty))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[]
            {
                "aggregate",
                "period-close",
                "period-close/{id:guid}/reopen",
                "rebuild",
                "rebuild/{runId:guid}/resume",
                "reconciliation"
            },
            posts);

        var gets = methods
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>().Select(a => a.Template ?? string.Empty))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(9, gets.Count);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-15 / BR-010 — một chiều: module nguồn KHÔNG tham chiếu Reporting
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC15_module_nguon_KHONG_tham_chieu_assembly_Reporting()
    {
        var nguon = new[]
        {
            typeof(eCommerce.Orders.Order).Assembly,
            typeof(eCommerce.Pos.RetailBill).Assembly,
            typeof(eCommerce.Inventory.InventoryItem).Assembly
        };

        Assert.Equal(3, nguon.Length);

        var xau = new List<string>();

        foreach (var asm in nguon)
        {
            foreach (var thamChieu in asm.GetReferencedAssemblies())
            {
                if (thamChieu.Name is not null
                    && thamChieu.Name.StartsWith("eCommerce.Reporting", StringComparison.Ordinal))
                {
                    xau.Add($"{asm.GetName().Name} -> {thamChieu.Name}");
                }
            }

            // Quét thêm kiểu (phòng khi tham chiếu vòng qua kiểu generic).
            foreach (var t in asm.GetTypes())
            {
                if (t.Namespace is not null && t.Namespace.StartsWith("eCommerce.Reporting", StringComparison.Ordinal))
                {
                    xau.Add($"{asm.GetName().Name} -> {t.FullName}");
                }
            }
        }

        Assert.Empty(xau);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-16 / BR-009 — chỉ xoá trong lượt thay thế theo (ngày, nguồn)
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC16_moi_lenh_xoa_nam_trong_TongHopBaoCaoService_va_loc_theo_ngay()
    {
        var goc = TimGocRepo();
        var thuMuc = Path.Combine(goc, "modules", "Reporting");

        var soLenhXoa = 0;
        var viPham = new List<string>();

        foreach (var tep in Directory.EnumerateFiles(thuMuc, "*.cs", SearchOption.AllDirectories))
        {
            if (tep.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || tep.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var dong = File.ReadAllLines(tep);

            for (var i = 0; i < dong.Length; i++)
            {
                if (!dong[i].Contains("DeleteAsync(", StringComparison.Ordinal)
                    && !dong[i].Contains("DeleteManyAsync(", StringComparison.Ordinal)
                    && !dong[i].Contains("DeleteDirectAsync(", StringComparison.Ordinal))
                {
                    continue;
                }

                soLenhXoa++;

                var laDungCua = Path.GetFileName(tep) == "TongHopBaoCaoService.cs";
                var locTheoNgay = dong[i].Contains("BusinessDate ==", StringComparison.Ordinal)
                                  || dong[i].Contains("SnapshotDate ==", StringComparison.Ordinal);

                if (!laDungCua || !locTheoNgay)
                {
                    viPham.Add($"{Path.GetFileName(tep)}:{i + 1} {dong[i].Trim()}");
                }
            }
        }

        // 🔒 Lưới an toàn: phải thấy ≥3 lệnh xoá (2 bảng doanh thu ở 2 đường + 1 ảnh tồn kho).
        Assert.True(soLenhXoa >= 3, $"Phép quét chỉ thấy {soLenhXoa} lệnh xoá — phép quét hỏng.");
        Assert.Empty(viPham);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-20 / BR-003 — 8 entity read-model là IMultiTenant, TenantId nullable
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC20_du_TAM_entity_read_model_la_IMultiTenant_va_TenantId_nullable()
    {
        var asm = typeof(RptFactSalesLine).Assembly;

        var thucThe = asm.GetTypes()
            .Where(t => t.Namespace is "eCommerce.Reporting"
                        && t is { IsClass: true, IsAbstract: false }
                        && t.Name.StartsWith("Rpt", StringComparison.Ordinal))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[]
            {
                "RptAggDailySales", "RptDimDate", "RptDimProduct", "RptDimStaff",
                "RptDimStore", "RptFactInventorySnapshot", "RptFactSalesLine", "RptWatermark"
            },
            thucThe.Select(t => t.Name).ToArray());

        foreach (var t in thucThe)
        {
            Assert.True(typeof(IMultiTenant).IsAssignableFrom(t), $"{t.Name} KHÔNG phải IMultiTenant");

            var tenantId = t.GetProperty("TenantId");
            Assert.NotNull(tenantId);
            Assert.Equal(typeof(Guid?), tenantId!.PropertyType);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // TC-21 / BR-015 — không SQL nối chuỗi; nhóm/sắp xếp là tập đóng
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC21_module_Reporting_KHONG_co_SQL_noi_chuoi()
    {
        var goc = TimGocRepo();
        var thuMuc = Path.Combine(goc, "modules", "Reporting");

        var viPham = new List<string>();
        var soTep = 0;

        foreach (var tep in Directory.EnumerateFiles(thuMuc, "*.cs", SearchOption.AllDirectories))
        {
            if (tep.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || tep.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            soTep++;
            var noiDung = File.ReadAllText(tep);

            if (noiDung.Contains("$\"SELECT", StringComparison.OrdinalIgnoreCase)
                || noiDung.Contains("+ \"SELECT", StringComparison.OrdinalIgnoreCase)
                || noiDung.Contains("\"SELECT ", StringComparison.OrdinalIgnoreCase))
            {
                viPham.Add(Path.GetFileName(tep));
            }
        }

        Assert.True(soTep > 10, $"Phép quét chỉ thấy {soTep} tệp — phép quét hỏng.");
        Assert.Empty(viPham);
    }

    [Fact]
    public void TC21_tham_so_nhom_sap_xep_la_tap_DONG_enum_khong_phai_chuoi()
    {
        foreach (var loai in new[]
                 {
                     typeof(GetLanChayInput), typeof(GetKyChotInput), typeof(GetLechSoInput)
                 })
        {
            foreach (var p in loai.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name.Contains("Sort", StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains("Group", StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains("OrderBy", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"{loai.Name}.{p.Name} là chuỗi tự do ⇒ mở đường SQL/động — phải là enum/tập đóng.");
                }
            }
        }

        Assert.Equal(typeof(RepProjection?), typeof(GetLanChayInput).GetProperty("Projection")!.PropertyType);
        Assert.Equal(typeof(RepRunStatus?), typeof(GetLanChayInput).GetProperty("Status")!.PropertyType);
        Assert.Equal(typeof(RepPeriodStatus?), typeof(GetKyChotInput).GetProperty("Status")!.PropertyType);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // §12 — mã 0010–0017 map đúng HTTP status ở Host (sai ⇒ ABP trả 403 "không có quyền")
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("LanChayKhongTonTai", "NotFound")]        // 0010
    [InlineData("KyDaChot", "Conflict")]                  // 0011
    [InlineData("KyChongLan", "Conflict")]                // 0012
    [InlineData("KhongHoTroRebuild", "BadRequest")]       // 0013
    [InlineData("ConLechChuaXuLy", "Conflict")]           // 0014
    [InlineData("ChuyenTrangThaiKhongHopLe", "Conflict")] // 0015
    [InlineData("ThieuLyDo", "BadRequest")]               // 0016
    [InlineData("CheckpointKhongCo", "Conflict")]         // 0017
    public void MaLoi_0010_0017_map_dung_HTTP_status(string tenHang, string status)
    {
        var goc = TimGocRepo();
        var tep = Path.Combine(goc, "src", "eCommerce.HttpApi.Host", "eCommerceHttpApiHostModule.cs");
        var noiDung = File.ReadAllText(tep);

        Assert.Contains("AbpExceptionHttpStatusCodeOptions", noiDung, StringComparison.Ordinal);

        var dong = $"ReportingErrorCodes.{tenHang}";
        var viTri = noiDung.IndexOf(dong, StringComparison.Ordinal);

        Assert.True(viTri >= 0, $"Không thấy map `{dong}` ở Host — mã lỗi sẽ rơi về 403.");

        var cuoiDong = noiDung.IndexOf(';', viTri);
        var cauLenh = noiDung.Substring(viTri, cuoiDong - viTri);

        Assert.Contains($"HttpStatusCode.{status}", cauLenh, StringComparison.Ordinal);
    }
}
