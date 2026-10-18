using eCommerce.Address;
using Volo.Abp;

// 🚨 VÌ SAO PHẢI CÓ ALIAS — bằng chứng THẬT cho `ADDR-004` **Q10**:
// nếu namespace của tệp kết thúc bằng `.Address` thì tên `Address` trần bị hiểu là **namespace**
// (`CS0118: 'Address' is a namespace but is used like a type`), KHÔNG phải type. Đo được ngày
// 18/10/2026 khi viết chính tệp này; namespace test đổi sang `eCommerce.Tests.DiaChi` và dùng alias.
// 🔑 Entity vẫn tên `Address` (namespace `eCommerce.Address` phân giải đúng) ⇒ KHÔNG phải đổi tên.
using AddressEntity = eCommerce.Address.Address;

namespace eCommerce.Tests.DiaChi;

/// <summary>
/// Test cho **`ADDR-004` phần A** — entity `Address` (địa chỉ VẬT LÝ, `AddrAddresses`).
///
/// 🎯 Bốn nhóm lỗi **IM LẶNG** được khoá lại ở đây:
///   1. **Bỏ dấu tiếng Việt trong canonical** ⇒ *"Xã Đông Xuân"* và *"Xã Dong Xuan"* bị gộp thành
///      **một** địa chỉ, dù có thể là hai địa danh khác nhau. Không lỗi nào nổi lên.
///   2. **Không `Normalize(FormC)`** ⇒ hai chuỗi **nhìn giống hệt** (dựng sẵn vs tổ hợp) cho **hai hash
///      khác nhau** ⇒ khử trùng lặp hỏng âm thầm.
///   3. **Nội dung địa lý đổi được sau `DaChuanHoa`** ⇒ hoá đơn/vận đơn đã in bị **sai hồi tố**.
///   4. **Guard trạng thái im lặng** (quy tắc bất di bất dịch #11) ⇒ chuyển sai mà không ai biết.
/// </summary>
public class AddressTests
{
    private static readonly DateTime Luc = new(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);

    private static AddressEntity DiaChiDaChuanHoa(
        string tinh = "01", string? huyen = null, string xa = "00004",
        string? soNha = "12 Lê Lợi", string dayDu = "12 Lê Lợi, Phường Ba Đình, Hà Nội",
        int phienBan = 2)
        => AddressEntity.TaoDaChuanHoa(
            AdministrativeSchemaVersion.TheoSo(phienBan),
            tinh, huyen, xa, soNha, dayDu, NguonTaoDiaChi.NhanVien, Luc);

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // 1. CANONICAL HOÁ — ba điều CẤM ở `ADDR-004` §5.2
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC01_canonical_KHONG_bo_dau_tieng_Viet()
    {
        // 🚨 Đây là điều CẤM #1. Nếu ai đó thêm `unaccent` vào `Chuan()` (ví dụ để "dễ so sánh"),
        // hai địa danh khác nhau sẽ gộp thành một — và không test nào khác bắt được.
        var coDau = DiaChiCanonical.Chuan("Xã Đông Xuân");
        var khongDau = DiaChiCanonical.Chuan("Xa Dong Xuan");

        Assert.NotEqual(coDau, khongDau);
        Assert.Equal("xã đông xuân", coDau);
        Assert.Contains("đ", coDau);     // `đ` là chữ cái riêng của tiếng Việt, không phải `d` + dấu
    }

    [Fact]
    public void TC02_canonical_chuan_hoa_FormC_nen_hai_dang_Unicode_cho_CUNG_hash()
    {
        // 🚨 Điều CẤM #2: "Phường" dựng sẵn (NFC) và "Phường" tổ hợp (NFD) NHÌN GIỐNG HỆT nhưng khác
        // chuỗi ⇒ thiếu `Normalize(FormC)` là hai hash khác nhau ⇒ khử trùng lặp HỎNG ÂM THẦM.
        var dungSan = "Phường Ba Đình";                                   // như người dùng gõ
        var toHop = dungSan.Normalize(System.Text.NormalizationForm.FormD);

        // Tiền đề: hai chuỗi THẬT SỰ khác nhau ở mức byte — nếu không, test này vô nghĩa.
        Assert.NotEqual(dungSan, toHop);

        Assert.Equal(
            DiaChiCanonical.TinhHashTho("12 Lê Lợi, " + dungSan),
            DiaChiCanonical.TinhHashTho("12 Lê Lợi, " + toHop));
    }

