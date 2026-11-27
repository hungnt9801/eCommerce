using System.Reflection;
using System.Text.RegularExpressions;
using eCommerce.Kernel.Events;
using eCommerce.Notification;
using eCommerce.Notification.Events;

namespace eCommerce.Tests.Notification;

/// <summary>
/// Quét **quy ước/bất biến** của module `Notification` bằng reflection (`SRS §14.1`):
/// `TC-12` (sổ event append-only) · `TC-17` (ETO 100 % `CorrelationId`, 0 PII) · `TC-20`/`TC-21` (khoá không
/// tiền tố `ecommerce:`, không đụng khoá ABP) · `TC-22` (0 `double`/`float`) · `TC-40` (0 cột "đã tới"/chi phí/
/// opt-out/quiet-hours/`OrderId`) · `TC-41` (PII tắt) · `TC-44` (một module một nguồn).
/// </summary>
public class NotificationConventionsTests
{
    private static readonly Assembly Domain = typeof(NotificationQueueItem).Assembly;
    private static readonly Assembly Contracts = typeof(INotificationQueueAppender).Assembly;
    private static readonly Assembly Application = typeof(NotificationQueueAppender).Assembly;

    private static IEnumerable<Assembly> CacAssembly => [Domain, Contracts, Application];

    private static IEnumerable<Type> TatCaType(Assembly asm)
    {
        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types.Where(t => t is not null && !t.Name.Contains('<', StringComparison.Ordinal));
    }

    private static IEnumerable<Type> TatCaTypeCuaModule()
        => CacAssembly.SelectMany(TatCaType);

    // ── TC-12: sổ event append-only ──────────────────────────────────────────────────────────

    [Fact]
    public void TC12_NotificationQueueEvent_khong_co_setter_cong_khai_va_khong_co_Update_Delete()
    {
        var t = typeof(NotificationQueueEvent);

        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.True(
                p.SetMethod is null || !p.SetMethod.IsPublic,
                $"`{t.Name}.{p.Name}` có setter CÔNG KHAI ⇒ phá vỡ append-only (BR-006).");
        }

