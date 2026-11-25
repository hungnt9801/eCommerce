using eCommerce.Address.Carrier;
using eCommerce.Address.Sync;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-002` BR-013 / BR-005 / BR-004** — vòng quét chuẩn hoá mã địa chỉ hãng (`D-84`/`D-91`),
/// hàng rào 3 chỉ đếm **chuyển MỚI** sang `NghiNgo` (`D-87`) và băm danh mục đã chuẩn hoá (`D-83`).
///
/// 🎯 Vì sao quan trọng: mã hãng là thứ **gửi trong payload tạo vận đơn**. Đổi hoa–thường/bỏ số 0 đầu là
/// gửi sai địa chỉ. Hàng rào 3 đếm lặp sẽ khiến một lượt đồng bộ hỏng **vĩnh viễn** bị `0508` (không bao
/// giờ áp được danh mục đúng nữa) — hoặc ngược lại, đếm thiếu sẽ âm thầm vô hiệu hoá toàn bộ ánh xạ.
/// </summary>
public class CarrierAddressMappingSourceRulesTests
{
    private const string Hang = "GHN";
    private static readonly DateTime TaiLuc = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);

    private static DonViDiaChiHang DonVi(string? ma, string? ten = "Tên", string? cap = "PROVINCE")
        => new(Hang, cap!, ma!, ten!);

    private static DanhMucDiaChiHang DanhMuc(params DonViDiaChiHang[] rows)
        => new(Hang, rows, "api/ghn", TaiLuc);

    private static CarrierAddressMapping AnhXa(
        string ma,
        string ten,
        CarrierMappingStatus trangThai = CarrierMappingStatus.DaXacMinh,
        string hang = Hang)
    {
        var m = CarrierAddressMapping.TaoMoi(
            hang,
            "PROVINCE",
            ma,
            ten,
            Guid.NewGuid(),
            trangThai == CarrierMappingStatus.DaXacMinh ? CarrierMatchMethod.ThuCong : CarrierMatchMethod.TheoTen,
            "api/ghn",
            TaiLuc);

        if (trangThai == CarrierMappingStatus.NghiNgo)
        {
            m.DanhDauNghiNgo();
        }

        return m;
    }

    private static KeHoachDongBoMaHang Lap(
        IReadOnlyList<CarrierAddressMapping> hienCo,
        DanhMucDiaChiHang danhMuc,
        double nguong = 1.0)
        => CarrierAddressMappingSynchronizer.LapKeHoach(Hang, hienCo, danhMuc, nguong, soDonViToiThieu: 2);

    // ── BR-013: chỉ Trim, giữ NGUYÊN phần lõi mã hãng ─────────────────────────────────────────────

    [Theory]
    [InlineData("0012Ab")]     // HOA/thường lẫn lộn
    [InlineData("0P-01")]      // số 0 đầu
    [InlineData("A b")]        // khoảng trắng GIỮA
    [InlineData("0000123")]    // toàn số có 0 đầu
    [InlineData("aBc-123")]    // chữ thường
    public void BR013_ChuanHoaVaKiemMa_CHI_Trim_khong_doi_hoa_thuong_so_0_hay_khoang_trang_giua(string ma)
    {
        var sach = CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(DanhMuc(DonVi(ma, "  Tên có khoảng trắng  ")));

        var dv = Assert.Single(sach);
        Assert.Equal(ma, dv.CarrierAddressCode);
        // `D-84` chỉ áp cho MÃ: tên/cấp/hãng giữ nguyên (không trim, không đổi hoa–thường).
        Assert.Equal("  Tên có khoảng trắng  ", dv.CarrierAddressName);
        Assert.Equal("PROVINCE", dv.CarrierLevel);
        Assert.Equal(Hang, dv.CarrierCode);
    }

    [Fact]
    public void BR013_ma_co_khoang_trang_hai_dau_thi_TRA_VE_da_Trim()
    {
        var sach = CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(DanhMuc(DonVi("  P1  ")));

        Assert.Equal("P1", Assert.Single(sach).CarrierAddressCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR013_ma_rong_hoac_null_thi_MOT_0509_khong_NRE_khong_0505(string? ma)
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(DanhMuc(DonVi(ma), DonVi("P1"))));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe, ex.Code);
        Assert.Equal(1, (int)ex.Data["soMaRong"]!);
        Assert.Equal(0, (int)ex.Data["soMaTrung"]!);
    }

    [Fact]
    public void BR013_phan_tu_null_trong_danh_muc_thi_0509_khong_NRE()
    {
        var danhMuc = DanhMuc(DonVi("P1"), null!, DonVi("P2"));

        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(danhMuc));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe, ex.Code);
        Assert.Equal(1, (int)ex.Data["soMaRong"]!);
    }

    [Fact]
    public void BR013_hai_ma_TRUNG_sau_Trim_thi_MOT_0509_soMaTrung_bang_SO_DON_VI_trung()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(DanhMuc(DonVi(" P1 "), DonVi("P1"))));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe, ex.Code);
        // `soMaTrung` = số ĐƠN VỊ nằm trong nhóm trùng (không phải số nhóm).
        Assert.Equal(2, (int)ex.Data["soMaTrung"]!);
        Assert.Contains("P1", (IEnumerable<string>)ex.Data["maTrung"]!);
    }

    [Fact]
    public void BR013_danh_muc_TOAN_rong_ma_thi_0509_chu_KHONG_phai_0506()
    {
        // Hàng rào 0b (0509) chạy TRƯỚC hàng rào 1 (0506) — lỗi phải nói "mã không hợp lệ",
        // không phải "danh mục rỗng".
        var ex = Assert.Throws<BusinessException>(
            () => Lap([], DanhMuc(DonVi(null), DonVi("   "))));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe, ex.Code);
        Assert.Equal(2, (int)ex.Data["soMaRong"]!);
    }

    [Fact]
    public void BR013_vua_rong_vua_trung_thi_MOT_exception_mang_CA_HAI_so_dem()
    {
        var ex = Assert.Throws<BusinessException>(
            () => CarrierAddressMappingSynchronizer.ChuanHoaVaKiemMa(
                DanhMuc(DonVi(null), DonVi("P1"), DonVi(" P1 "))));

        Assert.Equal(CarrierAddressErrorCodes.DanhMucHangMaKhongHopLe, ex.Code);
        Assert.Equal(1, (int)ex.Data["soMaRong"]!);
        Assert.Equal(2, (int)ex.Data["soMaTrung"]!);
    }

    // ── BR-005 + D-87: hàng rào 3 chỉ đếm CHUYỂN MỚI sang NghiNgo ─────────────────────────────────

    [Fact]
    public void BR005_hang_rao_3_chi_dem_CHUYEN_MOI_sang_NghiNgo_qua_tung_luot()
    {
        var hienCo = Enumerable.Range(1, 100)
            .Select(i => AnhXa($"P-{i:D3}", $"Tên {i}"))
            .ToList();

        // Lượt 1: nguồn thiếu 15/100 ⇒ 15% ≤ 20% ⇒ ÁP (15 NghiNgo).
        var luot1 = Lap(hienCo, DanhMuc(Enumerable.Range(1, 85)
            .Select(i => DonVi($"P-{i:D3}", $"Tên {i}")).ToArray()), nguong: 0.20);

        Assert.Equal(15, luot1.SoDanhDauNghiNgo);
        CarrierAddressMappingSynchronizer.ApDung(luot1, TaiLuc);

        // Lượt 2: thiếu THÊM 6 ⇒ 6/85 (KHÔNG phải 21/100) ⇒ không ném, 6 việc mới.
        var luot2 = Lap(hienCo, DanhMuc(Enumerable.Range(1, 79)
            .Select(i => DonVi($"P-{i:D3}", $"Tên {i}")).ToArray()), nguong: 0.20);

        Assert.Equal(6, luot2.SoDanhDauNghiNgo);
        CarrierAddressMappingSynchronizer.ApDung(luot2, TaiLuc);

        // Lượt 3: cùng danh mục ⇒ 0 việc (không đếm lặp, không 0508 vĩnh viễn).
        var luot3 = Lap(hienCo, DanhMuc(Enumerable.Range(1, 79)
            .Select(i => DonVi($"P-{i:D3}", $"Tên {i}")).ToArray()), nguong: 0.20);

        Assert.Equal(0, luot3.SoDanhDauNghiNgo);
    }

    [Fact]
    public void BR005_hang_rao_3_dung_MAU_SO_la_so_anh_xa_CHUA_NghiNgo()
    {
        // 10 ánh xạ / 8 đã NghiNgo / 2 biến mất ⇒ 2/2 = 100% > 20% ⇒ 0508 (không phải 2/10 = 20%).
        var hienCo = new List<CarrierAddressMapping>();

        for (var i = 1; i <= 8; i++)
        {
            hienCo.Add(AnhXa($"P-{i:D3}", $"Tên {i}", CarrierMappingStatus.NghiNgo));
        }

        hienCo.Add(AnhXa("P-009", "Tên 9"));
        hienCo.Add(AnhXa("P-010", "Tên 10"));

        var nguon = DanhMuc(Enumerable.Range(1, 8)
            .Select(i => DonVi($"P-{i:D3}", $"Tên {i}")).ToArray());

        var ex = Assert.Throws<BusinessException>(() => Lap(hienCo, nguon, nguong: 0.20));

        Assert.Equal(CarrierAddressErrorCodes.BienMatHangLoat, ex.Code);
        Assert.Equal(2, (int)ex.Data["soNghiNgo"]!);
        Assert.Equal(2, (int)ex.Data["tongDangDung"]!);
        Assert.Equal(8, (int)ex.Data["soDaNghiNgoTruoc"]!);
    }

    [Fact]
    public void BR005_doi_ten_anh_xa_DA_NghiNgo_van_sinh_CapNhatTen_nhung_KHONG_vao_tu_so()
    {
        var nghiNgo = AnhXa("P-001", "Tên cũ", CarrierMappingStatus.NghiNgo);
        var hienCo = new List<CarrierAddressMapping> { nghiNgo };

        for (var i = 2; i <= 5; i++)
        {
            hienCo.Add(AnhXa($"P-{i:D3}", $"Tên {i}"));
        }

        var nguon = DanhMuc(new[]
        {
            DonVi("P-001", "Tên mới"),
            DonVi("P-002", "Tên 2"),
            DonVi("P-003", "Tên 3"),
            DonVi("P-004", "Tên 4"),
            DonVi("P-005", "Tên 5")
        });

        var keHoach = Lap(hienCo, nguon, nguong: 0.20);

        // Vẫn sinh việc cập nhật tên cho người xác minh thấy tên mới...
        Assert.Equal(1, keHoach.SoCapNhatTen);
        // ...nhưng KHÔNG tính vào tử số (ánh xạ vốn đã NghiNgo) ⇒ không bị 0508.
        Assert.Equal(0, keHoach.SoDanhDauNghiNgo);
    }

    // ── BR-004: băm danh mục đã chuẩn hoá ───────────────────────────────────────────────────────

    [Fact]
    public void BR004_CarrierAddressSourceHash_on_dinh_thu_tu_va_Trim()
    {
        var row1 = new DonViDiaChiHang(Hang, "province", "P-001", "Ba Đình");
        var row2 = new DonViDiaChiHang(Hang, "PROVINCE", "P-002", "Ngọc Hà");

        var xuoi = CarrierAddressSourceHash.Tinh(Hang, new[] { row1, row2 });
        var nguoc = CarrierAddressSourceHash.Tinh(" ghn ", new[]
        {
            new DonViDiaChiHang(Hang, " PROVINCE ", " P-002 ", "  Ngọc Hà  "),
            new DonViDiaChiHang(Hang, "PROVINCE", "P-001", "Ba Đình")
        });

        Assert.Equal(CarrierAddressSourceHash.DoDaiHash, xuoi.Length);
        Assert.Equal(xuoi, nguoc);
    }

    [Fact]
    public void BR004_CarrierAddressSourceHash_KHONG_doi_hoa_thuong_ma_hang()
    {
        // Mã hãng chỉ Trim (`D-84`) ⇒ "p-001" và "P-001" là HAI mã khác nhau ⇒ hash phải khác.
        var hoa = CarrierAddressSourceHash.Tinh(Hang, new[] { new DonViDiaChiHang(Hang, "PROVINCE", "P-001", "A") });
        var thuong = CarrierAddressSourceHash.Tinh(Hang, new[] { new DonViDiaChiHang(Hang, "PROVINCE", "p-001", "A") });

        Assert.NotEqual(hoa, thuong);
    }

    [Fact]
    public void BR004_CarrierAddressSourceHash_chiu_null_khong_NRE()
    {
        var hash = CarrierAddressSourceHash.Tinh(null!, null);

        Assert.Equal(CarrierAddressSourceHash.DoDaiHash, hash.Length);
        Assert.All(hash, c => Assert.True(char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'));
    }

    [Fact]
    public void BR004_CarrierAddressSourceHash_truong_null_khong_NRE()
    {
        var hash = CarrierAddressSourceHash.Tinh(Hang, new[]
        {
            new DonViDiaChiHang(null!, null!, null!, null!)
        });

        Assert.Equal(CarrierAddressSourceHash.DoDaiHash, hash.Length);
    }
}
