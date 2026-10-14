using eCommerce.Address;
using eCommerce.Address.Sync;
using Volo.Abp;

namespace eCommerce.Tests.AddressSync;

/// <summary>
/// Test cho **bộ phân tích JSON `provinces.open-api.vn`** — dùng **JSON RÚT GỌN TỪ PHẢN HỒI THẬT**
/// (đã gọi API ngày 14/10/2026), không phải JSON tự nghĩ ra.
///
/// 🎯 Ba cạm bẫy được khoá ở đây:
///   1. `wards` **rỗng** ở endpoint danh sách (`/api/v2/p/`) ⇒ phải gọi `?depth=2` theo từng tỉnh.
///   2. Mã là **SỐ NGUYÊN** còn `AdministrativeUnit.Code` cần **đủ 5 ký tự** ⇒ **phải đệm 0 bên trái**.
///   3. **v1 khác v2** (v1 có `districts[].wards`, v2 có `wards` trực tiếp).
/// </summary>
public class ProvincesOpenApiParserTests
{
    // ── JSON THẬT (rút gọn) ────────────────────────────────────────────────

    /// <summary>`GET /api/v2/p/` — 34 tỉnh, `wards` RỖNG (đúng như API thật trả về).</summary>
    private const string JsonDanhSachV2 = """
    [
      { "name": "Thành phố Hà Nội", "code": 1, "division_type": "thành phố trung ương", "codename": "ha_noi", "phone_code": 24, "wards": [] },
      { "name": "Tỉnh Cao Bằng", "code": 4, "division_type": "tỉnh", "codename": "cao_bang", "phone_code": 206, "wards": [] }
    ]
    """;

    /// <summary>`GET /api/v2/p/1?depth=2` — tỉnh + 2 xã (mã thật: 4 = Ba Đình, 8 = Ngọc Hà).</summary>
    private const string JsonTinhV2 = """
    {
      "name": "Thành phố Hà Nội", "code": 1, "division_type": "thành phố trung ương", "codename": "ha_noi",
      "phone_code": 24,
      "wards": [
        { "name": "Phường Ba Đình", "code": 4, "division_type": "phường", "codename": "phuong_ba_dinh", "province_code": 1 },
        { "name": "Phường Ngọc Hà", "code": 8, "division_type": "phường", "codename": "phuong_ngoc_ha", "province_code": 1 }
      ]
    }
    """;

    /// <summary>`GET /api/v1/p/4?depth=3` — 3 cấp (mã thật: huyện 40, xã 1267).</summary>
    private const string JsonTinhV1 = """
    {
      "name": "Tỉnh Cao Bằng", "code": 4, "division_type": "tỉnh", "codename": "cao_bang",
      "districts": [
        {
          "name": "Thành phố Cao Bằng", "code": 40, "division_type": "thành phố", "province_code": 4,
          "wards": [
            { "name": "Phường Sông Hiến", "code": 1267, "division_type": "phường", "district_code": 40 }
          ]
        }
      ]
    }
    """;

    // ── Lấy danh sách tỉnh ─────────────────────────────────────────────────

    [Fact]
    public void LayMaTinh_dem_0_ben_trai_cho_du_2_chu_so()
    {
        // 🚨 Cạm bẫy 2: API trả `code: 1` (số) nhưng mã tỉnh phải là chuỗi "01".
        var ma = ProvincesOpenApiParser.LayMaTinh(JsonDanhSachV2);

        Assert.Equal(["01", "04"], ma);
    }