    [Fact]
    public void TC03_canonical_gop_khoang_trang_va_khac_hoa_thuong()
    {
        var a = DiaChiCanonical.TinhHashTho("12  Lê   Lợi ");
        var b = DiaChiCanonical.TinhHashTho("12 Lê Lợi");

        Assert.Equal(a, b);
        Assert.StartsWith("r|", DiaChiCanonical.DungChuoiCanonicalTho("12 Lê Lợi"));
    }

    [Fact]
    public void TC04_hash_dung_dinh_dang_hex_64_chu_thuong()
    {
        // 🔒 Khớp CHECK ở DB: `"ContentHash" ~ '^[0-9a-f]{64}$'` (chặn base64 / hex HOA / bytea).
        var hash = DiaChiCanonical.TinhHashTho("12 Lê Lợi");

        Assert.Equal(DiaChiCanonical.DoDaiHash, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void TC05_canonical_da_chuan_hoa_co_tien_to_va_phien_ban()
    {
        var s = DiaChiCanonical.DungChuoiCanonicalDaChuanHoa(2, "01", null, "00004", "12 Lê Lợi");

        // Đúng đặc tả §5.2: "n" + version + "|" + tỉnh + "|" + (huyện ?? "-") + "|" + xã + "|" + chữ
        Assert.Equal("n2|01|-|00004|12 lê lợi", s);
    }

    [Fact]
    public void TC06_huyen_null_va_huyen_rong_cho_CUNG_hash()
    {
        // 🔒 `HuyenCode` null (v2) và chuỗi khoảng trắng phải cho CÙNG một hash — nếu không, cùng một
        // địa chỉ đi qua hai đường (entity đã Trim vs nơi gọi truyền "   ") sẽ thành HAI bản ghi.
        // 📌 Bảo vệ ở hai lớp: `DungChuoiCanonicalDaChuanHoa` quy khoảng trắng về "-", và entity
        //    biến huyện rỗng thành `null` trước khi băm (TC07).
        var khongHuyen = DiaChiCanonical.TinhHashDaChuanHoa(2, "01", null, "00004", "12 Lê Lợi");
        var huyenRong = DiaChiCanonical.TinhHashDaChuanHoa(2, "01", "   ", "00004", "12 Lê Lợi");

        Assert.Equal(khongHuyen, huyenRong);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // 2. TẠO ĐỊA CHỈ — fail-closed
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC07_tao_da_chuan_hoa_chuan_hoa_so_nha_va_huyen_rong_thanh_null()
    {
        var a = DiaChiDaChuanHoa(huyen: "   ", soNha: "  12 Lê Lợi  ");
        var b = DiaChiDaChuanHoa(huyen: null, soNha: "12 Lê Lợi");

        Assert.Null(a.HuyenCode);
        Assert.Equal("12 Lê Lợi", a.SoNhaDuong);
        Assert.Equal(TrangThaiDiaChi.DaChuanHoa, a.TrangThai);
        Assert.NotNull(a.NormalizedAtUtc);
        Assert.Equal(b.ContentHash, a.ContentHash);
    }

    [Fact]
    public void TC08_tao_dia_chi_tho_KHONG_co_ma_va_hash_tien_to_r()
    {
        var a = AddressEntity.TaoTuDiaChiTho(" 123 Nguyễn Huệ, Quận 1 ", NguonTaoDiaChi.San);

        Assert.Equal(TrangThaiDiaChi.ChuaChuanHoa, a.TrangThai);
        Assert.Null(a.TinhCode);
        Assert.Null(a.XaCode);
        Assert.Null(a.HuyenCode);
        Assert.Null(a.NormalizedAtUtc);
        Assert.Equal(DiaChiCanonical.TinhHashTho("123 Nguyễn Huệ, Quận 1"), a.ContentHash);
        Assert.Equal("123 Nguyễn Huệ, Quận 1", a.DiaChiDayDu);   // Trim, KHÔNG cắt giữa chừng
    }

    [Fact]
    public void TC09_thieu_noi_dung_thi_NEM_0105()
    {
        var ex = Assert.Throws<BusinessException>(
            () => AddressEntity.TaoTuDiaChiTho("   ", NguonTaoDiaChi.Web));

        Assert.Equal(AddressErrorCodes.ThieuNoiDung, ex.Code);
    }

    [Fact]
    public void TC10_huyen_o_schema_2_cap_thi_NEM_0004()
    {
        // 🚨 Quy tắc PHÁP LÝ (Luật 72/2025/QH15): từ 01/7/2025 bỏ cấp huyện.
        var ex = Assert.Throws<BusinessException>(
            () => DiaChiDaChuanHoa(huyen: "001", phienBan: 2));

        Assert.Equal(AddressErrorCodes.HuyenKhongDuocPhepOCap2, ex.Code);
    }

    [Fact]
    public void TC11_ma_sai_dinh_dang_thi_NEM_0002()
    {
        // Tỉnh phải 2 chữ số — ném thay vì im lặng cắt/nối.
        var ex = Assert.Throws<BusinessException>(() => DiaChiDaChuanHoa(tinh: "1"));

        Assert.Equal(AddressErrorCodes.MaKhongHopLe, ex.Code);
    }

    [Fact]
    public void TC12_phien_ban_schema_la_thi_NEM_0005()
    {
        var ex = Assert.Throws<BusinessException>(() => AdministrativeSchemaVersion.TheoSo(3));

        Assert.Equal(AddressErrorCodes.PhienBanKhongHopLe, ex.Code);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // 3. MÁY TRẠNG THÁI — mọi chuyển SAI đều NÉM (quy tắc #11)
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TC13_chuan_hoa_thanh_cong_doi_trang_thai_va_tinh_lai_hash()
    {
        var a = AddressEntity.TaoTuDiaChiTho("12 Lê Lợi, Phường Ba Đình, Hà Nội", NguonTaoDiaChi.Web);
        var hashTho = a.ContentHash;

        a.ChuanHoa(
            AdministrativeSchemaVersion.V2, "01", null, "00004", "12 Lê Lợi",
            "12 Lê Lợi, Phường Ba Đình, Hà Nội", Luc);

        Assert.Equal(TrangThaiDiaChi.DaChuanHoa, a.TrangThai);
        Assert.Equal("01", a.TinhCode);
        Assert.Equal("00004", a.XaCode);
        Assert.Equal(Luc, a.NormalizedAtUtc);
        // 🔑 Hash PHẢI đổi sang đường canonical "n…" — nếu KHÔNG đổi thì địa chỉ đã chuẩn hoá đang
        // mang khoá tự nhiên của bản THÔ, và mọi so khớp trùng lặp đều sai.
        Assert.NotEqual(hashTho, a.ContentHash);
        Assert.Equal(
            DiaChiCanonical.TinhHashDaChuanHoa(2, "01", null, "00004", "12 Lê Lợi"),
            a.ContentHash);
    }

    [Fact]
    public void TC14_chuan_hoa_LAN_HAI_thi_NEM_0102()
    {
        // 🚨 ADR-05: nội dung địa lý BẤT BIẾN sau khi chuẩn hoá ⇒ phải tạo bản ghi mới.
        // Nếu im lặng cho qua, hoá đơn đã in từ snapshot chữ sẽ SAI HỒI TỐ.
        var a = DiaChiDaChuanHoa();

        var ex = Assert.Throws<BusinessException>(() => a.ChuanHoa(
            AdministrativeSchemaVersion.V2, "01", null, "00005", "99 Lê Lợi", "99 Lê Lợi, Hà Nội", Luc));

        Assert.Equal(AddressErrorCodes.DaCoNoiDungDiaLy, ex.Code);
        Assert.Equal("00004", a.XaCode);                  // KHÔNG bị đổi
    }

    [Fact]
    public void TC15_chuan_hoa_dia_chi_DA_XAC_MINH_cung_NEM_0102()
    {
        var a = DiaChiDaChuanHoa();
        a.XacMinh("Đối chiếu vận đơn GHN", Luc);

        var ex = Assert.Throws<BusinessException>(() => a.ChuanHoa(
            AdministrativeSchemaVersion.V2, "01", null, "00005", null, "x", Luc));

        Assert.Equal(AddressErrorCodes.DaCoNoiDungDiaLy, ex.Code);
    }

    [Fact]
    public void TC16_xac_minh_thieu_can_cu_thi_NEM_0106()
    {
        var a = DiaChiDaChuanHoa();

        var ex = Assert.Throws<BusinessException>(() => a.XacMinh("  ", Luc));

        Assert.Equal(AddressErrorCodes.ThieuCanCuXacMinh, ex.Code);
        Assert.Equal(TrangThaiDiaChi.DaChuanHoa, a.TrangThai);   // KHÔNG đổi trạng thái
    }

    [Fact]
    public void TC17_xac_minh_dia_chi_THO_thi_NEM_0103()
    {
        // Chưa biết đơn vị hành chính thì KHÔNG thể "xác minh địa chỉ đúng".
        var a = AddressEntity.TaoTuDiaChiTho("123 Nguyễn Huệ", NguonTaoDiaChi.San);

        var ex = Assert.Throws<BusinessException>(() => a.XacMinh("có ảnh chụp", Luc));

        Assert.Equal(AddressErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    [Fact]
    public void TC18_xac_minh_lai_tu_NghiNgo_duoc_phep()
    {
        var a = DiaChiDaChuanHoa();
        a.DanhDauNghiNgo("mã xã đã đóng");
        Assert.Equal(TrangThaiDiaChi.NghiNgo, a.TrangThai);

        a.XacMinh("đã đối chiếu danh mục mới", Luc);

        Assert.Equal(TrangThaiDiaChi.DaXacMinh, a.TrangThai);
    }

    [Fact]
    public void TC19_danh_dau_nghi_ngo_tu_DaXacMinh_GIU_nguyen_lich_su_xac_minh()
    {
        var a = DiaChiDaChuanHoa();
        a.XacMinh("căn cứ A", Luc);

        a.DanhDauNghiNgo("đối chiếu lại thấy lệch");

        Assert.Equal(TrangThaiDiaChi.NghiNgo, a.TrangThai);
        Assert.Equal(Luc, a.XacMinhLucUtc);      // 🔒 giữ lịch sử, không xoá dấu vết
    }

    [Fact]
    public void TC20_danh_dau_nghi_ngo_tu_dia_chi_THO_thi_NEM_0103()
    {
        var a = AddressEntity.TaoTuDiaChiTho("123 Nguyễn Huệ", NguonTaoDiaChi.San);

        var ex = Assert.Throws<BusinessException>(() => a.DanhDauNghiNgo("lý do"));

        Assert.Equal(AddressErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    [Fact]
    public void TC21_danh_dau_nghi_ngo_thieu_ly_do_thi_NEM_0103()
    {
        var a = DiaChiDaChuanHoa();

        var ex = Assert.Throws<BusinessException>(() => a.DanhDauNghiNgo("   "));

        Assert.Equal(AddressErrorCodes.ChuyenTrangThaiKhongHopLe, ex.Code);
    }

    [Fact]
    public void TC22_dia_chi_da_chuan_hoa_co_du_ma_va_bang_chung()
    {
        // 🔒 Bất biến fail-closed ở mức entity — DB còn một hàng rào nữa (CHECK #③).
        var a = DiaChiDaChuanHoa();

        Assert.NotNull(a.TinhCode);
        Assert.NotNull(a.XaCode);
        Assert.NotNull(a.NormalizedAtUtc);
        Assert.Matches("^[0-9]{2}$", a.TinhCode);
        Assert.Matches("^[0-9]{5}$", a.XaCode);
    }

    [Fact]
    public void TC23_schema_1_cap_cho_phep_huyen_va_co_trong_hash()
    {
        // Dữ liệu LỊCH SỬ (3 cấp) vẫn phải đọc được — nhưng hash phải mang `n1` + huyện.
        var a = DiaChiDaChuanHoa(tinh: "01", huyen: "001", xa: "00004", phienBan: 1);

        Assert.Equal(1, a.SchemaVersion);
        Assert.Equal("001", a.HuyenCode);
        Assert.Equal(
            DiaChiCanonical.TinhHashDaChuanHoa(1, "01", "001", "00004", "12 Lê Lợi"),
            a.ContentHash);
    }

    [Fact]
    public void TC24_hai_duong_tao_cung_noi_dung_cho_CUNG_hash()
    {
        // 🔑 Nếu hai đường (tạo mới vs chuẩn hoá) tính hash khác nhau thì khử trùng lặp hỏng âm thầm.
        var trucTiep = DiaChiDaChuanHoa();

        var quaChuanHoa = AddressEntity.TaoTuDiaChiTho("12 Lê Lợi, Phường Ba Đình, Hà Nội", NguonTaoDiaChi.Web);
        quaChuanHoa.ChuanHoa(
            AdministrativeSchemaVersion.V2, "01", null, "00004", "12 Lê Lợi",
            "12 Lê Lợi, Phường Ba Đình, Hà Nội", Luc);

        Assert.Equal(trucTiep.ContentHash, quaChuanHoa.ContentHash);
    }
}
