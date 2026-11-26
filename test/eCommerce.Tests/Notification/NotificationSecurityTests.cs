using System.Reflection;
using eCommerce.HttpApi.Controllers;
using eCommerce.Notification;
using eCommerce.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization.Permissions;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-38` — `BR-NOT-001-016`: **`[Authorize]` cấp CLASS** + **mọi permission được ĐỊNH NGHĨA**
/// (`SRS §6.1`, `§9`, `XC-004` BR-001/BR-002/BR-003).
///
/// 🚨 Hai lỗi **ĐÃ XẢY RA THẬT** ở Phase 0 (`GetShopsAsync` thành API công khai; API 403 cho mọi người
/// vì permission chưa định nghĩa). Test này khoá bất biến cho **riêng** `NOT-001` ở mức reflection —
/// kiểm cả **controller `/api/v1`** (9 op) lẫn **3 app service**.
///
/// 📌 Phần HTTP thật `401`/`403`/`200` cần host `WebApplicationFactory` — **CHƯA KIỂM ĐƯỢC** trong
/// phạm vi hạ tầng hiện có (xem testplan §"Chưa kiểm được"). Ở đây kiểm **nguyên nhân gốc** (thiếu
/// attribute / permission mồ côi) — đúng loại lỗi không có phản hồi biên dịch.
/// </summary>
public class NotificationSecurityTests
{
    private static readonly Type[] BaAppService =
    [
        typeof(NotificationTemplateAppService),
        typeof(NotificationQueueAppService),
        typeof(NotificationBatchAppService)
    ];

    private static readonly Type[] BaController =
    [
        typeof(NotificationTemplatesController),
        typeof(NotificationQueueController),
        typeof(NotificationBatchesController)
    ];

    private static IReadOnlyList<string> PermissionDaDinhNghia()
    {
        var boiCanh = new PermissionDefinitionContext(new ServiceCollection().BuildServiceProvider());
        new eCommercePermissionDefinitionProvider().Define(boiCanh);

        return boiCanh.Groups.Values
            .SelectMany(g => g.Permissions)
            .SelectMany(LayCaCay)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

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

    [Fact]
    public void TC38_ba_app_service_Notification_deu_co_Authorize_o_cap_CLASS()
    {
        // `[Authorize]` cấp class (policy có thể rỗng = chỉ cần đăng nhập) — method thiếu gắn sẽ KHÔNG công khai.
        var thieu = BaAppService
            .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
            .Select(t => t.Name)
            .ToList();

        Assert.True(thieu.Count == 0,
            "🚨 App service Notification THIẾU `[Authorize]` cấp CLASS ⇒ method không gắn gì thành API CÔNG KHAI:\n  "
            + string.Join("\n  ", thieu));
    }

    [Fact]
    public void TC38_khong_app_service_Notification_nao_ha_AllowAnonymous()
    {
        var viPham = BaAppService
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)
                    .Select(_ => $"{t.Name}.{m.Name}")))
            .ToList();

        Assert.True(viPham.Count == 0,
            "🚨 `NOT-001` KHÔNG được có endpoint `[AllowAnonymous]`:\n  " + string.Join("\n  ", viPham));
    }

    [Fact]
    public void TC38_chin_permission_Notification_deu_duoc_DINH_NGHIA()
    {
        string[] chin =
        [
            eCommerceConsts.Permissions.Notifications.TemplatesView,
            eCommerceConsts.Permissions.Notifications.TemplatesManage,
            eCommerceConsts.Permissions.Notifications.TemplatesPublish,
            eCommerceConsts.Permissions.Notifications.QueueCreate,
            eCommerceConsts.Permissions.Notifications.QueueView,
            eCommerceConsts.Permissions.Notifications.QueueCancel,
            eCommerceConsts.Permissions.Notifications.QueueRetry,
            eCommerceConsts.Permissions.Notifications.BatchesCreate,
            eCommerceConsts.Permissions.Notifications.BatchesApprove
        ];

        var daDinhNghia = PermissionDaDinhNghia().ToHashSet(StringComparer.Ordinal);
        var moCoi = chin.Where(p => !daDinhNghia.Contains(p)).ToList();

        Assert.True(moCoi.Count == 0,
            "🚨 Permission Notification dùng trong `[Authorize]` nhưng CHƯA ĐƯỢC ĐỊNH NGHĨA " +
            "(⇒ 403 cho MỌI người, kể cả admin):\n  " + string.Join("\n  ", moCoi));
    }

    [Fact]
    public void TC38_ba_controller_v1_co_Authorize_cap_class_va_khong_AllowAnonymous()
    {
        foreach (var c in BaController)
        {
            var auth = c.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
            Assert.True(auth.Count > 0, $"🚨 Controller `{c.Name}` thiếu `[Authorize]` cấp class.");
            Assert.All(auth, a => Assert.False(string.IsNullOrWhiteSpace(a.Policy),
                $"🚨 Controller `{c.Name}` có `[Authorize]` cấp class nhưng KHÔNG nêu permission."));

            var anon = c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
                .Select(m => m.Name)
                .ToList();

            Assert.True(anon.Count == 0,
                $"🚨 Controller `{c.Name}` có method `[AllowAnonymous]`: {string.Join(", ", anon)}");
        }
    }
}
