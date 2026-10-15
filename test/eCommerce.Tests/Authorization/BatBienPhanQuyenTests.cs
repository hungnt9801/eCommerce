using System.Reflection;
using eCommerce.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization.Permissions;

namespace eCommerce.Tests.Authorization;

/// <summary>
/// 🚨 **Test bất biến phân quyền** — `XC-004` **TC-01** và **TC-02**, cộng thêm 2 bất biến phụ.
///
/// Đây là 2 lỗi **ĐÃ XẢY RA THẬT** ở Phase 0, **không có lỗi biên dịch** và **không exception**:
///   1. một app service **chạy được khi chưa xác thực** vì thiếu `[Authorize]` cấp class;
///   2. API trả **`403` cho MỌI người** vì permission **chưa được định nghĩa**.
/// Tài liệu `XC-004` chốt: hai lỗi này **phải** bị CI chặn, không chờ review bằng mắt.
///
/// 📌 **Vì sao phải NẠP assembly từ đĩa thay vì quét `AppDomain`:** quét "assembly **đã nạp**" rất dễ
/// **thiếu** — đúng bài học **C16** (bộ kiểm tra cho ra kết quả *"sạch" giả tạo* trong khi có assembly
/// chưa từng được nạp). Ở đây phạm vi quét được **nạp tường minh từ thư mục build** và **đếm lại được**.
/// </summary>
public class BatBienPhanQuyenTests
{
    /// <summary>Các assembly có thể chứa app service — nạp từ thư mục build, KHÔNG dựa vào "đã nạp".</summary>
    private static readonly IReadOnlyList<Assembly> CacAssembly = NapAssemblyHeThong();

    private static IReadOnlyList<Assembly> NapAssemblyHeThong()
    {
        var daNap = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        var danhSach = new List<Assembly>();

        foreach (var duongDan in Directory.GetFiles(AppContext.BaseDirectory, "eCommerce*.dll"))
        {
            var ten = Path.GetFileNameWithoutExtension(duongDan);

            try
            {
                var asm = Assembly.LoadFrom(duongDan);
                if (daNap.TryAdd(asm.GetName().Name ?? ten, asm))
                {
                    danhSach.Add(asm);
                }
            }
            catch (Exception)
            {
                // Không nạp được (thiếu phụ thuộc native, ...) ⇒ bỏ qua NHƯNG test dưới sẽ đối chiếu
                // danh sách assembly bắt buộc, nên việc thiếu sẽ lộ ra thay vì im lặng.
            }
        }

        return danhSach;
    }

