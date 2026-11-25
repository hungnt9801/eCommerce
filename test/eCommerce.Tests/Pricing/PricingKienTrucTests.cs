using System.Reflection;
using eCommerce.Pricing;

namespace eCommerce.Tests.Pricing;

/// <summary>
/// 🚨 **TEST KIẾN TRÚC** (`PRC-002` `-arch` §3.1/§6.3 ca 3d) — bất biến *"chính sách chỉ đi vào qua THAM SỐ"*.
///
/// **Vì sao không khẳng định "cả assembly không tham chiếu `Volo.Abp.Settings`":** assembly
/// `eCommerce.Pricing.Domain` **chứa** `PricingSettingDefinitionProvider` (đọc/đăng ký setting) ⇒ khẳng định đó
/// SAI theo thiết kế. Bất biến cần giữ hẹp hơn: **bề mặt CÔNG KHAI của bốn kiểu hàm thuần** không nhận/không lộ
/// `ISettingProvider`/`ISettingManager`.
///
/// 📌 Rủi ro R5: nếu ai đó "tiện tay" gọi `ISettingProvider` **giữa pipeline** thì hai dòng của cùng một hoá đơn
/// có thể dùng hai chính sách (vi phạm ADR-08 — snapshot đọc MỘT lần ở B1). Test này chặn đúng đường đó.
/// </summary>
public class PricingKienTrucTests
{
    private const string ISettingProvider = "Volo.Abp.Settings.ISettingProvider";
    private const string ISettingManager = "Volo.Abp.Settings.ISettingManager";

    private static readonly Type[] KieuHamThuan =
    [
        typeof(TinhTienDon),
        typeof(ChonBacThang),
        typeof(GiaCoSoResolver),
        typeof(PricingChinhSach),
    ];

    [Fact]
    public void Ham_thuan_KHONG_nhan_hoac_lo_ISettingProvider_ISettingManager__3d()
    {
        var viPham = new List<string>();

        foreach (var kieu in KieuHamThuan)
        {
            KiemTraBeMat(kieu, kieu.Name, viPham);
        }

        Assert.True(viPham.Count == 0,
            "Hàm thuần của Pricing KHÔNG được biết kiểu setting (chính sách chỉ đi vào qua THAM SỐ — ADR-08):\n  "
            + string.Join("\n  ", viPham));
    }

    private static void KiemTraBeMat(Type kieu, string duongDan, List<string> viPham)
    {
        const BindingFlags congKhai =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var truong in kieu.GetFields(congKhai))
        {
            KiemTraKieu(truong.FieldType, $"{duongDan}.{truong.Name} (field)", viPham);
        }

        foreach (var thuocTinh in kieu.GetProperties(congKhai))
        {
            KiemTraKieu(thuocTinh.PropertyType, $"{duongDan}.{thuocTinh.Name} (property)", viPham);
        }

        foreach (var phuongThuc in kieu.GetMethods(congKhai))
        {
            KiemTraKieu(phuongThuc.ReturnType, $"{duongDan}.{phuongThuc.Name}() (return)", viPham);

            foreach (var thamSo in phuongThuc.GetParameters())
            {
                KiemTraKieu(thamSo.ParameterType, $"{duongDan}.{phuongThuc.Name}({thamSo.Name})", viPham);
            }
        }

        foreach (var hamDung in kieu.GetConstructors(congKhai))
        {
            foreach (var thamSo in hamDung.GetParameters())
            {
                KiemTraKieu(thamSo.ParameterType, $"{duongDan}..ctor({thamSo.Name})", viPham);
            }
        }

        // Kiểu lồng công khai (vd `ChonBacThang.Bac`) cũng là một phần bề mặt công khai.
        foreach (var kieuLong in kieu.GetNestedTypes(BindingFlags.Public))
        {
            KiemTraBeMat(kieuLong, $"{duongDan}.{kieuLong.Name}", viPham);
        }
    }

    private static void KiemTraKieu(Type? kieu, string viTri, List<string> viPham)
    {
        if (kieu is null)
        {
            return;
        }

        if (kieu.IsByRef)
        {
            KiemTraKieu(kieu.GetElementType(), viTri, viPham);
            return;
        }

        if (kieu.IsArray)
        {
            KiemTraKieu(kieu.GetElementType(), viTri, viPham);
            return;
        }

        if (kieu.FullName is ISettingProvider or ISettingManager)
        {
            viPham.Add($"{viTri} ⇒ {kieu.FullName}");
            return;
        }

        if (kieu.IsGenericType)
        {
            foreach (var thamSoKieu in kieu.GetGenericArguments())
            {
                KiemTraKieu(thamSoKieu, viTri, viPham);
            }
        }
    }
}
