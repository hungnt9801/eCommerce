using eCommerce.Catalog;

namespace eCommerce.Tests.Catalog;

/// <summary>
/// 🚨 **NHẬN DẠNG TỆP TẢI LÊN BẰNG MAGIC BYTES** (`CAT-003` **ADR-13**) — hàm **THUẦN**.
///
/// **VÌ SAO ĐÂY LÀ CHỐT BẢO MẬT, KHÔNG PHẢI "KIỂM DỮ LIỆU VÀO":**
/// `Content-Type` và đuôi tệp là **do client khai** ⇒ kẻ tấn công chỉ cần đổi một chuỗi là qua mọi phép kiểm
/// dựa trên chúng. Hệ quả **cụ thể**: tải lên **HTML/script độc hại** kèm `Content-Type: image/png`, hệ thống
/// lưu với tên `…png`; nếu kho tệp **phục vụ như tài liệu tĩnh** (S3 + CloudFront, `nginx` + thư mục upload —
/// rất hay gặp) thì trình duyệt **thực thi/hiển thị** nó ⇒ **XSS lưu trữ** hoặc **thực thi từ xa**.
///
/// 🔒 **Bốn nhóm được khoá ở đây:**
///   (1) **Nhận đúng** 4 định dạng ảnh được phép;
///   (2) **Từ chối** mọi thứ khác — kể cả thứ **trông giống** ảnh;
///   (3) **WebP phải kiểm HAI chữ ký ở ĐÚNG vị trí** — chỉ kiểm `RIFF` thì **WAV/AVI lọt qua** (cùng họ `RIFF`);
///   (4) **Không đọc quá mảng** và **không ném** với đầu vào ngắn/rỗng.
/// </summary>
public class NhanDangAnhTests
{
    // ── (1) NHẬN ĐÚNG 4 ĐỊNH DẠNG ĐƯỢC PHÉP ────────────────────────────────────────────────────