        var phuongThucSua = t
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("Update", StringComparison.Ordinal)
                        || m.Name.StartsWith("Delete", StringComparison.Ordinal))
            .ToList();

        Assert.True(phuongThucSua.Count == 0,
            "Sổ `NotificationQueueEvents` lộ đường sửa/xoá: "
            + string.Join(", ", phuongThucSua.Select(m => m.Name)));
    }

    // ── TC-17: ETO 100% CorrelationId + 0 PII ────────────────────────────────────────────────

    [Fact]
    public void TC17_moi_ETO_deu_co_CorrelationId_va_khong_mang_PII()
    {
        var etos = TatCaType(Contracts)
            .Where(t => typeof(INotificationEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        Assert.Equal(4, etos.Count); // Enqueued · HandedOff · QueueFailed · BatchApproved

        var tuCam = new[] { "Recipient", "Email", "Phone", "PhoneNumber", "Plaintext", "Address" };

        foreach (var eto in etos)
        {
            var corr = eto.GetProperty("CorrelationId");
            Assert.True(corr is not null, $"ETO `{eto.Name}` thiếu `CorrelationId` (XC-005 BR-003).");
            Assert.Equal(typeof(string), corr!.PropertyType);

            foreach (var p in eto.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Assert.DoesNotContain(
                    tuCam,
                    cam => p.Name.Contains(cam, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    // ── TC-20 / TC-21: quy ước tên khoá + không trùng khoá ABP ────────────────────────────────

    [Fact]
    public void TC20_TC21_khoa_phan_tan_khong_tien_to_ecommerce_va_khong_dung_khoa_ABP()
    {
        var tenantId = Guid.NewGuid();
        var khoa = new[]
        {
            NotificationLockKeys.QueueDispatch(tenantId),
            NotificationLockKeys.Template(Guid.NewGuid()),
            NotificationLockKeys.Batch(Guid.NewGuid()),
            NotificationLockKeys.QueueReplay(tenantId)
        };

        Assert.All(khoa, k => Assert.StartsWith("notification:", k, StringComparison.Ordinal));
        Assert.All(khoa, k => Assert.DoesNotContain("ecommerce:", k, StringComparison.OrdinalIgnoreCase));
        Assert.All(khoa, k => Assert.DoesNotContain("Abp", k, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(khoa.Length, khoa.Distinct(StringComparer.Ordinal).Count());

        // Mẫu chính xác + nhánh host (`tenantId = null`).
        Assert.Equal($"notification:queue-dispatch:{tenantId}", NotificationLockKeys.QueueDispatch(tenantId));
        Assert.Equal($"notification:queue-replay:{tenantId}", NotificationLockKeys.QueueReplay(tenantId));
        Assert.Equal("notification:queue-dispatch:host", NotificationLockKeys.QueueDispatch(null));
        Assert.Equal("notification:queue-replay:host", NotificationLockKeys.QueueReplay(null));
    }

    // ── TC-22: không double/float trong biểu thức tiền ───────────────────────────────────────

    private static bool LaSoThuc(Type t)
    {
        if (t == typeof(double) || t == typeof(float))
        {
            return true;
        }

        if (t.IsArray)
        {
            return LaSoThuc(t.GetElementType()!);
        }

        return t.IsGenericType && t.GetGenericArguments().Any(LaSoThuc);
    }

    [Fact]
    public void TC22_khong_co_double_float_trong_chu_ky_thanh_vien_module()
    {
        var viPham = new List<string>();

        foreach (var t in TatCaTypeCuaModule())
        {
            const BindingFlags co = BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var f in t.GetFields(co))
            {
                if (LaSoThuc(f.FieldType))
                {
                    viPham.Add($"{t.Name}.{f.Name} : {f.FieldType.Name}");
                }
            }

            foreach (var p in t.GetProperties(co))
            {
                if (LaSoThuc(p.PropertyType))
                {
                    viPham.Add($"{t.Name}.{p.Name} : {p.PropertyType.Name}");
                }
            }

            foreach (var m in t.GetMethods(co))
            {
                if (LaSoThuc(m.ReturnType) || m.GetParameters().Any(x => LaSoThuc(x.ParameterType)))
                {
                    viPham.Add($"{t.Name}.{m.Name}(...) : {m.ReturnType.Name}");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "Module Notification dùng `double`/`float` cho tiền/lưu lượng (XC-001 BR-008):\n  "
            + string.Join("\n  ", viPham.Distinct()));
    }

    // ── TC-40: không sở hữu "đã tới"/chi phí/opt-out/quiet-hours/OrderId ─────────────────────

    [Fact]
    public void TC40_khong_co_cot_da_toi_chi_phi_opt_out_quiet_hours_OrderId()
    {
        var cam = new HashSet<string>(StringComparer.Ordinal)
        {
            "DeliveredAt", "DeliveredAtUtc", "Cost", "CostAmount",
            "OptOut", "QuietHours", "OrderId", "OrderLineId"
        };

        var viPham = new List<string>();

        foreach (var t in TatCaTypeCuaModule())
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (cam.Contains(p.Name))
                {
                    viPham.Add($"{t.Name}.{p.Name}");
                }
            }
        }

        Assert.True(viPham.Count == 0,
            "NOT-001 sở hữu cột thuộc NOT-002 (D-193/BR-022):\n  " + string.Join("\n  ", viPham));
    }

    // ── TC-41: PII mặc định TẮT ───────────────────────────────────────────────────────────────

    [Fact]
    public void TC41_PII_tat_chi_luu_ma_hoa_va_bam_khong_luu_plaintext()
    {
        var muc = typeof(NotificationQueueItem);
        Assert.NotNull(muc.GetProperty("RecipientEncrypted"));
        Assert.NotNull(muc.GetProperty("RecipientHash"));
        Assert.Null(muc.GetProperty("Recipient")); // cấm cột người nhận dạng rõ

        // DTO hàng đợi chỉ lộ bản CHE.
        var dto = typeof(NotificationQueueItemDto);
        Assert.NotNull(dto.GetProperty("RecipientMasked"));
        Assert.Null(dto.GetProperty("Recipient"));
        Assert.Null(dto.GetProperty("RecipientEncrypted"));

        // Không type tầng Domain nào có thuộc tính `Recipient` plaintext.
        var coRecipient = TatCaType(Domain)
            .Where(t => t.GetProperty("Recipient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null)
            .Select(t => t.Name)
            .ToList();

        Assert.True(coRecipient.Count == 0,
            "Có thuộc tính `Recipient` plaintext trong Domain (D-191): " + string.Join(", ", coRecipient));
    }

    // ── TC-44: một module, một nguồn ─────────────────────────────────────────────────────────

    [Fact]
    public void TC44_mot_ChannelKind_mot_NotificationErrorCodes_dai_0010_0020_va_ma_moi_0002_0003_0006()
    {
        var tatCa = TatCaTypeCuaModule().ToList();

        // 1) Một enum kênh gửi tên `ChannelKind`, KHÔNG có `NotificationChannelKind`.
        var channel = tatCa.Where(t => t.IsEnum && t.Name == "ChannelKind").ToList();
        Assert.Single(channel);
        Assert.DoesNotContain(tatCa, t => t.Name == "NotificationChannelKind");

        Assert.Equal(
            new[] { ("Email", 0), ("Sms", 1), ("ZaloOa", 2) },
            Enum.GetValues<ChannelKind>().Select(v => (v.ToString(), (int)v)).ToArray());

        // 2) Một lớp mã lỗi: 11 mã NOT-001 (`0010–0020`) + 3 mã NOT-002 mới (`0002`/`0003`/`0006`).
        var lopMa = tatCa.Where(t => t.Name == "NotificationErrorCodes").ToList();
        Assert.Single(lopMa);

        var ma = typeof(NotificationErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(14, ma.Count);
        Assert.Equal(ma.Count, ma.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ma, m => Assert.Matches(new Regex(@"^eCommerce:Notification:\d{4}$"), m));

        var so = ma.Select(m => int.Parse(m[^4..], System.Globalization.CultureInfo.InvariantCulture))
            .OrderBy(x => x)
            .ToArray();

        // Dải NOT-001 vẫn ĐỦ 11 mã `0010–0020` (không được đánh số lại / rút bớt).
        var daiCu = so.Where(x => x is >= 10 and <= 20).ToArray();
        Assert.Equal(Enumerable.Range(10, 11).ToArray(), daiCu);

        // 3 mã mới của NOT-002 dùng dải `0001–0009` (không chiếm `0010+`).
        var maMoi = so.Where(x => x is 2 or 3 or 6).ToArray();
        Assert.Equal(new[] { 2, 3, 6 }, maMoi);
        Assert.Equal(14, daiCu.Length + maMoi.Length);

        Assert.Equal("eCommerce:Notification:", NotificationConsts.ErrorCodePrefix);
    }
}
