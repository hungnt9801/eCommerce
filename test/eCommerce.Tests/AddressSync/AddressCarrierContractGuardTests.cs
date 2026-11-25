using System.Reflection;
using eCommerce.Address;
using eCommerce.Address.Carrier;
using eCommerce.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-002`** — bất biến "kiểm bằng máy" (reflection + quét mã nguồn) cho phần contract/arch
/// `D-232`…`D-237`, cùng các BR mà không có lỗi biên dịch nào bắt được nếu ai đó vô tình nới:
///   • **BR-001**: bảng lô **không** có FK/navigation tới ánh xạ (mã hãng là dữ liệu dẫn xuất);
///   • **BR-011/NFR-010**: **0** endpoint/mã hãng hard-code trong mã nguồn `Carrier*` (cấu hình, không hằng);
///   • **BR-010/NFR-008**: worker dùng khoá theo hãng `address:carrier-sync` + **timeout hữu hạn**;
///   • **BR-014**: worker tự mở UoW `requiresNew` + `isTransactional` + `CompleteAsync`;
///   • **BR-003**: worker ghi đúng **một** dòng lô qua `CarrierAddressSyncRun.Ghi` ở cả đường thành công lẫn lỗi;
///   • **BR-016**: **0** trường tiền/`Money`/`decimal` trên entity ADDR-002;
///   • **`D-232`**: hợp đồng hẹp `ICarrierAddressResolver` **đúng 1** method; app service **KHÔNG** kế thừa nó
///     và có **đúng 6** method public (`D-235`); `CarrierAddressCodeResultDto` giữ hình dạng `Mapping`+`Unit`;
///   • **`D-234`**: `MauCanNguoiAnhXa` là `string[]`, mẫu ≤ 20;
///   • **`D-236`**: `UnmappedCodesResultDto` có `SyncRunId`/`LatestRunOutcome` **nullable**; `D-237`: không cột tên cũ.
///
/// 📌 Quét mã nguồn = đọc tệp trong `modules/Address` (khuôn TC-13/TC-17 "test quét mã nguồn"); bù cho việc
/// **chưa** có harness worker trong bộ test (xem `docs/tests/ADDR-002-testplan.md` §nợ).
/// </summary>
public class AddressCarrierContractGuardTests
{
    // ── BR-001: bảng lô không FK/navigation tới ánh xạ ──────────────────────────────────────────

    [Fact]
    public void BR001_bang_lo_KHONG_co_navigation_toi_anh_xa_ma_hang_la_du_lieu_dan_xuat()
    {
        var nav = typeof(CarrierAddressSyncRun)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(CarrierAddressMapping)
                        || typeof(IEnumerable<CarrierAddressMapping>).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(nav);

        // Ánh xạ giữ **một** Guid đơn vị đích của ta, không giữ khoá ngoại trỏ vào mã hãng.
        var khoa = typeof(CarrierAddressMapping).GetProperty(nameof(CarrierAddressMapping.OurAdministrativeUnitId));
        Assert.NotNull(khoa);
        Assert.Equal(typeof(Guid), khoa!.PropertyType);
    }

    // ── BR-011 / NFR-010: không hard-code endpoint / mã hãng ─────────────────────────────────────

    [Fact]
    public void BR011_khong_hard_code_endpoint_hang_trong_ma_nguon_Carrier()
    {
        var viPham = DocCacTepCarrier()
            .Where(t => t.NoiDung.Contains("https://", StringComparison.Ordinal)
                        || t.NoiDung.Contains("online-gateway", StringComparison.Ordinal)
                        || t.NoiDung.Contains("api.ghn", StringComparison.Ordinal)
                        || t.NoiDung.Contains("api.ghtk", StringComparison.Ordinal))
            .Select(t => t.Ten)
            .ToList();

        Assert.True(viPham.Count == 0,
            "Hard-code endpoint/mã hãng (BR-011 phải qua cấu hình): " + string.Join(", ", viPham));
    }

    // ── BR-010 / NFR-008: khoá theo hãng + timeout hữu hạn ──────────────────────────────────────

    [Fact]
    public void BR010_worker_dung_khoa_theo_hang_va_co_timeout_huu_han()
    {
        var worker = DocTepCarrier("CarrierAddressSyncWorker.cs");

        Assert.Contains("address:carrier-sync", worker);          // `TienToKhoa`
        Assert.Contains("TryAcquireAsync", worker);
        Assert.Contains("TimeSpan.FromSeconds(5)", worker);       // timeout hữu hạn, không acquire vô hạn
    }

    // ── BR-014: UoW requiresNew + isTransactional + CompleteAsync ──────────────────────────────

    [Fact]
    public void BR014_worker_mo_UoW_requiresNew_isTransactional_va_CompleteAsync()
    {
        var worker = DocTepCarrier("CarrierAddressSyncWorker.cs");

        Assert.Contains("requiresNew: true", worker);
        Assert.Contains("isTransactional: true", worker);
        Assert.Contains("CompleteAsync", worker);
    }

    // ── BR-003: một dòng lô qua `Ghi` ở cả đường thành công lẫn lỗi ─────────────────────────────

    [Fact]
    public void BR003_worker_ghi_dong_lo_qua_Ghi_o_ca_duong_thanh_cong_va_loi()
    {
        var worker = DocTepCarrier("CarrierAddressSyncWorker.cs");

        // Đường `DaApDung` (trong `DongBoMotHangAsync`) và đường `BiChan`/`Loi` (`GhiLoThatBaiAsync`)
        // cùng dựng dòng lô bằng factory duy nhất `CarrierAddressSyncRun.Ghi`.
        var soLanGoi = worker.Split("CarrierAddressSyncRun.Ghi(").Length - 1;
        Assert.True(soLanGoi >= 2, "Phải có ≥ 2 chỗ gọi `CarrierAddressSyncRun.Ghi` (thành công + thất bại).");

        Assert.Contains("GhiLoThatBaiAsync", worker);
        Assert.Contains("loRepo.InsertAsync", worker);
    }

    // ── BR-016 / TC-24: không có trường tiền ───────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(CarrierAddressMapping))]
    [InlineData(typeof(CarrierAddressSyncRun))]
    [InlineData(typeof(CarrierAddressSyncRunUnmappedCode))]
    public void BR016_khong_co_truong_tien_decimal_hay_Money(Type loai)
    {
        var truongTien = loai
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(decimal)
                        || p.PropertyType.Name.Contains("Money", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();

        Assert.True(truongTien.Count == 0,
            $"{loai.Name} có trường tiền (ADDR-002 không có tiền — BR-016): {string.Join(", ", truongTien)}");
    }

    // ── D-232: hợp đồng hẹp 1 method; app service KHÔNG kế thừa; DTO hình dạng Mapping+Unit ──────

    [Fact]
    public void D232_hop_dong_hep_dung_MOT_method_va_app_service_khong_ke_thua()
    {
        var method = typeof(ICarrierAddressResolver)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .ToList();

        Assert.Single(method);
        Assert.Equal(nameof(ICarrierAddressResolver.ResolveCarrierAddressCodeAsync), method[0].Name);

        // `D-232` ①(4)(5): app service uỷ quyền, KHÔNG hiện thực hợp đồng in-process (tránh ABP sinh POST cho
        // method đọc — bài học `CustomerDirectory.cs:10-23`).
        Assert.False(typeof(ICarrierAddressResolver).IsAssignableFrom(typeof(CarrierAddressMappingAppService)));

        var kq = typeof(CarrierAddressCodeResultDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(2, kq.Count);
        Assert.Contains("Mapping", kq);
        Assert.Contains("Unit", kq);
    }

    // ── D-235: app service đúng 6 method public ────────────────────────────────────────────────

    [Fact]
    public void D235_app_service_co_dung_SAU_method_public()
    {
        var ten = typeof(CarrierAddressMappingAppService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        string[] mongDoi =
        [
            "GetUnmappedCodesAsync",
            "LayChuaXacMinhAsync",
            "PhatHienSaiLechAsync",
            "ResolveCarrierAddressCodeAsync",
            "TraMaHangAsync",
            "XacMinhAsync"
        ];

        Assert.Equal(mongDoi, ten);
    }

    // ── D-234: mẫu ≤ 20, kiểu string[] ────────────────────────────────────────────────────────

    [Fact]
    public void D234_MauCanNguoiAnhXa_la_string_array_va_gioi_han_20()
    {
        Assert.Equal(20, CarrierAddressSyncRun.SoMauToiDa);

        var p = typeof(CarrierAddressSyncRun)
            .GetProperty(nameof(CarrierAddressSyncRun.MauCanNguoiAnhXa));

        Assert.NotNull(p);
        Assert.Equal(typeof(string[]), p!.PropertyType);
    }

    // ── D-236: SyncRunId + LatestRunOutcome nullable ───────────────────────────────────────────

    [Fact]
    public void D236_ket_qua_GetUnmapped_co_SyncRunId_va_LatestRunOutcome_nullable()
    {
        var dto = typeof(UnmappedCodesResultDto);

        Assert.Equal(typeof(Guid?), dto.GetProperty(nameof(UnmappedCodesResultDto.SyncRunId))!.PropertyType);
        Assert.Equal(typeof(CarrierAddressSyncOutcome?),
            dto.GetProperty(nameof(UnmappedCodesResultDto.LatestRunOutcome))!.PropertyType);
    }

    // ── D-237: không lưu tên cũ của mã trên dòng lô ─────────────────────────────────────────────

    [Fact]
    public void D237_bang_lo_KHONG_luu_ten_cu_cua_ma()
    {
        var nghiVan = typeof(CarrierAddressSyncRun)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.Contains("TenCu", StringComparison.Ordinal)
                        || p.Name.Contains("OldName", StringComparison.Ordinal)
                        || p.Name.Contains("TenMoi", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(nghiVan);
    }

    // ── D-252: [Authorize] CẤP METHOD đúng permission Verify + định nghĩa ở Host ───────────────
    //
    // Lỗ thật của lưới cũ (`D-252` ② T8): `D235` chỉ so TÊN 6 method; `TC01` chỉ đòi `[Authorize]` cấp CLASS;
    // `TC02` chỉ kiểm permission *khi* attribute có; `BR008` chỉ kiểm hằng ↔ định nghĩa. Không ca nào bắt
    // việc XOÁ `[Authorize(...)]` cấp method của `XacMinhAsync` ⇒ xác minh ánh xạ (mở khoá vận đơn cho cả
    // tenant) chỉ còn `[Authorize]` TRỐNG của class (mọi user đăng nhập đều xác minh được). Ca này khoá lại.

    [Fact]
    public void D252_XacMinhAsync_co_Authorize_dung_permission_Verify()
    {
        var method = typeof(CarrierAddressMappingAppService)
            .GetMethod(nameof(CarrierAddressMappingAppService.XacMinhAsync));

        Assert.NotNull(method);

        // `inherit: false` ⇒ phải là attribute CẤP METHOD, không mượn `[Authorize]` trống của class.
        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>(inherit: false);

        Assert.NotNull(authorize);
        Assert.Equal(eCommerceConsts.Permissions.Addresses.CarrierMappingVerify, authorize!.Policy);
        Assert.NotEqual(eCommerceConsts.Permissions.Addresses.Manage, authorize.Policy);

        // Permission tương ứng phải có định nghĩa và định nghĩa ở side Host (`D-85`, khuôn BR008/TC18).
        Assert.Equal("eCommerce.Addresses.CarrierMapping.Verify",
            eCommerceConsts.Permissions.Addresses.CarrierMappingVerify);

        var boiCanh = new PermissionDefinitionContext(new ServiceCollection().BuildServiceProvider());
        new eCommercePermissionDefinitionProvider().Define(boiCanh);

        var dinhNghia = boiCanh.Groups.Values
            .SelectMany(g => g.Permissions)
            .SelectMany(LayCaCay)
            .Single(p => p.Name == eCommerceConsts.Permissions.Addresses.CarrierMappingVerify);

        Assert.Equal(MultiTenancySides.Host, dinhNghia.MultiTenancySide);
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

    // ── tiện ích đọc mã nguồn ──────────────────────────────────────────────────────────────────

    private sealed record TepNguon(string Ten, string NoiDung);

    private static string GocRepo()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);

        while (thuMuc is not null)
        {
            if (File.Exists(Path.Combine(thuMuc.FullName, "eCommerce.slnx")))
            {
                return thuMuc.FullName;
            }

            thuMuc = thuMuc.Parent;
        }

        throw new InvalidOperationException(
            "Không tìm thấy gốc repo (eCommerce.slnx) từ " + AppContext.BaseDirectory);
    }

    private static IReadOnlyList<TepNguon> DocCacTepCarrier()
    {
        var goc = Path.Combine(GocRepo(), "modules", "Address");

        return Directory
            .EnumerateFiles(goc, "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Contains("Carrier", StringComparison.Ordinal))
            .Select(f => new TepNguon(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
    }

    private static string DocTepCarrier(string tenTep)
        => DocCacTepCarrier().SingleOrDefault(t => t.Ten == tenTep)?.NoiDung
           ?? throw new InvalidOperationException("Không tìm thấy tệp mã nguồn Carrier: " + tenTep);
}