    [Fact]
    public void LayMaTinh_JSON_khong_phai_mang_thi_NEM_LOI_0605()
    {
        var ex = Assert.Throws<BusinessException>(() => ProvincesOpenApiParser.LayMaTinh("{\"a\":1}"));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang, ex.Code);
    }

    // ── Phân tích một tỉnh ─────────────────────────────────────────────────

    [Fact]
    public void V2_phan_tich_duoc_tinh_va_cac_xa()
    {
        var ds = ProvincesOpenApiParser.PhanTichTinh(JsonTinhV2, schemaVersion: 2);

        Assert.Equal(3, ds.Count);

        var tinh = ds[0];
        Assert.Equal(AdministrativeLevel.Tinh, tinh.Level);
        Assert.Equal("01", tinh.Code);
        Assert.Equal("Thành phố Hà Nội", tinh.Name);
        Assert.Null(tinh.ParentCode);

        // 🚨 Cạm bẫy 2 ở cấp xã: `code: 4` ⇒ "00004" (5 ký tự).
        var xa = ds.Single(x => x.Name == "Phường Ba Đình");
        Assert.Equal(AdministrativeLevel.Xa, xa.Level);
        Assert.Equal("00004", xa.Code);
        Assert.Equal("01", xa.ParentCode);
        Assert.Equal(2, xa.SchemaVersion);

        Assert.Equal("00008", ds.Single(x => x.Name == "Phường Ngọc Hà").Code);
    }

    [Fact]
    public void V1_phan_tich_duoc_ca_3_cap()
    {
        // 🚨 Cạm bẫy 3: v1 lồng xã TRONG huyện.
        var ds = ProvincesOpenApiParser.PhanTichTinh(JsonTinhV1, schemaVersion: 1);

        Assert.Equal(3, ds.Count);
        Assert.Equal("04", ds[0].Code);                                            // tỉnh: 2 chữ số
        Assert.Equal("040", ds.Single(x => x.Level == AdministrativeLevel.QuanHuyen).Code);   // huyện: 3 chữ số
        Assert.Equal("01267", ds.Single(x => x.Level == AdministrativeLevel.Xa).Code);        // xã: 5 chữ số
    }

    [Fact]
    public void Xa_o_V1_co_parent_la_HUYEN_chu_khong_phai_tinh()
    {
        var ds = ProvincesOpenApiParser.PhanTichTinh(JsonTinhV1, schemaVersion: 1);
        var xa = ds.Single(x => x.Level == AdministrativeLevel.Xa);

        Assert.Equal("040", xa.ParentCode);
    }

    // ── 🚨 Cạm bẫy 1: `wards` rỗng ở endpoint danh sách ────────────────────

    [Fact]
    public void V2_danh_sach_tinh_co_wards_RONG_thi_chi_thu_duoc_TINH()
    {
        // Đây chính là dữ liệu THẬT của `/api/v2/p/`. Test này ghi lại cạm bẫy:
        // ai chỉ gọi endpoint danh sách sẽ nhận **34 tỉnh và 0 xã** ⇒ danh mục thiếu toàn bộ cấp xã,
        // chính là thứ sẽ khiến hàng rào 4 (`0604`) phải chặn ở lần đồng bộ sau.
        var ds = ProvincesOpenApiParser.PhanTichTinh("""{ "name": "Tỉnh Cao Bằng", "code": 4, "wards": [] }""", 2);

        Assert.Single(ds);
        Assert.Equal(AdministrativeLevel.Tinh, ds[0].Level);
        Assert.DoesNotContain(ds, x => x.Level == AdministrativeLevel.Xa);
    }

    // ── Fail-closed khi sai định dạng ──────────────────────────────────────

    [Fact]
    public void Thieu_truong_code_thi_NEM_LOI_0605()
    {
        var ex = Assert.Throws<BusinessException>(
            () => ProvincesOpenApiParser.PhanTichTinh("""{ "name": "Tỉnh lạ" }""", 2));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang, ex.Code);
    }

    [Fact]
    public void Code_khong_phai_SO_thi_NEM_LOI_0605()
    {
        // Nếu nguồn đổi định dạng (mã thành chuỗi) thì phải DỪNG, không được đoán.
        var ex = Assert.Throws<BusinessException>(
            () => ProvincesOpenApiParser.PhanTichTinh("""{ "name": "Tỉnh lạ", "code": "01" }""", 2));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang, ex.Code);
    }

    [Fact]
    public void Thieu_truong_name_thi_NEM_LOI_0605()
    {
        var ex = Assert.Throws<BusinessException>(
            () => ProvincesOpenApiParser.PhanTichTinh("""{ "code": 4 }""", 2));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang, ex.Code);
    }

    [Fact]
    public void Ma_DAI_HON_quy_uoc_thi_NEM_LOI_chu_KHONG_cat_bot()
    {
        // 🚨 Bất biến: mã dài hơn quy ước nghĩa là HIỂU SAI định dạng ⇒ dừng, KHÔNG cắt bớt
        // (cắt bớt sẽ tạo ra mã "hợp lệ" nhưng SAI, và không ai phát hiện).
        var ex = Assert.Throws<BusinessException>(
            () => ProvincesOpenApiParser.PhanTichTinh("""{ "name": "Tỉnh lạ", "code": 12345 }""", 2));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang, ex.Code);
        Assert.Equal(2, ex.Data["doDaiYeuCau"]);
    }

    [Fact]
    public void JSON_tinh_khong_phai_doi_tuong_thi_NEM_LOI_0605()
    {
        var ex = Assert.Throws<BusinessException>(() => ProvincesOpenApiParser.PhanTichTinh("[]", 2));

        Assert.Equal(AdministrativeSyncErrorCodes.DanhMucSaiDinhDang, ex.Code);
    }

    // ── Ghép nhiều tỉnh ────────────────────────────────────────────────────

    [Fact]
    public void Ghep_nhieu_tinh_lai_va_ghi_nguon_thoi_diem()
    {
        var taiLuc = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);

        var danhMuc = ProvincesOpenApiParser.Ghep(
            [
                ProvincesOpenApiParser.PhanTichTinh(JsonTinhV2, 2),
                ProvincesOpenApiParser.PhanTichTinh("""{ "name": "Tỉnh Cao Bằng", "code": 4, "wards": [] }""", 2)
            ],
            taiLuc);

        Assert.Equal(4, danhMuc.DonVis.Count);          // 2 tỉnh + 2 xã
        Assert.Equal(ProvincesOpenApiParser.TenNguon, danhMuc.Nguon);
        Assert.Equal(taiLuc, danhMuc.TaiLucUtc);
        Assert.Equal(2, danhMuc.DonVis.Count(x => x.Level == AdministrativeLevel.Tinh));
    }

    [Fact]
    public void Danh_muc_phan_tich_duoc_PHICH_HOP_voi_bo_dong_bo()
    {
        // Nối hai lõi: parser → synchronizer phải chạy được mà không cần chỉnh gì
        // (đây là điều kiện để worker chỉ còn là lớp mỏng).
        var danhMuc = ProvincesOpenApiParser.Ghep([ProvincesOpenApiParser.PhanTichTinh(JsonTinhV2, 2)],
            new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));

        var kh = AdministrativeUnitSynchronizer.LapKeHoach(2, [], danhMuc, soDonViToiThieu: 2);

        Assert.Equal(3, kh.SoThemMoi);

        var moi = AdministrativeUnitSynchronizer.ApDung(kh);
        Assert.Contains(moi, x => x.Code == "00004" && x.ParentCode == "01");
    }
}
