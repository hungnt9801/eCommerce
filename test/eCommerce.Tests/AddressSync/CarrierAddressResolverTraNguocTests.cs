using eCommerce.Address;
using eCommerce.Address.Carrier;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-002` BR-009** — tra **NGƯỢC** (đơn vị của ta → mã địa chỉ hãng) phải **FAIL-CLOSED**
/// (`D-89`): không trả `null`, **không** chọn bừa "cái đầu/mới nhất". Sai ở đây là gửi hàng tới địa chỉ
/// không xác định — không thu hồi được và không có cảnh báo.
///
/// Luật kiểm: `0505` tham số rỗng · `0501` không thấy đích/không có ứng viên · `0504` đích đã đóng ·
/// `0502` chỉ có `ChuaXacMinh`/`NghiNgo` · `0510` nhiều hơn một `DaXacMinh`.
/// </summary>
public class CarrierAddressResolverTraNguocTests
{
    private const string Hang = "GHN";
    private static readonly DateTime TaiLuc = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);

    private static readonly Guid DichId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DichKhacId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AdministrativeUnit DonVi(Guid id, bool hoatDong = true)
    {
        var dv = AdministrativeUnit.TaoMoi(
            AdministrativeSchemaVersion.V2,
            AdministrativeLevel.Xa,
            "00001",
            "Phường A",
            parentCode: "01",
            id: id);

        if (!hoatDong)
        {
            dv.Dong("Sáp nhập để test");
        }

        return dv;
    }

    private static CarrierAddressMapping AnhXa(
        string ma,
        Guid dichId,
        CarrierMappingStatus trangThai = CarrierMappingStatus.DaXacMinh,
        string hang = Hang)
    {
        var m = CarrierAddressMapping.TaoMoi(
            hang,
            "PROVINCE",
            ma,
            $"Tên {ma}",
            dichId,
            trangThai == CarrierMappingStatus.DaXacMinh ? CarrierMatchMethod.ThuCong : CarrierMatchMethod.TheoTen,
            "api/ghn",
            TaiLuc);

        if (trangThai == CarrierMappingStatus.NghiNgo)
        {
            m.DanhDauNghiNgo();
        }

        return m;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR009_hang_rong_thi_0505(string? hang)
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc([], [DonVi(DichId)], hang!, DichId));

        Assert.Equal(CarrierAddressErrorCodes.ThieuThongTinBatBuoc, ex.Code);
    }

    [Fact]
    public void BR009_don_vi_dich_Guid_Empty_thi_0505()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc([], [DonVi(DichId)], Hang, Guid.Empty));

        Assert.Equal(CarrierAddressErrorCodes.ThieuThongTinBatBuoc, ex.Code);
    }

    [Fact]
    public void BR009_dich_KHONG_ton_tai_thi_0501()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc([], [DonVi(DichKhacId)], Hang, DichId));

        Assert.Equal(CarrierAddressErrorCodes.KhongTimThayMaHang, ex.Code);
    }

    [Fact]
    public void BR009_dich_da_DONG_thi_0504_khong_tra_ma_hang()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc(
                [AnhXa("P-1", DichId)], [DonVi(DichId, hoatDong: false)], Hang, DichId));

        Assert.Equal(CarrierAddressErrorCodes.DonViDichDaDong, ex.Code);
    }

    [Fact]
    public void BR009_KHONG_co_ung_vien_nao_cung_hang_va_dich_thi_0501()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc(
                [AnhXa("P-1", DichKhacId)], [DonVi(DichId), DonVi(DichKhacId)], Hang, DichId));

        Assert.Equal(CarrierAddressErrorCodes.KhongTimThayMaHang, ex.Code);
        Assert.Equal(0, (int)ex.Data["soAnhXa"]!);
    }

    [Fact]
    public void BR009_chi_co_ChuaXacMinh_va_NghiNgo_thi_0502()
    {
        var ungVien = new[]
        {
            AnhXa("P-1", DichId, CarrierMappingStatus.ChuaXacMinh),
            AnhXa("P-2", DichId, CarrierMappingStatus.NghiNgo)
        };

        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc(ungVien, [DonVi(DichId)], Hang, DichId));

        Assert.Equal(CarrierAddressErrorCodes.MaChuaXacMinh, ex.Code);
        Assert.Equal(2, (int)ex.Data["soAnhXa"]!);
        Assert.Contains("ChuaXacMinh", (string)ex.Data["trangThai"]!, StringComparison.Ordinal);
        Assert.Contains("NghiNgo", (string)ex.Data["trangThai"]!, StringComparison.Ordinal);
    }

    [Fact]
    public void BR009_HAI_anh_xa_DaXacMinh_cung_dich_thi_0510_khong_chon_bua()
    {
        var ungVien = new[]
        {
            AnhXa("P-2", DichId),
            AnhXa("P-1", DichId)
        };

        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc(ungVien, [DonVi(DichId)], Hang, DichId));

        Assert.Equal(CarrierAddressErrorCodes.AnhXaNguocKhongDuyNhat, ex.Code);
        Assert.Equal(2, (int)ex.Data["soAnhXa"]!);
        // Danh sách mã phải sắp `Ordinal` để thông điệp ổn định.
        Assert.Equal(["P-1", "P-2"], ((IEnumerable<string>)ex.Data["carrierAddressCode"]!).ToList());
    }

    [Fact]
    public void BR009_MOT_DaXacMinh_va_MOT_NghiNgo_thi_tra_DaXacMinh()
    {
        var ungVien = new[]
        {
            AnhXa("P-1", DichId, CarrierMappingStatus.NghiNgo),
            AnhXa("P-2", DichId)
        };

        var ketQua = CarrierAddressMappingResolver.TraNguoc(ungVien, [DonVi(DichId)], Hang, DichId);

        Assert.Equal("P-2", ketQua.AnhXa.CarrierAddressCode);
        Assert.Equal(DichId, ketQua.DonVi.Id);
        Assert.Equal(CarrierMappingStatus.DaXacMinh, ketQua.AnhXa.Status);
    }

    [Fact]
    public void BR009_anh_xa_cua_HANG_KHAC_cung_dich_KHONG_duoc_tinh()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingResolver.TraNguoc(
                [AnhXa("P-1", DichId, hang: "GHTK")], [DonVi(DichId)], Hang, DichId));

        Assert.Equal(CarrierAddressErrorCodes.KhongTimThayMaHang, ex.Code);
    }

    [Fact]
    public void BR009_ma_hang_KHONG_phan_biet_hoa_thuong()
    {
        var ketQua = CarrierAddressMappingResolver.TraNguoc(
            [AnhXa("P-1", DichId)], [DonVi(DichId)], "ghn", DichId);

        Assert.Equal("P-1", ketQua.AnhXa.CarrierAddressCode);
    }
}
