using System.Reflection;

namespace eCommerce.Tests.Notification;

/// <summary>
/// `TC-28` — `BR-NOT-001-020`: `REP-*` **CẤM** query bảng giao dịch của `NOT-001`; báo cáo **phải** đọc
/// **read-model** (`REP-001`/`REP-002`) (`SRS §1 BR-020`, `NFR-NOT-001-009`).
///
/// 🔒 Kiểm **cách ly kiến trúc**: không assembly `eCommerce.Reporting*` nào tham chiếu `eCommerce.Notification.*`,
/// và không kiểu nào của `Reporting` dùng `NotificationQueueItem`/`NotificationQueueEvent`/… làm field/tham số/trả về.
/// Đây là hàng rào **chống tái phát**: nếu ai "tiện tay" JOIN thẳng bảng hàng đợi để làm dashboard, test đỏ ngay
/// (thay vì lỗi chỉ lộ khi bảng phình to và trang báo cáo chậm dần).
///
/// 📌 Panel chi tiết một mục (`GetEventsAsync` → `GET /queue/{id}/events`) **được phép** đọc
/// `NotificationQueueEvents` — nó nằm **trong** module Notification, không phải `REP-*`, nên không nằm trong
/// phạm vi quét ở đây (đúng ngoại lệ của `TC-28`).
/// </summary>
public class NotificationReportingIsolationTests
{
    private static readonly IReadOnlyList<Assembly> ReportingAssemblies = NapReporting();

    private static IReadOnlyList<Assembly> NapReporting()
    {
        var danhSach = new List<Assembly>();
        var daNap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var duongDan in Directory.GetFiles(AppContext.BaseDirectory, "eCommerce.Reporting*.dll"))
        {
            var ten = Path.GetFileNameWithoutExtension(duongDan);
            if (!daNap.Add(ten))
            {
                continue;
            }

            try
            {
                danhSach.Add(Assembly.LoadFrom(duongDan));
            }
            catch (Exception)
            {
                // Không nạp được (thiếu phụ thuộc native) ⇒ bỏ qua; test "phạm vi" bên dưới sẽ lộ nếu thiếu.
            }
        }

        return danhSach;
    }

    [Fact]
    public void Pham_vi_quet_co_assembly_Reporting_that()
    {
        // Chống "test rỗng": nếu không nạp được assembly Reporting nào thì test dưới xanh VÔ NGHĨA.
        var ten = ReportingAssemblies.Select(a => a.GetName().Name).ToList();

        Assert.Contains("eCommerce.Reporting.Application", ten);
        Assert.Contains("eCommerce.Reporting.Domain", ten);
    }

    [Fact]
    public void TC28_khong_assembly_Reporting_nao_tham_chieu_eCommerce_Notification()
    {
        var viPham = new List<string>();

        foreach (var asm in ReportingAssemblies)
        {
            foreach (var refAsm in asm.GetReferencedAssemblies())
            {
                if (refAsm.Name is { } ten && ten.StartsWith("eCommerce.Notification", StringComparison.Ordinal))
                {
                    viPham.Add($"{asm.GetName().Name} -> {ten}");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 Assembly `Reporting` tham chiếu module Notification (BR-020: báo cáo phải đọc read-model):\n  "
            + string.Join("\n  ", viPham.Distinct()));
    }

    [Fact]
    public void TC28_khong_kieu_Reporting_nao_dung_bang_giao_dich_NOT001()
    {
        var viPham = new List<string>();

        foreach (var asm in ReportingAssemblies)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.OfType<Type>().ToArray();
            }

            foreach (var t in types)
            {
                const BindingFlags co = BindingFlags.Public | BindingFlags.NonPublic
                                        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

                foreach (var p in t.GetProperties(co))
                {
                    GhiNeuChamNotification(p.PropertyType, $"{t.Name}.{p.Name}", viPham);
                }

                foreach (var f in t.GetFields(co))
                {
                    GhiNeuChamNotification(f.FieldType, $"{t.Name}.{f.Name}", viPham);
                }

                foreach (var m in t.GetMethods(co))
                {
                    GhiNeuChamNotification(m.ReturnType, $"{t.Name}.{m.Name}(..)->", viPham);
                    foreach (var p in m.GetParameters())
                    {
                        GhiNeuChamNotification(p.ParameterType, $"{t.Name}.{m.Name}({p.Name})", viPham);
                    }
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "🚨 Kiểu `Reporting` dùng bảng giao dịch của NOT-001 (BR-020):\n  "
            + string.Join("\n  ", viPham.Distinct()));
    }

    private static void GhiNeuChamNotification(Type kieu, string viTri, List<string> viPham)
    {
        if (kieu.IsGenericType)
        {
            foreach (var arg in kieu.GetGenericArguments())
            {
                GhiNeuChamNotification(arg, viTri, viPham);
            }

            return;
        }

        if (kieu.Namespace is { } ns && ns.StartsWith("eCommerce.Notification", StringComparison.Ordinal))
        {
            viPham.Add($"{viTri} : {kieu.FullName}");
        }
    }
}