    private static IEnumerable<Type> CacAppService()
        => CacAssembly
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t is not null).Select(t => t!);
                }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.Name.EndsWith("AppService", StringComparison.Ordinal));

    /// <summary>Tên permission được dùng trong `[Authorize(...)]` (cả cấp class lẫn cấp method).</summary>
    private static IEnumerable<string> PermissionDungTrongCode()
        => CacAppService()
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true))))
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    /// <summary>Mọi permission **được ĐỊNH NGHĨA** — đọc bằng cách **chạy thật** `PermissionDefinitionProvider`.</summary>
    private static IReadOnlyList<string> PermissionDaDinhNghia()
    {
        var boiCanh = new PermissionDefinitionContext(new ServiceCollection().BuildServiceProvider());

        // Chạy đúng provider của sản phẩm — không chép tay danh sách (chép tay thì test sẽ "đúng" mãi
        // kể cả khi provider bị sửa sai).
        new eCommercePermissionDefinitionProvider().Define(boiCanh);

        // ⚠️ `PermissionGroupDefinition.Permissions` CHỈ trả permission CẤP CAO NHẤT; permission con nằm ở
        // `PermissionDefinition.Children`. Bỏ sót bước đệ quy ⇒ test báo "mồ côi" cho MỌI permission con
        // (dương tính giả) — đã gặp thật khi viết test này, nên phải đệ quy.
        return boiCanh.Groups
            .Values
            .SelectMany(g => g.Permissions)
            .SelectMany(LayCaCay)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Duyệt permission và TOÀN BỘ permission con (cây).</summary>
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

    /// <summary>Mọi hằng số permission khai trong `eCommerceConsts.Permissions`.</summary>
    private static IReadOnlyList<string> HangSoPermission()
        => typeof(eCommerceConsts.Permissions)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void Pham_vi_quet_phai_DU_va_khong_duoc_thu_hep_im_lang()
    {
        // 🚨 Chống bài học C16: nếu phạm vi quét bị thu hẹp (assembly không nạp được, đổi cách lọc...)
        // thì các test dưới sẽ "xanh" một cách VÔ NGHĨA. Bất biến: các assembly có app service PHẢI có mặt.
        var tenDaQuet = CacAssembly.Select(a => a.GetName().Name).Where(n => n is not null).ToHashSet(StringComparer.Ordinal);

        string[] batBuoc =
        [
            "eCommerce.Application",
            "eCommerce.ChannelHub.Application",
            "eCommerce.EInvoice.Application"
        ];

        var thieu = batBuoc.Where(t => !tenDaQuet.Contains(t)).ToList();

        Assert.True(
            thieu.Count == 0,
            "Phạm vi quét THIẾU assembly có app service (test sẽ xanh vô nghĩa):\n  " + string.Join("\n  ", thieu) +
            $"\nĐã quét {tenDaQuet.Count} assembly: {string.Join(", ", tenDaQuet.OrderBy(x => x, StringComparer.Ordinal))}");

        Assert.True(CacAppService().Any(), "Không tìm thấy app service nào ⇒ phép quét hỏng.");
    }

    [Fact]
    public void TC01_moi_app_service_phai_co_Authorize_o_cap_CLASS()
    {
        // 🚨 Lỗi Phase 0 thật: chỉ đặt [Authorize] ở vài method ⇒ các method còn lại thành API CÔNG KHAI.
        // Đã quan sát được đúng như vậy với `GetShopsAsync`.
        var thieu = CacAppService()
            .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            thieu.Count == 0,
            "🚨 App service THIẾU [Authorize] ở cấp CLASS ⇒ method không gắn gì sẽ trở thành API CÔNG KHAI:\n  "
            + string.Join("\n  ", thieu));
    }

    [Fact]
    public void TC02_moi_permission_dung_trong_Authorize_phai_DUOC_DINH_NGHIA()
    {
        // 🚨 Lỗi Phase 0 thật: permission chưa định nghĩa ⇒ ABP coi là LUÔN bị từ chối ⇒ 403 cho MỌI người,
        // KỂ CẢ admin. Đây là lỗi không thể đoán ra nếu không có test.
        var daDinhNghia = PermissionDaDinhNghia().ToHashSet(StringComparer.Ordinal);
        var dungTrongCode = PermissionDungTrongCode();

        var moCoi = dungTrongCode.Where(p => !daDinhNghia.Contains(p)).ToList();

        Assert.True(
            moCoi.Count == 0,
            "🚨 Permission dùng trong [Authorize] nhưng CHƯA ĐƯỢC ĐỊNH NGHĨA (⇒ 403 cho MỌI người):\n  "
            + string.Join("\n  ", moCoi));
    }

    [Fact]
    public void TC02b_chieu_NGUOC_lai_hang_so_va_dinh_nghia_phai_KHOP_NHAU()
    {
        // Bất biến 2 CHIỀU (XC-004 §14.1 TC-02): hằng số "mồ côi" (khai mà không định nghĩa) cũng là lỗi —
        // nó khiến người sau tưởng permission đã tồn tại và gõ `[Authorize(HangSoDo)]` ⇒ 403.
        var daDinhNghia = PermissionDaDinhNghia();
        var hangSo = HangSoPermission();

        var hangSoKhongDinhNghia = hangSo.Except(daDinhNghia, StringComparer.Ordinal).ToList();
        var dinhNghiaKhongHangSo = daDinhNghia.Except(hangSo, StringComparer.Ordinal).ToList();

        Assert.True(
            hangSoKhongDinhNghia.Count == 0,
            "🚨 Hằng số permission KHAI mà KHÔNG được định nghĩa trong provider:\n  "
            + string.Join("\n  ", hangSoKhongDinhNghia));

        Assert.True(
            dinhNghiaKhongHangSo.Count == 0,
            "🚨 Permission ĐƯỢC ĐỊNH NGHĨA nhưng không có hằng số trong eCommerceConsts.Permissions " +
            "(dễ dẫn tới magic string):\n  " + string.Join("\n  ", dinhNghiaKhongHangSo));

        Assert.True(hangSo.Count > 0, "Không đọc được hằng số permission nào ⇒ phép quét hỏng.");
    }

    [Fact]
    public void Moi_permission_phai_nam_trong_dung_group_va_dung_tien_to()
    {
        // Quy ước tên (XC-004 D9/§): permission thuộc group `eCommerce` và bắt đầu bằng `eCommerce.`.
        // Sai tiền tố ⇒ không lộ ra ở đâu cả cho tới khi có người gõ đúng chuỗi mong đợi và nhận 403.
        var sai = PermissionDaDinhNghia()
            .Where(p => !p.StartsWith(eCommerceConsts.Permissions.GroupName + ".", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            sai.Count == 0,
            $"🚨 Permission KHÔNG đúng tiền tố '{eCommerceConsts.Permissions.GroupName}.':\n  "
            + string.Join("\n  ", sai));
    }
}
