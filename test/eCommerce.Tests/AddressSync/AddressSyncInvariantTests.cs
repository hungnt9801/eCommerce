using System.Reflection;
using eCommerce.Address;
using eCommerce.Address.Carrier;
using eCommerce.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization.Permissions;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-002` BR-015 / BR-012 / BR-008** — bất biến "kiểm bằng máy" cho phần W18-2:
///   • mã lỗi mới `0509`/`0510` đúng giá trị, **do module Address sở hữu**, không trùng dải `0001…0009`;
///   • hai bảng lô mới là **sổ APPEND-ONLY**: 0 public setter, 0 method đổi trên entity;
///   • permission xác minh ánh xạ `Addresses.CarrierMapping.Verify` có **cả** hằng số **lẫn** định nghĩa
///     trong provider (`D-85`).
///
/// 📌 Test này là **reflection nhẹ** — không DB, không host. Phần DB/route thuộc W18-4.
/// </summary>
public class AddressSyncInvariantTests
{
    [Fact]
    public void BR015_danh_muc_ma_loi_moi_dung_dai_05xx_do_module_Address_so_huu()
    {
        Assert.Equal("eCommerce:Address:0509", CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe);
        Assert.Equal("eCommerce:Address:0510", CarrierAddressErrorCodes.AnhXaNguocKhongDuyNhat);
    }

    [Fact]
    public void BR015_ma_05xx_KHONG_trung_voi_ma_0001_0009_cua_ADDR_001()
    {
        var maAddr001 = HangSoCua(typeof(AddressErrorCodes));
        var maAddr002 = HangSoCua(typeof(CarrierAddressErrorCodes));

        var trung = maAddr001.Intersect(maAddr002, StringComparer.Ordinal).ToList();

        Assert.True(trung.Count == 0, "Mã lỗi trùng giữa hai lớp: " + string.Join(", ", trung));
        Assert.DoesNotContain("eCommerce:Address:0509", maAddr001);
        Assert.DoesNotContain("eCommerce:Address:0510", maAddr001);
    }

    [Theory]
    [InlineData(typeof(CatalogSyncRun))]
    [InlineData(typeof(CarrierAddressSyncRun))]
    [InlineData(typeof(CarrierAddressSyncRunUnmappedCode))]
    public void BR012_entity_so_APPEND_ONLY_khong_co_public_setter_va_khong_co_method_doi(Type loai)
    {
        var coSetter = loai
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        Assert.True(
            coSetter.Count == 0,
            $"{loai.Name} có public setter (cho phép sửa sổ append-only): {string.Join(", ", coSetter)}");

        var methodDoi = loai
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.True(
            methodDoi.Count == 0,
            $"{loai.Name} có method public đổi được trạng thái (sổ append-only): {string.Join(", ", methodDoi)}");
    }

    [Fact]
    public void BR008_permission_XAC_MINH_ANH_XA_co_hang_so_VA_duoc_dinh_nghia_trong_provider()
    {
        Assert.Equal("eCommerce.Addresses.CarrierMapping.Verify", eCommerceConsts.Permissions.Addresses.CarrierMappingVerify);

        var boiCanh = new PermissionDefinitionContext(new ServiceCollection().BuildServiceProvider());
        new eCommercePermissionDefinitionProvider().Define(boiCanh);

        var daDinhNghia = boiCanh.Groups
            .Values
            .SelectMany(g => g.Permissions)
            .SelectMany(LayCaCay)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(eCommerceConsts.Permissions.Addresses.CarrierMappingVerify, daDinhNghia);
        // Tên cũ `Addresses.Verify` KHÔNG được dùng lại (`D-85`): xác minh ánh xạ ≠ xác minh địa chỉ khách.
        Assert.NotEqual(
            eCommerceConsts.Permissions.Addresses.Verify,
            eCommerceConsts.Permissions.Addresses.CarrierMappingVerify);
    }

    private static IReadOnlyList<string> HangSoCua(Type lop)
        => lop.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

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
}
