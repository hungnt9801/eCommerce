using eCommerce.Address;
using eCommerce.Address.Sync;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// **`ADDR-001` BR-008/BR-009** — `AddressSourceHash` là SHA-256 của danh mục ĐVHC **đã chuẩn hoá**
/// (`D-78` §7.3): ổn định qua lần chạy lại, **không** phụ thuộc thứ tự dòng nguồn, **không** phụ thuộc
/// khoảng trắng; **cấm** `Guid.NewGuid()`/thời điểm. Sai hash ⇒ "chạy lại cùng dữ liệu" thành "lô mới"
/// (BR-008) và không đối soát được file vs job (BR-009).
/// </summary>
public class AddressSourceHashTests
{
    private static readonly DonViNguon[] Mau =
    [
        new(2, AdministrativeLevel.Tinh, "01", "Hà Nội"),
        new(2, AdministrativeLevel.Xa, "00001", "Phường A", "01"),
        new(2, AdministrativeLevel.Xa, "00002", "Phường B", "01")
    ];

    [Fact]
    public void BR009_hash_la_SHA256_hex_THUONG_64_ky_tu()
    {
        var hash = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau);

        Assert.Equal(AddressSourceHash.DoDaiHash, hash.Length);
        Assert.All(hash, c => Assert.True(char.IsAsciiDigit(c) || c is >= 'a' and <= 'f', $"Ký tự '{c}' không phải hex thường."));
    }

    [Fact]
    public void BR009_chay_lai_cung_du_lieu_thi_CUNG_hash()
    {
        var lan1 = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau);
        var lan2 = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau);

        Assert.Equal(lan1, lan2);
    }

    [Fact]
    public void BR009_thu_tu_dong_nguon_KHONG_doi_hash()
    {
        var xuoi = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau);
        var nguoc = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau.Reverse());

        Assert.Equal(xuoi, nguoc);
    }

    [Fact]
    public void BR009_chi_khac_khoang_trang_thi_KHONG_doi_hash()
    {
        var goc = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau);

        var coKhoangTrang = AddressSourceHash.Tinh(2, "  provinces.open-api.vn  ", new[]
        {
            new DonViNguon(2, AdministrativeLevel.Tinh, " 01 ", " Hà Nội "),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001 ", "Phường A", " 01"),
            new DonViNguon(2, AdministrativeLevel.Xa, " 00002", " Phường B ", "01 ")
        });

        Assert.Equal(goc, coKhoangTrang);
    }

    [Fact]
    public void BR009_du_lieu_khac_thi_hash_KHAC()
    {
        var goc = AddressSourceHash.Tinh(2, "provinces.open-api.vn", Mau);
        var doiTen = AddressSourceHash.Tinh(2, "provinces.open-api.vn", new[]
        {
            new DonViNguon(2, AdministrativeLevel.Tinh, "01", "Hà Nội mới"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001", "Phường A", "01"),
            new DonViNguon(2, AdministrativeLevel.Xa, "00002", "Phường B", "01")
        });
        var doiPhienBan = AddressSourceHash.Tinh(1, "provinces.open-api.vn", Mau);
        var doiNguon = AddressSourceHash.Tinh(2, "nguon-khac", Mau);

        Assert.NotEqual(goc, doiTen);
        Assert.NotEqual(goc, doiPhienBan);
        Assert.NotEqual(goc, doiNguon);
    }

    [Fact]
    public void BR009_chiu_null_va_null_tuong_duong_rong_khong_NRE()
    {
        // Danh mục lỗi trước khi có dòng nào (adapter ném) ⇒ `rows = null`; `SourceName` có thể null.
        var hashNull = AddressSourceHash.Tinh(2, null, null);
        var hashRong = AddressSourceHash.Tinh(2, "", []);

        Assert.Equal(AddressSourceHash.DoDaiHash, hashNull.Length);
        Assert.Equal(hashRong, hashNull);
    }

    [Fact]
    public void BR009_tung_truong_null_khong_lam_NRE()
    {
        // `Code`/`ParentCode`/`Name` null là dữ liệu nguồn hỏng — hash vẫn phải tính được (hàng rào 0b
        // mới là nơi từ chối), KHÔNG được ném NullReferenceException ở bước băm.
        var hash = AddressSourceHash.Tinh(2, "provinces.open-api.vn", new[]
        {
            new DonViNguon(2, AdministrativeLevel.Tinh, null!, null!),
            new DonViNguon(2, AdministrativeLevel.Xa, "00001", null!, "01")
        });

        Assert.Equal(AddressSourceHash.DoDaiHash, hash.Length);
    }
}
