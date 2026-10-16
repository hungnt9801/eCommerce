using eCommerce.Catalog;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🚨 **ẢNH SẢN PHẨM** (`CAT-003`) — ba bất biến ở **tầng domain**.
///
/// 🔒 **Ba nhóm:**
///   (1) **Không lưu bytes trong DB** (`ADR-02`) — test **reflection**: entity **cấm** có thuộc tính kiểu
///       `byte[]`. 📌 Đây là bất biến **kiến trúc**, và nó **rất dễ** bị phá: chỉ cần một lần "cho nhanh"
///       thêm `byte[] NoiDung` là bảng phình và **không có lỗi biên dịch nào**;
///   (2) **Ảnh chính PHẢI có `Alt`** (`ADR-12` + nghĩa vụ trợ năng/SEO);
///   (3) **Chỉ MỘT đường đổi cờ ảnh chính** (`LaAnhChinh` là `protected set`) ⇒ luật **chỉ có một chỗ**.
/// </summary>
public class ProductMediaTests
{
    private static ProductMedia Tao(string? alt = null, long kichThuoc = 1024)
        => new(Guid.NewGuid(), Guid.NewGuid(), "khoa/abc.jpg", DinhDangMedia.Jpeg, kichThuoc, alt);

    // ── (1) 🚨 KHÔNG CÓ BYTES TRONG ENTITY (`ADR-02`) ─────────────────────────────────────────

    [Fact]
    public void ADR02__entity_KHONG_duoc_co_thuoc_tinh_chua_BYTES_anh()
    {
        // 🚨 **VÌ SAO TEST NÀY TỒN TẠI:** `ADR-02` cấm lưu blob trong DB. Nhưng thêm `byte[] NoiDung`
        // vào entity là việc **rất dễ** khi ai đó muốn "cho nhanh" — và **không có lỗi biên dịch nào**.
        // Hệ quả khi phá: bảng phình (mỗi ảnh vài trăm KB), `pg_dump`/backup **chậm và đắt**, và vì
        // PostgreSQL đọc theo **trang 8 KB**, đọc **1 dòng metadata** cũng kéo theo trang chứa blob
        // ⇒ **mọi** truy vấn vào bảng bị ảnh hưởng.
        // 🔒 Bất biến kiến trúc phải được **khoá bằng test**, không chỉ ghi trong tài liệu.
        var thuocTinhByte = typeof(ProductMedia)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(byte[])
                     || p.PropertyType == typeof(byte)
                     || p.PropertyType == typeof(Memory<byte>)
                     || p.PropertyType == typeof(ReadOnlyMemory<byte>))
            .Select(p => $"{p.Name} : {p.PropertyType.Name}")
            .ToList();