    public static TheoryData<byte[], DinhDangMedia> AnhHopLe => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10], DinhDangMedia.Jpeg },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00], DinhDangMedia.Png },
        { "GIF87a".Select(c => (byte)c).ToArray(), DinhDangMedia.Gif },
        { "GIF89a".Select(c => (byte)c).ToArray(), DinhDangMedia.Gif },
        { "RIFF????WEBPVP8 ".Select(c => (byte)c).ToArray(), DinhDangMedia.WebP },
    };

    [Theory]
    [MemberData(nameof(AnhHopLe))]
    public void Nhan_dung_dinh_dang_anh_duoc_phep(byte[] dauTep, DinhDangMedia mongDoi)
    {
        Assert.Equal(mongDoi, NhanDangAnh.Tu(dauTep));
    }

    // ── (2) 🚨 TỪ CHỐI MỌI THỨ KHÁC — kể cả thứ TRÔNG GIỐNG ảnh ────────────────────────────────

    [Fact]
    public void TU_CHOI__HTML_doi_ten_thanh_png()
    {
        var html = "<!DOCTYPE html><script>alert(1)</script>"u8.ToArray();

        // 🚨 ĐÂY LÀ CA TẤN CÔNG THẬT: kẻ tấn công gửi tệp này với `Content-Type: image/png` và tên `a.png`.
        // Nếu ta tin `Content-Type`/đuôi tệp thì nó **qua**, và nếu kho tệp phục vụ như tài liệu tĩnh thì
        // trình duyệt **thực thi** nó. Chữ ký nhị phân là thứ duy nhất **kẻ tấn công không đổi được**
        // nếu vẫn muốn nó là ảnh.
        Assert.Null(NhanDangAnh.Tu(html));
    }

    [Theory]
    [InlineData("<?php system($_GET['c']); ?>")]       // script phía máy chủ
    [InlineData("%PDF-1.7")]                            // PDF
    [InlineData("PK\u0003\u0004")]                      // ZIP (còn giả được .docx/.xlsx)
    [InlineData("{\"a\":1}")]                           // JSON
    [InlineData("MZ\u0090\u0000")]                      // tệp thực thi Windows (PE)
    public void TU_CHOI__cac_dinh_dang_KHONG_phai_anh(string noiDung)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(noiDung);

        Assert.Null(NhanDangAnh.Tu(bytes));
    }

    [Fact]
    public void TU_CHOI__chu_ky_dung_nhung_SAI_VI_TRI()
    {
        // PNG signature nằm ở vị trí 1 (không phải 0) ⇒ KHÔNG được nhận.
        var lech = new byte[] { 0x00, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        Assert.Null(NhanDangAnh.Tu(lech));
    }

    [Fact]
    public void TU_CHOI__chi_giong_PHAN_DAU()
    {
        // `FF D8` (thiếu byte thứ ba) — không đủ chữ ký JPEG.
        Assert.Null(NhanDangAnh.Tu([0xFF, 0xD8]));
    }

    // ── (3) 🚨 WebP: PHẢI kiểm HAI chữ ký ở ĐÚNG vị trí ───────────────────────────────────────

    [Fact]
    public void WebP__RIFF_don_thuan__KHONG_duoc_coi_la_anh()
    {
        // 🚨 **ĐÂY LÀ CA DỄ SAI NHẤT.** `RIFF` ở 0..3 là phần đầu của **WAV** và **AVI** nữa.
        // Nếu chỉ kiểm `RIFF` thì một tệp **âm thanh/video** sẽ lọt qua như "ảnh WebP" ⇒ mọi bước
        // xử lý ảnh sau đó **hỏng**, và lỗi nổi lên ở nơi không liên quan.
        var wav = "RIFF????WAVEfmt "u8.ToArray();

        Assert.Null(NhanDangAnh.Tu(wav));
    }

    [Fact]
    public void WebP__co_RIFF_va_WEBP_nhung_WEBP_SAI_VI_TRI__KHONG_nhan()
    {
        // `WEBP` ở vị trí 4 thay vì 8 ⇒ KHÔNG phải WebP hợp lệ.
        var saiViTri = "RIFFWEBP????"u8.ToArray();

        Assert.Null(NhanDangAnh.Tu(saiViTri));
    }

    [Fact]
    public void WebP__du_12_byte_va_dung_hai_vi_tri__nhan()
    {
        var webp = "RIFF\u0000\u0000\u0000\u0000WEBP"u8.ToArray();

        Assert.Equal(DinhDangMedia.WebP, NhanDangAnh.Tu(webp));
    }

    // ── (4) KHÔNG ĐỌC QUÁ MẢNG · KHÔNG NÉM ────────────────────────────────────────────────────

    [Fact]
    public void Dau_vao_RONG_hoac_NULL__tra_null__khong_nem()
    {
        Assert.Null(NhanDangAnh.Tu(null));
        Assert.Null(NhanDangAnh.Tu([]));
        Assert.Null(NhanDangAnh.Tu([0x01]));
        Assert.Null(NhanDangAnh.Tu([0x01, 0x02]));
    }

    [Fact]
    public void Mang_ngan_hon_chu_ky_khong_lam_NEM__moi_do_dai_0_den_11()
    {
        // 🔒 "Không đọc quá mảng" là **bất biến an toàn**: một `IndexOutOfRangeException` trong đường
        // tải lên sẽ là `HTTP 500` (trông như lỗi hệ thống) thay vì `403` có mã nghiệp vụ.
        for (var n = 0; n <= NhanDangAnh.SoByteCanDoc - 1; n++)
        {
            var mang = Enumerable.Repeat((byte)0xFF, n).ToArray();

            var kq = NhanDangAnh.Tu(mang);

            Assert.True(kq is null || kq == DinhDangMedia.Jpeg, $"Độ dài {n} cho kết quả lạ: {kq}");
        }
    }

    // ── (5) MIME VÀ ĐUÔI TỆP GẮN CHẶT VỚI ĐỊNH DẠNG ĐÃ NHẬN DẠNG ───────────────────────────────

    [Theory]
    [InlineData(DinhDangMedia.Jpeg, "image/jpeg", "jpg")]
    [InlineData(DinhDangMedia.Png, "image/png", "png")]
    [InlineData(DinhDangMedia.Gif, "image/gif", "gif")]
    [InlineData(DinhDangMedia.WebP, "image/webp", "webp")]
    public void Mime_va_duoi_tep__theo_dinh_dang_DA_NHAN_DANG(DinhDangMedia dinhDang, string mime, string duoi)
    {
        // 🔒 `Content-Type` **phải** lấy từ định dạng đã kiểm chữ ký — **KHÔNG** từ tên tệp/`Content-Type`
        // của client. Đây chính là lớp chặn *"HTML độc hại đội lốt .png"*: kho tệp sẽ phục vụ nó với
        // `image/png` (không phải `text/html`) ⇒ trình duyệt **không** thực thi.
        Assert.Equal(mime, NhanDangAnh.Mime(dinhDang));
        Assert.Equal(duoi, NhanDangAnh.DuoiTep(dinhDang));
    }

    [Fact]
    public void Mime__dinh_dang_ngoai_danh_sach__NEM__khong_tra_chuoi_doan_mo()
    {
        // 📌 Thà ném ra **ngay tại chỗ** còn hơn trả về một `Content-Type` mặc định nào đó —
        // mặc định sai trong đường phục vụ tệp là **mở lại** đúng lỗ hổng mà lớp này tồn tại để chặn.
        Assert.Throws<ArgumentOutOfRangeException>(() => NhanDangAnh.Mime((DinhDangMedia)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => NhanDangAnh.DuoiTep((DinhDangMedia)999));
    }

    [Fact]
    public void SoByteCanDoc__DU_cho_moi_dinh_dang_trong_danh_sach()
    {
        // 🔒 Bất biến cho chỗ gọi: đọc `SoByteCanDoc` byte đầu là **đủ** để nhận dạng mọi định dạng.
        // Nếu thêm định dạng có chữ ký **dài hơn** mà quên nâng hằng số này thì định dạng đó
        // **âm thầm** không bao giờ được nhận ra (tệp hợp lệ bị từ chối) — test này sẽ đỏ.
        var webp = "RIFF\u0000\u0000\u0000\u0000WEBP"u8.ToArray();
        Assert.True(webp.Length <= NhanDangAnh.SoByteCanDoc);

        Assert.Equal(DinhDangMedia.WebP, NhanDangAnh.Tu(webp.AsSpan(0, NhanDangAnh.SoByteCanDoc).ToArray()));
    }
}
