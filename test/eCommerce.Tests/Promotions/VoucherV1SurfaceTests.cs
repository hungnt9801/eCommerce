using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.HttpApi.Controllers;
using eCommerce.Promotions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace eCommerce.Promotions.Tests;

/// <summary>
/// `PRO-001` / `W25-4` — **bề mặt `/api/v1/voucher/**` đúng allowlist 7 route** (`D-299` ①, `D-307` ①(5)(c), `TC-34`).
///
/// 🚨 VÌ SAO: thêm/bớt route `/api/v1/voucher/**` (ví dụ mở lại `reserve`/`commit`) là **thay đổi hợp đồng công khai**
/// và phá `ADR-13` (giữ/chốt lượt phải chạy **trong** UoW đặt hàng, không qua HTTP riêng) — test này ĐỎ để buộc qua
/// quyết định `D-xx`. Kiểm **tầng route/controller** bằng **phản chiếu** (không Host/DB), khuôn `PromotionV1SurfaceTests`.
///
/// 🔐 Đồng thời khoá: `[Authorize]` cấp **CLASS** = `Promotions.Default` (`BR-15`), map method → permission hẹp
/// (`.Voucher.Generate`/`.Voucher.Publish`/`.Voucher.OverrideLimit`, `CR-10`), **0** `[AllowAnonymous]` (`BR-24`,
/// `D-146`), và allowlist **đúng 7** method của `IVoucherAdminAppService` (`TC-40`, `BR-18` — không `UpdateAsync`).
/// </summary>
public class VoucherV1SurfaceTests
{
    private const string RouteGoc = "api/v1/voucher";

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

    private static List<(string Verb, string Template, MethodInfo Method)> RouteCuaController()
        => typeof(VoucherController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => new
            {
                Method = m,
                Http = m.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().FirstOrDefault()
            })
            .Where(x => x.Http is not null)
            .Select(x => (x.Http!.HttpMethods.Single(), x.Http.Template ?? string.Empty, x.Method))
            .ToList();

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  Route + quyền cấp class
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void D307a_route_template_cap_class_dung_api_v1_voucher()
    {
        var route = typeof(VoucherController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal(RouteGoc, route!.Template);
    }

    [Fact]
    public void D307a_Authorize_cap_CLASS_la_Promotions_Default()
    {
        var lop = typeof(VoucherController).GetCustomAttributes(inherit: true)
            .Where(a => a.GetType().Name == "AuthorizeAttribute")
            .ToList();

        // 🔐 ĐÚNG MỘT `[Authorize]` cấp class (`BR-15`, `XC-004` BR-001) — thêm cái thứ hai = policy chồng nhau.
        Assert.Single(lop);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.Default, Policy(lop[0]));
    }

