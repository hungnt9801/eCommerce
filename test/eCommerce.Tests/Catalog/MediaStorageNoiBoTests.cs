using eCommerce.Catalog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🚨 **KHO TỆP TRÊN ĐĨA CỤC BỘ** (`CAT-003` `ADR-02`) — hiện thực <see cref="IMediaStorage"/>.
///
/// 🔒 **HAI NHÓM ĐƯỢC KHOÁ Ở ĐÂY:**
///   (1) 🚨 **CHỐNG THOÁT THƯ MỤC** — lỗ hổng **nghiêm trọng nhất** của mọi chức năng tải tệp. Nếu khoá
///       đến từ dữ liệu người dùng kiểm soát, `../../appsettings.json` sẽ khiến hệ thống **ghi/đọc/xoá tệp
///       NGOÀI thư mục kho** — ghi đè cấu hình, hoặc **đặt tệp thực thi vào thư mục web**.
///       📌 Khoá trong lớp này **do hệ thống sinh**, NHƯNG nó **được lưu vào DB và đọc lại** ⇒ nó là
///       **dữ liệu**, phải kiểm **như dữ liệu không tin được**;
///   (2) **Hợp đồng I/O** — ghi rồi đọc lại ra **đúng bytes** · đọc khoá không tồn tại ⇒ `null` (**không ném**)
///       · **xoá là IDEMPOTENT** (xoá hai lần ⇒ **không ném**) · tệp rỗng ⇒ **từ chối**.
///
/// 📌 Mỗi test dùng **thư mục tạm RIÊNG** (`Guid`) ⇒ test **độc lập**, chạy song song được, và **không**
/// để lại rác trong repo. ⚠️ Đây **không phải** chi tiết nhỏ: dùng chung một thư mục thì test này xoá tệp
/// của test kia ⇒ **đỏ ngẫu nhiên** — loại lỗi tốn thời gian nhất để tìm.
/// </summary>
public class MediaStorageNoiBoTests : IDisposable
{
    private readonly string _gocTest;
    private readonly MediaStorageNoiBo _kho;

