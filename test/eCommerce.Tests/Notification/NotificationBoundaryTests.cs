using System.Reflection;
using eCommerce.Notification;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-33` — `BR-NOT-001-019` / `ADR-09`: **module `Notification` CẤM gọi app service của module khác**;
/// chỉ **tiêu thụ ETO** (`SRS §0 ADR-09`, `ARCH §2.1`).
///
/// 🔒 Chứng minh bằng **hai chiều**:
///   ① assembly `Notification.Application` **không** tham chiếu assembly module khác (`GetReferencedAssemblies`),
///   ② không constructor/field nào nhận kiểu `*AppService` ngoài namespace `eCommerce.Notification`
///      (đúng lỗi *"gọi `IOrdersAppService.GetAsync()` để lấy số tiền"* mà ADR-09 cấm).
///
/// 📌 Vì sao cần test dù `csproj` hiện không tham chiếu: một dòng `ProjectReference` thêm vào là **build xanh**
/// và lỗi chỉ lộ khi kiến trúc đã rạn — test biến nó thành CI đỏ ngay.
/// </summary>
public class NotificationBoundaryTests
{
    private static readonly Assembly Application = typeof(NotificationQueueAppender).Assembly;

    /// <summary>Các assembly `eCommerce.*` module `Notification` **được phép** tham chiếu.</summary>
    private static readonly string[] DuocPhep =
    [
        "eCommerce.Notification",
        "eCommerce.Kernel",
        "eCommerce.Domain.Shared"
    ];

    [Fact]
    public void TC33_Notification_Application_KHONG_tham_chieu_module_khac()
    {
        var thamChieuModuleKhac = Application.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(ten => ten.StartsWith("eCommerce.", StringComparison.Ordinal))
            .Where(ten => !DuocPhep.Any(ok => ten.StartsWith(ok, StringComparison.Ordinal)))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(thamChieuModuleKhac.Count == 0,
            "🚨 `Notification.Application` tham chiếu module KHÁC (ADR-09: chỉ tiêu thụ ETO):\n  "
            + string.Join("\n  ", thamChieuModuleKhac));
    }

    [Fact]
    public void TC33_khong_constructor_hay_field_nao_goi_AppService_module_khac()
    {
        var viPham = new List<string>();

        foreach (var t in Application.GetTypes().Where(t => !t.Name.Contains('<', StringComparison.Ordinal)))
        {
            const BindingFlags co = BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var ctor in t.GetConstructors(co))
            {
                foreach (var p in ctor.GetParameters())
                {
                    GhiNeuAppServiceNgoai(p.ParameterType, $"{t.Name}..ctor({p.Name})", viPham);
                }
            }

            foreach (var f in t.GetFields(co))
            {
                GhiNeuAppServiceNgoai(f.FieldType, $"{t.Name}.{f.Name}", viPham);
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 Module Notification nhận app service của module KHÁC (ADR-09):\n  "
            + string.Join("\n  ", viPham.Distinct()));
    }

    private static void GhiNeuAppServiceNgoai(Type kieu, string viTri, List<string> viPham)
    {
        if (!kieu.Name.EndsWith("AppService", StringComparison.Ordinal))
        {
            return;
        }

        var ns = kieu.Namespace ?? string.Empty;
        if (ns.StartsWith("eCommerce.Notification", StringComparison.Ordinal))
        {
            return;
        }

        viPham.Add($"{viTri} : {kieu.FullName}");
    }
}
