using System.Reflection;
using eCommerce.HttpApi.Controllers;
using eCommerce.Promotions.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// `TC-70` (`D-285` ⑥, `D-286` ⑥) — **bề mặt `/api/v1/promotion/**` đúng allowlist 7 route**, không mở op ngoài
/// phạm vi. Kiểm **tầng route/controller** bằng **phản chiếu** (không host Host/DB): route template, `[Authorize]`
/// cấp class, map method → permission, và literal `rule-catalog` không bị `{id:guid}` nuốt.
///
/// 🚨 VÌ SAO: thêm/bớt route `/api/v1` mà **không có dòng `D-xx`** (ví dụ mở lại `evaluate`/`validate-voucher`/
/// `import-rule-costs`/`redemptions`) là **thay đổi hợp đồng công khai** — test này ĐỎ để buộc qua quyết định.
/// 📌 Allowlist 10 method của `IPromotionAppService` (`TC-31`) **giữ nguyên** — kiểm nhẹ ở đây, chi tiết ở
/// `PromotionSurfaceTests`.
/// </summary>
public class PromotionV1SurfaceTests
{
    private const string RouteGoc = "api/v1/promotion";

    private static string Goc()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "eCommerce.slnx")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("Không tìm thấy gốc repo (eCommerce.slnx).");
    }

    private static string? Policy(object attr)
        => attr.GetType().GetProperty("Policy")?.GetValue(attr) as string;

    private static string? PolicyCuaMethod(MethodInfo m)
    {
        var attr = m.GetCustomAttributes(inherit: true)
            .Where(a => a.GetType().Name == "AuthorizeAttribute")
            .ToList();
        Assert.Single(attr);
        return Policy(attr[0]);
    }

    [Fact]
    public void TC70a_route_template_cap_class_dung_api_v1_promotion()
    {
        var route = typeof(PromotionsController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal(RouteGoc, route!.Template);
    }

    [Fact]
    public void TC70a_Authorize_cap_CLASS_la_Promotions_Default()
    {
        var lop = typeof(PromotionsController).GetCustomAttributes(inherit: true)
            .Where(a => a.GetType().Name == "AuthorizeAttribute")
            .ToList();

        Assert.Single(lop);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Default, Policy(lop[0]));
    }

    [Fact]
    public void TC70a_dung_7_route__va_0_route_ngoai_pham_vi()
    {
        // Gom (verb, template) của mọi action có `[HttpGet]`/`[HttpPost]`.
        var route = typeof(PromotionsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => new
            {
                Method = m,
                Http = m.GetCustomAttributes(inherit: true)
                    .OfType<HttpMethodAttribute>()
                    .FirstOrDefault()
            })
            .Where(x => x.Http is not null)
            .Select(x => new
            {
                x.Method,
                Verb = x.Http!.HttpMethods.Single(),
                Template = x.Http.Template ?? string.Empty
            })
            .ToList();

        // 🚨 ĐÚNG 7 route — thêm/bớt mà không có dòng `D-xx` ⇒ test ĐỎ.
        Assert.Equal(7, route.Count);

        var tap = route.Select(r => $"{r.Verb} {r.Template}").OrderBy(x => x, StringComparer.Ordinal).ToList();
        string[] mongDoi =
        [
            "GET ",
            "GET rule-catalog",
            "GET {id:guid}",
            "POST ",
            "POST {id:guid}/approve",
            "POST {id:guid}/save-tree",
            "POST {id:guid}/set-status",
        ];
        Assert.Equal(mongDoi.OrderBy(x => x, StringComparer.Ordinal), tap);

        // 🚫 4 op KHÔNG được mở ở v1 (`D-285` ④, `D-286`): evaluate / validate-voucher / import-rule-costs / redemptions.
        foreach (var cam in new[] { "evaluate", "validate-voucher", "import-rule-costs", "redemptions" })
        {
            Assert.DoesNotContain(route, r => r.Template.Contains(cam, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void TC70a_map_method_sang_permission_dung()
    {
        var am = typeof(PromotionsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any())
            .ToDictionary(m => m.Name, m => PolicyCuaMethod(m), StringComparer.Ordinal);

        Assert.Equal(eCommerceConsts.Permissions.Promotions.View, am[nameof(PromotionsController.GetListAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.View, am[nameof(PromotionsController.GetRuleCatalogAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.View, am[nameof(PromotionsController.GetAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Manage, am[nameof(PromotionsController.CreateAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Manage, am[nameof(PromotionsController.SaveTreeAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Approve, am[nameof(PromotionsController.ApproveAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Manage, am[nameof(PromotionsController.SetStatusAsync)]);
    }

    [Fact]
    public void TC70d_rule_catalog_la_literal_khong_bi_id_guid_nuot()
    {
        // Route literal `rule-catalog` KHÔNG có ràng buộc `:guid` ⇒ `GET /{id:guid}` không nuốt nó.
        var m = typeof(PromotionsController).GetMethod(nameof(PromotionsController.GetRuleCatalogAsync))!;
        var http = m.GetCustomAttributes(inherit: true).OfType<HttpGetAttribute>().Single();

        Assert.Equal("rule-catalog", http.Template);
        Assert.DoesNotContain(":guid", http.Template!);

        // 📌 Khai báo literal PHẢI đứng TRƯỚC `{id:guid}` trong nguồn (đọc tĩnh) — không dựa vào thứ tự reflection.
        var nguon = File.ReadAllText(Path.Combine(
            Goc(), "src/eCommerce.HttpApi/Controllers/PromotionsController.cs"));
        var viTriLiteral = nguon.IndexOf("[HttpGet(\"rule-catalog\")]", StringComparison.Ordinal);
        var viTriId = nguon.IndexOf("[HttpGet(\"{id:guid}\")]", StringComparison.Ordinal);

        Assert.True(viTriLiteral >= 0 && viTriId >= 0);
        Assert.True(viTriLiteral < viTriId,
            "`rule-catalog` phải khai báo TRƯỚC `{id:guid}` để literal không bị route tham số che.");
    }

    [Fact]
    public void TC70b_IPromotionAppService_van_dung_10_method__khong_UpdateAsync()
    {
        // `TC-31` — allowlist 10 method giữ nguyên (thêm `UpdateAsync` hay bớt method ⇒ mở đường sửa trái `D-150`).
        var ten = typeof(IPromotionAppService).GetMethods().Select(m => m.Name).ToList();

        Assert.Equal(10, ten.Count);
        Assert.DoesNotContain("UpdateAsync", ten);
    }
}