        Assert.True(thuocTinhByte.Count == 0,
            "🚨 `ProductMedia` có thuộc tính chứa BYTES ⇒ vi phạm ADR-02 (bytes phải ở object storage, KHÔNG ở DB):\n  "
            + string.Join("\n  ", thuocTinhByte)
            + "\n⇒ Dùng `IMediaStorage` + lưu `KhoaLuuTru` (con trỏ), không lưu nội dung.");
    }

    [Fact]
    public void ADR02__entity_CO_con_tro_khoa_luu_tru()
    {
        var m = Tao();

        // 🔒 Đối chứng với test trên: entity **phải** có **con trỏ** tới bytes, nếu không thì ảnh
        // **không lấy lại được**. Hai test này là **cặp**: một cấm bytes, một đòi con trỏ.
        Assert.False(string.IsNullOrWhiteSpace(m.KhoaLuuTru));
        Assert.Equal(DinhDangMedia.Jpeg, m.DinhDangDaKiem);
    }

    // ── (2) 🚨 ẢNH CHÍNH PHẢI CÓ `Alt` ────────────────────────────────────────────────────────

    [Fact]
    public void AnhChinh__KHONG_co_Alt__NEM__va_KHONG_doi_co()
    {
        var m = Tao(alt: null);

        var loi = Assert.Throws<BusinessException>(() => m.DatLaAnhChinh(true));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc, loi.Code);
        // 🔒 "Ném mà để lại rác là gieo bug": cờ PHẢI nguyên vẹn sau khi ném.
        Assert.False(m.LaAnhChinh);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnhChinh__Alt_chi_khoang_trang__van_NEM(string altRong)
    {
        var m = Tao(alt: "có alt rồi");
        m.DatAlt(altRong);          // xoá Alt ⇒ `null`

        Assert.Throws<BusinessException>(() => m.DatLaAnhChinh(true));
        Assert.False(m.LaAnhChinh);
    }

    [Fact]
    public void AnhChinh__CO_Alt__duoc()
    {
        var m = Tao(alt: "Áo thun nam màu trắng");

        m.DatLaAnhChinh(true);

        Assert.True(m.LaAnhChinh);
    }

    [Fact]
    public void AnhChinh__bo_danh_dau__KHONG_can_Alt()
    {
        var m = Tao(alt: null);

        // 📌 Bỏ đánh dấu là thao tác **giảm** ràng buộc ⇒ không có lý do đòi `Alt`.
        // Nếu đòi, người dùng **không thể** gỡ ảnh chính khỏi một ảnh thiếu `Alt` (bế tắc).
        m.DatLaAnhChinh(false);

        Assert.False(m.LaAnhChinh);
    }

    // ── (3) CHỈ MỘT ĐƯỜNG ĐỔI CỜ ─────────────────────────────────────────────────────────────

    [Fact]
    public void LaAnhChinh__KHONG_co_setter_cong_khai__chi_doi_qua_phuong_thuc()
    {
        var thuocTinh = typeof(ProductMedia).GetProperty(nameof(ProductMedia.LaAnhChinh));

        Assert.NotNull(thuocTinh);
        // 🔒 `ADR-12` + `ADR-13` cùng dựa vào điều này: cờ ảnh chính **chỉ** đổi qua `DatLaAnhChinh`
        // (để luật *"ảnh chính phải có Alt"* **không thể** bị bỏ qua). Nếu ai đó đổi thành `public set`,
        // luật trở thành **trang trí** — và test này sẽ đỏ.
        Assert.False(thuocTinh!.SetMethod?.IsPublic ?? false,
            "`LaAnhChinh` KHÔNG được có setter công khai — đổi cờ phải qua `DatLaAnhChinh`.");
    }

    // ── (4) DỮ LIỆU VÀO: sản phẩm · khoá · kích thước ────────────────────────────────────────

    [Fact]
    public void Thieu_ProductId__NEM()
    {
        Assert.Throws<BusinessException>(
            () => new ProductMedia(Guid.NewGuid(), Guid.Empty, "k.jpg", DinhDangMedia.Jpeg, 100));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Thieu_khoa_luu_tru__NEM__vi_khong_lay_lai_duoc_bytes(string khoaRong)
    {
        Assert.Throws<BusinessException>(
            () => new ProductMedia(Guid.NewGuid(), Guid.NewGuid(), khoaRong, DinhDangMedia.Png, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Kich_thuoc_KHONG_DUONG__NEM_ma_TEP_KHONG_PHAI_ANH(long kichThuoc)
    {
        // 📌 Tệp `0` byte là ca **thật** (tải lên tệp rỗng) và nó **luôn** là lỗi: tệp rỗng **không thể**
        // có magic bytes hợp lệ. 🔑 Dùng mã `TepKhongPhaiAnh` (không phải "thiếu trường") vì chỉ dẫn đúng
        // cho người dùng là *"chọn tệp khác"*.
        var loi = Assert.Throws<BusinessException>(
            () => new ProductMedia(Guid.NewGuid(), Guid.NewGuid(), "k.jpg", DinhDangMedia.Jpeg, kichThuoc));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh, loi.Code);
    }

    // ── (5) `Alt`: chuẩn hoá + trần độ dài ───────────────────────────────────────────────────

    [Fact]
    public void DatAlt__cat_khoang_trang__va_rong_thi_thanh_null()
    {
        var m = Tao();

        m.DatAlt("  Áo thun  ");
        Assert.Equal("Áo thun", m.Alt);

        m.DatAlt("   ");
        // 📌 Lưu chuỗi khoảng trắng là **có `Alt`** theo kiểm tra `IsNullOrWhiteSpace`… nhưng theo
        // **trình đọc màn hình** thì vẫn là **rỗng** ⇒ phải chuẩn hoá về `null` để hai bên **nhất quán**.
        Assert.Null(m.Alt);
    }

    [Fact]
    public void DatAlt__qua_dai__NEM()
    {
        var m = Tao();
        var quaDai = new string('a', ProductMedia.DoDaiAltToiDa + 1);

        Assert.Throws<BusinessException>(() => m.DatAlt(quaDai));
    }

    [Fact]
    public void Khoa_luu_tru__qua_dai__NEM()
    {
        var quaDai = new string('k', ProductMedia.DoDaiKhoaLuuTruToiDa + 1);

        Assert.Throws<BusinessException>(
            () => new ProductMedia(Guid.NewGuid(), Guid.NewGuid(), quaDai, DinhDangMedia.Jpeg, 100));
    }

    [Fact]
    public void Anh_moi__mac_dinh_KHONG_phai_anh_chinh()
    {
        var m = Tao(alt: "có alt");

        // 🔒 Mặc định `false` ⇒ **không** ảnh nào "vô tình" thành ảnh chính (unique index ở DB sẽ chặn
        // ảnh chính thứ hai, nhưng lỗi ràng buộc **khó hiểu** hơn hẳn so với việc mặc định đúng).
        Assert.False(m.LaAnhChinh);
    }
}
