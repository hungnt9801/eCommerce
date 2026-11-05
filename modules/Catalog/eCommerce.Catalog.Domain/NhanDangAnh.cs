namespace eCommerce.Catalog;

/// <summary>Định dạng tệp media mà hệ thống **chấp nhận** (`CAT-003` §Mô hình dữ liệu).</summary>
public enum DinhDangMedia
{
    /// <summary>JPEG (`FF D8 FF`).</summary>
    Jpeg = 0,

    /// <summary>PNG (`89 50 4E 47 0D 0A 1A 0A`).</summary>
    Png = 1,

    /// <summary>GIF (`GIF87a` / `GIF89a`).</summary>
    Gif = 2,

    /// <summary>WebP (`RIFF`…`WEBP`).</summary>
    WebP = 3
}

/// <summary>
/// 🚨 **NHẬN DẠNG TỆP BẰNG CHỮ KÝ NHỊ PHÂN (MAGIC BYTES)** — `CAT-003` **ADR-13**. Hàm **THUẦN**.
///
/// **VÌ SAO KHÔNG TIN `Content-Type`/ĐUÔI TỆP TỪ CLIENT (đây là lỗ hổng bảo mật, không phải "chuyện đẹp"):**
/// `Content-Type` và đuôi tệp là **do client khai** — kẻ tấn công chỉ cần đổi một chuỗi là qua được mọi
/// phép kiểm dựa trên chúng. Hệ quả **cụ thể**:
/// <list type="bullet">
///   <item>tải lên một **script/PHP/HTML** độc hại kèm `Content-Type: image/png` ⇒ hệ thống lưu vào kho tệp
///     với tên `…png`. Nếu kho tệp **phục vụ tệp như tài liệu tĩnh** (rất hay gặp: S3 + CloudFront,
///     `nginx` + thư mục upload), trình duyệt/khách truy cập sẽ **thực thi hoặc hiển thị** nó như
///     HTML/script ⇒ **XSS lưu trữ (stored XSS)** hoặc **thực thi từ xa**;</item>
///   <item>hoặc đơn giản: tệp không phải ảnh ⇒ **mọi** bước xử lý sau (cắt/nén/đổi kích cỡ) **hỏng**,
///     và lỗi nổi lên ở **nơi không liên quan** (khó truy).</item>
/// </list>
/// 📌 Nội dung tệp thì **kẻ tấn công không đổi được** nếu vẫn muốn nó là ảnh — nên chữ ký nhị phân là
/// thứ duy nhất **đáng tin** để quyết định *"đây có phải ảnh và là ảnh loại gì"*.
///
/// 🔒 **Thiết kế của lõi này:**
/// <list type="number">
///   <item>**Chỉ ĐỌC VÀI BYTE ĐẦU** — không nạp cả tệp vào bộ nhớ. Một người dùng có thể gửi tệp **vài GB**;
///     đọc hết để kiểm là **tự làm mất dịch vụ** (đúng họ lỗi `DoS`);</item>
///   <item>**Chỉ nhận 4 định dạng ẢNH** (`Jpeg`/`Png`/`Gif`/`WebP`) — danh sách **đóng**:
///     thêm định dạng là **quyết định có chủ ý**, không phải chuyện tự nhiên xảy ra;</item>
///   <item>**KHÔNG dựa vào việc tệp "trông giống" ảnh** — chỉ chữ ký đúng ở **đúng vị trí** mới qua;
///   <item>**Trả `null` khi không nhận ra** (không ném) ⇒ chỗ gọi **quyết định** mã lỗi nghiệp vụ, và
///     hàm này **test được mọi nhánh** không cần I/O.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT (nói thẳng, đừng tưởng nhầm là "đã an toàn tuyệt đối"):** chữ ký chỉ chứng minh
/// **phần đầu tệp** đúng định dạng. Một tệp **polyglot** (đầu là GIF hợp lệ, phần sau là HTML/script) **vẫn qua**
/// được phép kiểm này.
/// ⇒ 🔒 **Phòng thủ nhiều lớp (bắt buộc, không thay thế được):**
///   (a) kho tệp **phải** phục vụ với `Content-Type` do **MÁY CHỦ** đặt (từ `DinhDang` đã nhận dạng, **không**
///       phải từ tên tệp) và kèm `X-Content-Type-Options: nosniff`;
///   (b) **không** phục vụ tệp tải lên từ **cùng tên miền** với ứng dụng (dùng miền/kho riêng) ⇒ dù có
///       thực thi được cũng **không** đọc được cookie/phiên của miền chính.
/// 📌 Ghi ở đây để người sau **không** đọc "đã kiểm magic bytes" thành "đã an toàn".
/// </summary>
public static class NhanDangAnh
{
    /// <summary>Số byte tối thiểu cần để nhận dạng **mọi** định dạng trong danh sách (WebP cần 12).</summary>
    public const int SoByteCanDoc = 12;

