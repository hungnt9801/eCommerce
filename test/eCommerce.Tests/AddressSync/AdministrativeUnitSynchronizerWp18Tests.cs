using eCommerce.Address;
using eCommerce.Address.Sync;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` BR-007** — danh tính ĐVHC bất biến (`D-77`): `Name` CHỈ đổi qua `DoiTen` **kèm** căn cứ
/// pháp lý (`D-239`, mã `0009`) và nguồn đổi `ParentCode` của mã đã có ⇒ từ chối CẢ LÔ (hàng rào 5,
/// `D-81`, mã `0008`).
///
/// 🎯 Vì sao quan trọng: `0009` chặn việc sửa tên danh mục pháp lý không dấu vết. Hàng rào 5 chặn việc
/// một xã "âm thầm chuyển tỉnh" — nếu lọt, resolver sẽ trả `0003` cho mọi địa chỉ đúng theo danh mục mới
/// và toàn bộ vận đơn mới fail-closed.
/// </summary>
public class AdministrativeUnitSynchronizerWp18Tests
{
    private static AdministrativeUnit DonVi(
        AdministrativeLevel cap,
        string ma,
        string ten,
        string? cha = null,
        bool hoatDong = true,
        string? legalBasis = null)
    {
        var dv = AdministrativeUnit.TaoMoi(
            AdministrativeSchemaVersion.V2, cap, ma, ten, parentCode: cha, legalBasis: legalBasis);

        if (!hoatDong)
        {
            dv.Dong("Đóng để test");
        }

        return dv;
    }

    private static DanhMucNguon Nguon(params DonViNguon[] rows)
        => new(rows.ToList(), "provinces.open-api.vn", new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc));

    // ── BR-007: DoiTen BẮT BUỘC có căn cứ pháp lý (`D-239`) ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR007_DoiTen_thieu_can_cu_phap_ly_thi_NEM_0009_va_KHONG_doi_ten(string? canCu)
    {
        var dv = DonVi(AdministrativeLevel.Tinh, "01", "Hà Nội", legalBasis: "NQ cũ");

        var ex = Assert.Throws<BusinessException>(() => dv.DoiTen("Thành phố Hà Nội", canCu!));

        Assert.Equal(AddressErrorCodes.ThieuCanCuDoiTen, ex.Code);
        // Không được đổi tên mù: cả tên LẪN căn cứ cũ giữ nguyên.
        Assert.Equal("Hà Nội", dv.Name);
        Assert.Equal("NQ cũ", dv.LegalBasis);
    }

    [Fact]
    public void BR007_DoiTen_co_can_cu_thi_doi_ten_va_ghi_can_cu_da_Trim()
    {
        var dv = DonVi(AdministrativeLevel.Tinh, "01", "Hà Nội");

        dv.DoiTen("Thành phố Hà Nội", "  NQ 60-NQ/TW  ");

        Assert.Equal("Thành phố Hà Nội", dv.Name);
        Assert.Equal("NQ 60-NQ/TW", dv.LegalBasis);
    }

    // ── BR-007 + D-81: hàng rào 5 — nguồn đổi ParentCode ────────────────────────────────────────

    [Fact]
    public void BR007_nguon_doi_ParentCode_cua_ma_DA_CO_thi_NEM_0008_va_khong_ghi_de_ParentCode()
    {
        var tinh = DonVi(AdministrativeLevel.Tinh, "01", "Hà Nội");
        var xa = DonVi(AdministrativeLevel.Xa, "00001", "Phường A", cha: "01");

        var nguon = Nguon(
            new DonViNguon(2, AdministrativeLevel.Tinh, "01", "Hà Nội"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001", "Phường A", "02"));

        var ex = Assert.Throws<BusinessException>(
            () => AdministrativeUnitSynchronizer.LapKeHoach(2, [tinh, xa], nguon, soDonViToiThieu: 2));

        Assert.Equal(AddressErrorCodes.NguonDoiDonViCapTren, ex.Code);
        Assert.Equal(1, (int)ex.Data["soDoiCha"]!);
        // Entity KHÔNG bị sửa: lô bị từ chối trước khi áp dụng.
        Assert.Equal("01", xa.ParentCode);
        Assert.Equal("Phường A", xa.Name);
    }

    [Fact]
    public void BR007_chi_khac_khoang_trang_o_ParentCode_thi_KHONG_nem_0008()
    {
        // Chuẩn hoá: Trim, `null` ≡ rỗng — chỉ khác khoảng trắng thì không phải "đổi cha".
        var tinh = DonVi(AdministrativeLevel.Tinh, "01", "Hà Nội");
        var xa = DonVi(AdministrativeLevel.Xa, "00001", "Phường A", cha: "01");

        var nguon = Nguon(
            new DonViNguon(2, AdministrativeLevel.Tinh, "01", "Hà Nội"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001", "Phường A", " 01 "));

        var keHoach = AdministrativeUnitSynchronizer.LapKeHoach(2, [tinh, xa], nguon, soDonViToiThieu: 2);

        Assert.False(keHoach.CoThayDoi);
    }

    [Fact]
    public void BR007_ma_dang_DONG_ma_nguon_doi_ParentCode_van_NEM_0008()
    {
        // `D-81`: kiểm hàng rào 5 TRƯỚC nhánh `MoLai` ⇒ bản ghi đang đóng vẫn phải bị từ chối.
        var tinh = DonVi(AdministrativeLevel.Tinh, "01", "Hà Nội");
        var xa = DonVi(AdministrativeLevel.Xa, "00001", "Phường A", cha: "01", hoatDong: false);

        var nguon = Nguon(
            new DonViNguon(2, AdministrativeLevel.Tinh, "01", "Hà Nội"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001", "Phường A", "02"));

        var ex = Assert.Throws<BusinessException>(
            () => AdministrativeUnitSynchronizer.LapKeHoach(2, [tinh, xa], nguon, soDonViToiThieu: 2));

        Assert.Equal(AddressErrorCodes.NguonDoiDonViCapTren, ex.Code);
        Assert.False(xa.IsActive);
    }

    [Fact]
    public void BR007_lo_vua_doi_cha_vua_vuot_nguong_dong_thi_UU_TIEN_0008_khong_phai_0604()
    {
        // NC-5: thứ tự hàng rào là hợp đồng. Lô vừa đổi cha (0008) vừa đóng quá 20% phải trả 0008,
        // vì 0008 là "danh tính đổi" — nguy hiểm hơn "đóng nhiều".
        var hienCo = new List<AdministrativeUnit> { DonVi(AdministrativeLevel.Tinh, "01", "Hà Nội") };

        for (var i = 1; i <= 9; i++)
        {
            hienCo.Add(DonVi(AdministrativeLevel.Xa, $"0000{i}", $"Phường {i}", cha: "01"));
        }

        // Nguồn chỉ còn tỉnh + xã 00001 (đổi cha) ⇒ 8/10 xã biến mất (vượt ngưỡng 20%).
        var nguon = Nguon(
            new DonViNguon(2, AdministrativeLevel.Tinh, "01", "Hà Nội"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001", "Phường 1", "02"));

        var ex = Assert.Throws<BusinessException>(
            () => AdministrativeUnitSynchronizer.LapKeHoach(2, hienCo, nguon, nguongDong: 0.20, soDonViToiThieu: 2));

        Assert.Equal(AddressErrorCodes.NguonDoiDonViCapTren, ex.Code);
    }
}
