using System.Reflection;
using System.Text.Json;
using eCommerce.HttpApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// `PRO-001` / `W25-4` — **hợp đồng OpenAPI `packages/api-client/openapi.v1.json` khớp controller** (`D-308`).
///
/// 🚨 VÌ SAO: `W25-3` tái sinh hợp đồng 75 → 82 path (+7 `/api/v1/voucher/**`). Hợp đồng là **đầu vào** của
/// `apps/admin-web` (`schema.d.ts` sinh từ tệp này) ⇒ nếu controller đổi route/verb mà tệp không tái sinh (hoặc
/// tệp có path không tồn tại thật), FE gọi 404/405 mà **không** có gì đỏ. Test này đối chiếu **hai chiều** bằng
/// phản chiếu `VoucherController` — không host Host/DB.
///
/// 📌 So template `{id:guid}` ↔ `{id}` (OpenAPI bỏ ràng buộc route), theo đúng ghi chú `D-307` ①(5)(c).
/// 🚫 Khẳng định **0** path `/api/v1/voucher/reserve` hay `/api/v1/voucher/commit` (`ADR-13`, `TC-34`).
/// </summary>
public class VoucherOpenApiContractTests
{
    private static readonly string[] PhuongThucHttp =
        ["get", "post", "put", "delete", "patch", "head", "options", "trace"];

    private static string Goc()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "eCommerce.slnx")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("Không tìm thấy gốc repo (eCommerce.slnx).");
    }

    private static JsonDocument DocOpenApi()
    {
        var duongDan = Path.Combine(Goc(), "packages/api-client/openapi.v1.json");
        Assert.True(File.Exists(duongDan), $"Thiếu hợp đồng OpenAPI: {duongDan}");
        return JsonDocument.Parse(File.ReadAllText(duongDan));
    }

    /// <summary>Tập `(verb, path)` của các path `/api/v1/voucher` trong tệp OpenAPI.</summary>
    private static List<string> CapTuOpenApi(JsonDocument doc)
    {
        var ketQua = new List<string>();
        foreach (var p in doc.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!p.Name.StartsWith("/api/v1/voucher", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var op in p.Value.EnumerateObject())
            {
                if (PhuongThucHttp.Contains(op.Name, StringComparer.Ordinal))
                {
                    ketQua.Add($"{op.Name} {p.Name}");
                }
            }
        }

        return ketQua.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>Tập `(verb, "/" + template)` suy từ reflection `VoucherController`; `{id:guid}` → `{id}`.</summary>
    private static List<string> CapTuController()
    {
        var ketQua = new List<string>();
        foreach (var m in typeof(VoucherController)
                     .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var http = m.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().FirstOrDefault();
            if (http is null)
            {
                continue;
            }

            var template = (http.Template ?? string.Empty).Replace("{id:guid}", "{id}", StringComparison.Ordinal);
            var path = "/api/v1/voucher" + (template.Length == 0 ? string.Empty : "/" + template);
            ketQua.Add($"{http.HttpMethods.Single().ToLowerInvariant()} {path}");
        }

        return ketQua.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void D308_dung_7_path_voucher__va_dung_bo_7_path_literal()
    {
        using var doc = DocOpenApi();

        var pathVoucher = doc.RootElement.GetProperty("paths").EnumerateObject()
            .Select(p => p.Name)
            .Where(x => x.StartsWith("/api/v1/voucher", StringComparison.Ordinal))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // 🚨 ĐÚNG 7 (`D-308`) — thêm/bớt path công khai mà không tái sinh hợp đồng ⇒ ĐỎ.
        Assert.Equal(7, pathVoucher.Count);

        string[] mongDoi =
        [
            "/api/v1/voucher",
            "/api/v1/voucher/generate",
            "/api/v1/voucher/redemptions",
            "/api/v1/voucher/{id}",
            "/api/v1/voucher/{id}/issue",
            "/api/v1/voucher/{id}/override-limit",
            "/api/v1/voucher/{id}/revoke",
        ];
        Assert.Equal(mongDoi.OrderBy(x => x, StringComparer.Ordinal), pathVoucher);
    }

    [Fact]
    public void D308_moi_path_co_dung_method_tuong_ung()
    {
        using var doc = DocOpenApi();
        var cap = CapTuOpenApi(doc);

        string[] mongDoi =
        [
            "get /api/v1/voucher",
            "get /api/v1/voucher/redemptions",
            "get /api/v1/voucher/{id}",
            "post /api/v1/voucher/generate",
            "post /api/v1/voucher/{id}/issue",
            "post /api/v1/voucher/{id}/override-limit",
            "post /api/v1/voucher/{id}/revoke",
        ];

        Assert.Equal(mongDoi.OrderBy(x => x, StringComparer.Ordinal), cap);
    }

    [Fact]
    public void D308_OpenAPI_KHONG_co_reserve_commit__khop_ADR13()
    {
        using var doc = DocOpenApi();
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.DoesNotContain(paths, p => p.Contains("/voucher/reserve", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(paths, p => p.Contains("/voucher/commit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void D308_dong_bo_voi_VoucherController_bang_phan_chieu()
    {
        // 🔁 Đối chiếu TỰ ĐỘNG hai chiều: OPENAPI ⇄ controller. Lệch một route/verb ⇒ FE gọi 404/405 im lặng.
        using var doc = DocOpenApi();

        var tuOpenApi = CapTuOpenApi(doc);
        var tuController = CapTuController();

        Assert.Equal(tuController, tuOpenApi);
    }
}