    private static readonly byte[] ChuKyJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ChuKyPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ChuKyGif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] ChuKyGif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] ChuKyRiff = "RIFF"u8.ToArray();
    private static readonly byte[] ChuKyWebP = "WEBP"u8.ToArray();

    /// <summary>
    /// Nhận dạng ảnh từ **vài byte đầu** của tệp.
    /// 🔒 Trả `null` nếu: `null`/rỗng · **ngắn hơn** chữ ký cần · hoặc không khớp định dạng nào.
    /// 📌 **Không ném** — *"tệp lạ"* là **đầu vào bình thường** của một endpoint tải lên, không phải lỗi lập trình.
    /// </summary>
    public static DinhDangMedia? Tu(byte[]? dauTep)
    {
        if (dauTep is null || dauTep.Length < ChuKyJpeg.Length)
        {
            return null;
        }

        // ⚠️ THỨ TỰ KIỂM: JPEG trước (3 byte) để tệp ngắn vẫn phân loại được; mỗi phép kiểm đều tự
        // kiểm **độ dài** của nó ⇒ không bao giờ đọc quá mảng.
        if (BatDauBang(dauTep, ChuKyJpeg))
        {
            return DinhDangMedia.Jpeg;
        }

        if (BatDauBang(dauTep, ChuKyPng))
        {
            return DinhDangMedia.Png;
        }

        if (BatDauBang(dauTep, ChuKyGif87) || BatDauBang(dauTep, ChuKyGif89))
        {
            return DinhDangMedia.Gif;
        }

        // 🔒 WebP: `RIFF` ở 0..3 **VÀ** `WEBP` ở **8..11** — hai chữ ký **cách nhau**, nên phải kiểm cả hai
        // **ở đúng vị trí**. Chỉ kiểm `RIFF` là **sai**: `RIFF` còn là phần đầu của **WAV/AVI** (âm thanh/video)
        // ⇒ tệp âm thanh sẽ lọt qua như "ảnh WebP".
        if (dauTep.Length >= SoByteCanDoc
            && BatDauBang(dauTep, ChuKyRiff)
            && BatDauBang(dauTep, ChuKyWebP, viTri: 8))
        {
            return DinhDangMedia.WebP;
        }

        return null;
    }

    /// <summary>
    /// **Tên loại MIME** theo định dạng **ĐÃ NHẬN DẠNG** — dùng để đặt `Content-Type` khi phục vụ tệp.
    /// 🔒 **VÌ SAO PHẢI LẤY TỪ ĐÂY (không từ tên tệp/`Content-Type` của client):** xem chú thích lớp —
    /// tin client là **mở đường XSS lưu trữ**. Hàm này gắn chặt `Content-Type` với **chữ ký nhị phân đã kiểm**.
    /// </summary>
    public static string Mime(DinhDangMedia dinhDang) => dinhDang switch
    {
        DinhDangMedia.Jpeg => "image/jpeg",
        DinhDangMedia.Png => "image/png",
        DinhDangMedia.Gif => "image/gif",
        DinhDangMedia.WebP => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(dinhDang), dinhDang, "Định dạng media chưa có MIME.")
    };

    /// <summary>Đuôi tệp **CHUẨN** theo định dạng đã nhận dạng (dùng khi sinh khoá lưu trữ).</summary>
    public static string DuoiTep(DinhDangMedia dinhDang) => dinhDang switch
    {
        DinhDangMedia.Jpeg => "jpg",
        DinhDangMedia.Png => "png",
        DinhDangMedia.Gif => "gif",
        DinhDangMedia.WebP => "webp",
        _ => throw new ArgumentOutOfRangeException(nameof(dinhDang), dinhDang, "Định dạng media chưa có đuôi tệp.")
    };

    /// <summary>So khớp tiền tố — tự kiểm độ dài, **không** ném khi mảng ngắn.</summary>
    private static bool BatDauBang(byte[] nguon, byte[] chuKy, int viTri = 0)
    {
        if (nguon.Length < viTri + chuKy.Length)
        {
            return false;
        }

        for (var i = 0; i < chuKy.Length; i++)
        {
            if (nguon[viTri + i] != chuKy[i])
            {
                return false;
            }
        }

        return true;
    }
}