    [Fact]
    public void D307a_dung_7_route__va_0_route_ngoai_pham_vi()
    {
        var route = RouteCuaController();

        // 🚨 ĐÚNG 7 route — thêm/bớt mà không có dòng `D-xx` ⇒ test ĐỎ.
        Assert.Equal(7, route.Count);

        var tap = route.Select(r => $"{r.Verb} {r.Template}").OrderBy(x => x, StringComparer.Ordinal).ToList();
        string[] mongDoi =
        [
            "GET ",
            "GET {id:guid}",
            "GET redemptions",
            "POST generate",
            "POST {id:guid}/issue",
            "POST {id:guid}/override-limit",
            "POST {id:guid}/revoke",
        ];
        Assert.Equal(mongDoi.OrderBy(x => x, StringComparer.Ordinal), tap);

        // 🚫 Không route `reserve`/`commit` (`TC-34`, `ADR-13`): hợp đồng nội bộ `Sales` gọi in-process trong UoW.
        foreach (var cam in new[] { "reserve", "commit" })
        {
            Assert.DoesNotContain(route, r => r.Template.Contains(cam, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void D307a_map_method_sang_permission_dung()
    {
        var am = RouteCuaController()
            .ToDictionary(r => r.Method.Name, r => PolicyCuaMethod(r.Method), StringComparer.Ordinal);

        Assert.Equal(eCommerceConsts.Permissions.Promotions.View, am[nameof(VoucherController.GetListAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.View, am[nameof(VoucherController.GetAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.View, am[nameof(VoucherController.GetRedemptionsAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.VoucherGenerate, am[nameof(VoucherController.GenerateAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.VoucherPublish, am[nameof(VoucherController.IssueAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.VoucherPublish, am[nameof(VoucherController.RevokeAsync)]);
        Assert.Equal(eCommerceConsts.Permissions.Promotions.VoucherOverrideLimit, am[nameof(VoucherController.OverrideLimitAsync)]);
    }

    [Fact]
    public void BR24_Khong_co_AllowAnonymous_trong_VoucherController_va_khong_co_reserve_commit_trong_nguon()
    {
        // `BR-24` (`D-146`) + `TC-34` (`ADR-13`): quét mã NGUỒN sau khi bỏ chú thích `//` và `/* */`
        // (chú thích nhắc `[AllowAnonymous]`/`reserve` KHÔNG được làm test xanh giả).
        var nguon = File.ReadAllText(Path.Combine(
            Goc(), "src/eCommerce.HttpApi/Controllers/VoucherController.cs"));

        nguon = Regex.Replace(nguon, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        nguon = Regex.Replace(nguon, @"//[^\r\n]*", " ");

        Assert.DoesNotContain("[AllowAnonymous]", nguon, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reserve", nguon, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"commit", nguon, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════════
    //  Allowlist app service (`TC-40`, `BR-18`) + bề mặt auto `/api/app/voucher-admin/**`
    // ══════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC40_IVoucherAdminAppService_dung_7_method__khong_UpdateAsync()
    {
        var ten = typeof(IVoucherAdminAppService).GetMethods().Select(m => m.Name)
            .OrderBy(x => x, StringComparer.Ordinal).ToList();

        string[] mongDoi =
        [
            "GenerateAsync",
            "GetAsync",
            "GetListAsync",
            "GetRedemptionsAsync",
            "IssueAsync",
            "OverrideLimitAsync",
            "RevokeAsync",
        ];

        // 🚨 ĐÚNG 7 method (`D-302` ①(3)) — thêm `UpdateAsync`/method mới ⇒ mở đường sửa trái `BR-18`/`D-150` ⇒ ĐỎ.
        Assert.Equal(mongDoi, ten);
    }

    [Fact]
    public void TC40_controller_KHONG_tu_them_op_ngoai_7_method_cua_hop_dong()
    {
        // Mỗi action `/api/v1/voucher` phải gọi một method **có** trong `IVoucherAdminAppService` (không chế op).
        var tenHopDong = typeof(IVoucherAdminAppService).GetMethods().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var tenAction = RouteCuaController().Select(r => r.Method.Name).ToList();

        Assert.Equal(7, tenAction.Count);
        foreach (var ten in tenAction)
        {
            Assert.Contains(ten, tenHopDong);
        }
    }

    [Fact]
    public void D307b_giu_be_mat_auto_voucher_admin__khong_tat_remote_service()
    {
        // 📌 `D-299` ①(1)(2) / `D-307` ①(5)(b): GIỮ NGUYÊN bề mặt auto `/api/app/voucher-admin/**` do ABP sinh.
        // Điều kiện để ABP sinh conventional controller: interface là `IApplicationService` **và** KHÔNG bị
        // `[RemoteService(IsEnabled = false)]`. Chỉ ĐỌC + assert (không host) — xoá/tắt ⇒ bề mặt auto mất IM LẶNG.
        Assert.Contains(typeof(IVoucherAdminAppService).GetInterfaces(),
            i => i.Name == "IApplicationService");

        static bool TatRemote(Type t)
            => t.GetCustomAttributes(inherit: true).Any(a =>
            {
                if (a.GetType().Name != "RemoteServiceAttribute")
                {
                    return false;
                }

                var isEnabled = a.GetType().GetProperty("IsEnabled")?.GetValue(a);
                return isEnabled is bool b && !b;
            });

        Assert.False(TatRemote(typeof(IVoucherAdminAppService)));
        Assert.False(TatRemote(typeof(VoucherAdminAppService)));

        // Tên conventional controller suy từ tên class (`…AppService` → `voucher-admin`).
        Assert.Equal("VoucherAdminAppService", nameof(VoucherAdminAppService));
        var tenTaiNguyen = nameof(VoucherAdminAppService).Replace("AppService", string.Empty, StringComparison.Ordinal);
        Assert.Equal("VoucherAdmin", tenTaiNguyen);
    }
}