    public MediaStorageNoiBoTests()
    {
        _gocTest = Path.Combine(Path.GetTempPath(), "ecom-media-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_gocTest);

        _kho = new MediaStorageNoiBo(_gocTest);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_gocTest))
            {
                Directory.Delete(_gocTest, recursive: true);
            }
        }
        catch (IOException)
        {
            // Dọn rác không được làm đỏ test — rác trong thư mục tạm của hệ điều hành là vô hại.
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>`IHostEnvironment` giả — chỉ cần `ContentRootPath` (lớp này **không** dùng gì khác).</summary>
    private sealed class MoiTruongGia : IHostEnvironment
    {
        public MoiTruongGia(string goc) => ContentRootPath = goc;

        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static byte[] AnhPngThat() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01
    ];

    // ── (1) HỢP ĐỒNG I/O ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Luu_roi_Doc_lai__ra_DUNG_bytes()
    {
        var bytes = AnhPngThat();

        var kq = await _kho.LuuAsync(bytes, DinhDangMedia.Png, tenantId: null, productId: Guid.NewGuid());
        var doc = await _kho.DocAsync(kq.KhoaLuuTru);

        Assert.NotNull(doc);
        Assert.Equal(bytes, doc);
    }

    [Fact]
    public async Task Luu__khoa_do_HE_THONG_sinh__co_duoi_theo_dinh_dang_va_nam_trong_thu_muc_kho()
    {
        var kq = await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, null, Guid.NewGuid());

        // 🔒 Khoá **không** chứa tên tệp client (lớp này **không** nhận tham số tên tệp — đó là chủ ý,
        // xem hợp đồng `IMediaStorage`). Đuôi tệp lấy từ **định dạng đã kiểm** ⇒ `Content-Type` khi phục vụ
        // khớp với nội dung thật.
        Assert.EndsWith(".png", kq.KhoaLuuTru, StringComparison.Ordinal);
        Assert.StartsWith("media/", kq.KhoaLuuTru, StringComparison.Ordinal);
        Assert.DoesNotContain("..", kq.KhoaLuuTru, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Luu_HAI_lan__cho_HAI_khoa_KHAC_nhau__khong_ghi_de()
    {
        var id = Guid.NewGuid();

        var a = await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, null, id);
        var b = await _kho.LuuAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, DinhDangMedia.Jpeg, null, id);

        // 🚨 Nếu khoá dùng **tên tệp client gửi** thì hai tệp khác nhau **ghi đè** nhau ⇒ mất ảnh của
        // người khác và **không lỗi nào**. `Guid` mới mỗi lần lưu là điều kiện để không bao giờ ghi đè.
        Assert.NotEqual(a.KhoaLuuTru, b.KhoaLuuTru);
        Assert.Equal(AnhPngThat(), await _kho.DocAsync(a.KhoaLuuTru));
    }

    [Fact]
    public async Task Doc__khoa_KHONG_ton_tai__tra_NULL__khong_nem()
    {
        // 📌 *"Tệp không còn"* là ca **có thật** (dọn nhầm, đổi máy, khôi phục backup thiếu tệp) và chỗ gọi
        // cần **phân biệt** nó với *"lỗi đọc"* để hiện ảnh thay thế.
        Assert.Null(await _kho.DocAsync("media/chung/khong-co/khong-ton-tai.png"));
    }

    [Fact]
    public async Task Xoa__IDEMPOTENT__xoa_hai_lan_khong_nem()
    {
        var kq = await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, null, Guid.NewGuid());

        await _kho.XoaAsync(kq.KhoaLuuTru);
        Assert.Null(await _kho.DocAsync(kq.KhoaLuuTru));

        // 🔒 Xoá lần hai ⇒ **KHÔNG ném**. Vì sao: xoá ảnh là thao tác người dùng **bấm lại** (double-click)
        // và **có thể chạy lại** sau lỗi mạng; ném ở lần thứ hai sẽ chặn **việc dọn dẹp** và để lại
        // **tệp mồ côi** tốn tiền lưu trữ.
        await _kho.XoaAsync(kq.KhoaLuuTru);
    }

    [Fact]
    public async Task Xoa__khoa_khong_ton_tai__khong_nem()
    {
        await _kho.XoaAsync("media/chung/khong-co/khong-ton-tai.png");
    }

    [Fact]
    public async Task Luu__noi_dung_RONG__NEM__khong_tao_tep_rac()
    {
        var loi = await Assert.ThrowsAsync<BusinessException>(
            () => _kho.LuuAsync(ReadOnlyMemory<byte>.Empty, DinhDangMedia.Png, null, Guid.NewGuid()));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh, loi.Code);
    }

    // ── (2) 🚨 CHỐNG THOÁT THƯ MỤC — nhóm quan trọng nhất ─────────────────────────────────────

    [Theory]
    [InlineData("../ngoai.txt")]
    [InlineData("../../ngoai.txt")]
    [InlineData("media/../../ngoai.txt")]
    [InlineData("media/chung/../../../ngoai.txt")]
    [InlineData("..\\ngoai.txt")]
    [InlineData("media\\..\\..\\ngoai.txt")]
    public async Task TU_CHOI__khoa_chua_hai_cham__khong_doc_ra_ngoai_thu_muc_kho(string khoaDocHai)
    {
        // 🚨 **TẤN CÔNG THẬT:** ghi một tệp **bí mật** NGAY CẠNH thư mục kho (tức ngoài `_gocTest`),
        // rồi thử đọc nó qua khoá có `..`. Nếu lớp này không chặn thì tệp bí mật **đọc được** ⇒
        // đọc được `appsettings.json`/khoá bí mật của hệ thống.
        var thuMucNgoai = Path.GetDirectoryName(_gocTest)!;
        var tenBiMat = "bi-mat-" + Guid.NewGuid().ToString("N") + ".txt";
        var duongDanBiMat = Path.Combine(thuMucNgoai, tenBiMat);
        await File.WriteAllTextAsync(duongDanBiMat, "KHONG-DUOC-DOC-DUOC");

        try
        {
            // Khoá được viết để trỏ ra tệp bí mật (nằm ngoài `media-noi-bo/`).
            var khoa = khoaDocHai.Replace("ngoai.txt", tenBiMat);

            var loi = await Assert.ThrowsAsync<BusinessException>(() => _kho.DocAsync(khoa));

            Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh, loi.Code);
        }
        finally
        {
            File.Delete(duongDanBiMat);
        }
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    // 📌 `S11` (21/10/2026) — hai dạng nữa mà `Path.IsPathRooted` KHÔNG nhận ra trên Linux:
    //    (a) ổ đĩa dùng `/`; (b) UNC `\\server\share`. Cả hai phải bị chặn trên MỌI hệ điều hành.
    [InlineData("C:/Windows/System32/drivers/etc/hosts")]
    [InlineData(@"\\server\share\bi-mat.txt")]
    public async Task TU_CHOI__khoa_la_DUONG_DAN_TUYET_DOI(string khoaTuyetDoi)
    {
        // 🔒 Đường dẫn **tuyệt đối** bỏ qua hoàn toàn gốc kho ⇒ phải chặn **trước** khi ghép.
        var loi = await Assert.ThrowsAsync<BusinessException>(() => _kho.DocAsync(khoaTuyetDoi));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh, loi.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TU_CHOI__khoa_RONG(string khoaRong)
    {
        var loi = await Assert.ThrowsAsync<BusinessException>(() => _kho.DocAsync(khoaRong));

        Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh, loi.Code);
    }

    [Fact]
    public async Task Xoa__khoa_chua_hai_cham__NEM__khong_xoa_tep_ngoai()
    {
        // 🚨 **Đường XOÁ nguy hiểm hơn đường ĐỌC:** đọc lộ dữ liệu, còn **ghi/xoá** có thể phá hệ thống.
        // Ca này kiểm: khoá có `..` ⇒ **NÉM**, và tệp ngoài vẫn **còn nguyên**.
        var thuMucNgoai = Path.GetDirectoryName(_gocTest)!;
        var ten = "khong-duoc-xoa-" + Guid.NewGuid().ToString("N") + ".txt";
        var duongDan = Path.Combine(thuMucNgoai, ten);
        await File.WriteAllTextAsync(duongDan, "phai-con-nguyen");

        try
        {
            var loi = await Assert.ThrowsAsync<BusinessException>(() => _kho.XoaAsync("../" + ten));

            Assert.Equal(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh, loi.Code);
            Assert.True(File.Exists(duongDan), "Tệp NGOÀI thư mục kho đã bị xoá ⇒ lỗ hổng thoát thư mục!");
        }
        finally
        {
            File.Delete(duongDan);
        }
    }

    [Fact]
    public async Task Luu__dat_tep_DUNG_trong_thu_muc_kho__khong_ra_ngoai()
    {
        var kq = await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, null, Guid.NewGuid());

        // 📌 **BẪY ĐÃ VẤP HAI LẦN — ghi lại vì rất dễ tái diễn:** lần đầu tôi ghép `<ContentRoot>/<khoá>`
        // trong khi lớp **cũ** tự thêm `ThuMucMacDinh` ⇒ test ĐỎ. 🔑 **Nay lớp KHÔNG tự thêm gì**: gốc do
        // **chỗ đăng ký** truyền vào (`CatalogMediaModule` ghép `ContentRootPath` + `ThuMucMacDinh`), và
        // test truyền thẳng `_gocTest` ⇒ đường dẫn thật là `<gocTest>/<khoá>`.
        // ⇒ **Bất biến của lớp này:** *gốc nhận vào CHÍNH LÀ thư mục kho* — không tự suy diễn thêm.
        // 📌 Đó cũng là điều làm lớp này **test được** không cần host giả.
        var thuMucKho = Path.GetFullPath(_gocTest);
        var duongDanDay = Path.GetFullPath(Path.Combine(thuMucKho, kq.KhoaLuuTru));

        // 🔒 Đối chứng cho các test chặn ở trên: **đường hợp lệ** phải nằm **trong** thư mục kho.
        // Nếu test này đỏ thì các test chặn kia có thể xanh vì lý do **sai** (chặn tất cả).
        Assert.StartsWith(thuMucKho, duongDanDay, StringComparison.Ordinal);
        Assert.True(File.Exists(duongDanDay));
    }

    // ── (3) TÁCH THEO TENANT ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Luu__co_va_KHONG_co_tenant__khoa_NAM_O_thu_muc_KHAC_nhau()
    {
        var id = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var coTenant = await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, tenant, id);
        var khongTenant = await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, null, id);

        // 🔒 Tách theo tenant ở **đường dẫn** là lớp phòng thủ **thứ hai**: lớp thứ nhất là mã hoá
        // `tenantId` thành thư mục riêng ⇒ kể cả khi tầng trên tra nhầm bản ghi, đường dẫn **vẫn** khác.
        Assert.Contains(tenant.ToString("N"), coTenant.KhoaLuuTru, StringComparison.Ordinal);
        Assert.Contains("chung", khongTenant.KhoaLuuTru, StringComparison.Ordinal);
        Assert.NotEqual(coTenant.KhoaLuuTru, khongTenant.KhoaLuuTru);
    }

    [Fact]
    public async Task Luu__KHONG_duoc_tao_tep_trong_thu_muc_goc_repo()
    {
        var truoc = Directory.GetFiles(_gocTest, "*", SearchOption.AllDirectories).Length;

        await _kho.LuuAsync(AnhPngThat(), DinhDangMedia.Png, null, Guid.NewGuid());

        var sau = Directory.GetFiles(_gocTest, "*", SearchOption.AllDirectories).Length;

        // 📌 Đối chứng "kho tệp **chỉ** ghi trong `ContentRoot`": số tệp **trong** gốc tăng **đúng 1**
        // (và mọi tệp sinh ra đều bị `Dispose` xoá ⇒ không để lại rác trong repo).
        Assert.Equal(truoc + 1, sau);
    }
}
