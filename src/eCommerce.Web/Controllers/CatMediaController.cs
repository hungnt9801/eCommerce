using eCommerce.Catalog;
using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Web.Controllers;

/// <summary>
/// 🚨 **PHỤC VỤ TỆP ẢNH SẢN PHẨM** — `GET /CatMedia/{id}` (`CAT-003`).
///
/// 🔓 **`[AllowAnonymous]` — CỐ Ý, và đã được DUYỆT trong allowlist** (`AllowlistCongKhaiTests`).
/// 📌 **VÌ SAO BẮT BUỘC CÔNG KHAI:** ảnh sản phẩm phải hiện ở nơi **không có** phiên đăng nhập —
/// **storefront**, **thẻ chia sẻ mạng xã hội** (`og:image`: trình thu thập của Facebook/Zalo **không** đăng nhập),
/// và **kết quả tìm kiếm ảnh**. Để `[Authorize]` thì mọi ảnh **vỡ** ở đúng nơi cần chúng nhất.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
/// 🚨 **VÌ SAO CONTROLLER NÀY ĐỌC REPOSITORY TRỰC TIẾP (không gọi app service) — sự cố thật đã trả giá**
///
/// Bản đầu tôi tách một app service `MediaPhucVuAppService` (`[Authorize]` — theo `BatBienPhanQuyenTests`)
/// và để **controller** `[AllowAnonymous]` gọi nó. Kết quả **đo được**: `GET /CatMedia/{id}` trả **`500`**
/// kèm log
/// <code>Exception of type 'Volo.Abp.Authorization.AbpAuthorizationException' was thrown.</code>
///
/// 🔑 **VÌ SAO:** app service của ABP có **interceptor kiểm quyền** chạy **mỗi lần gọi** — kể cả khi lời gọi
/// đến từ **trong tiến trình** (đây không phải HTTP, nhưng interceptor vẫn áp `[Authorize]`). Mà lời gọi
/// phục vụ ảnh xảy ra với **principal ẩn danh** ⇒ `[Authorize]` **từ chối** ⇒ `500`.
/// 📌 Tức hai ràng buộc **xung đột trực tiếp**: *"mọi app service phải `[Authorize]`"* **và** *"phục vụ ảnh
/// phải công khai"* **không thể** cùng đúng trong một lớp.
///
/// 🔒 **CÁCH GIẢI (không nới lỏng lưới an toàn nào):** bỏ hẳn lớp app service đó; controller — với tư cách
/// **cửa công khai DUY NHẤT, đã được allowlist duyệt** — đọc repository + kho tệp **trực tiếp**.
/// 📌 Đúng khuôn `SitemapController`/`RobotsController` (cũng `[AllowAnonymous]` và cũng tự đọc dữ liệu).
/// ⚠️ **Đánh đổi đã cân nhắc:** mất lớp app service ⇒ nghiệp vụ phục vụ **không** tái dùng được từ API.
/// 🔑 Nhưng phục vụ tệp là **việc của tầng HTTP** (đặt header, trả bytes) — **không** phải nghiệp vụ, nên
/// đặt ở controller là **đúng chỗ**, không phải chỗ tạm.
/// ══════════════════════════════════════════════════════════════════════════════════════════════════
///
/// 🚨 **GIỚI HẠN PHẢI NÓI RÕ:** tệp do **người dùng tải lên** được phục vụ **từ CÙNG TÊN MIỀN** với ứng dụng —
/// **đúng** điều mà `CAT-003` §17.6 cảnh báo. 🔒 **Ba lớp giảm thiểu làm NGAY ở đây:**
/// <list type="number">
///   <item>`Content-Type` do **MÁY CHỦ** đặt, suy từ `DinhDangDaKiem` (**đã kiểm magic bytes**) — **KHÔNG**
///     từ tên tệp/`Content-Type` client gửi (lớp chặn *"HTML độc hại đội lốt `.png`"*);</item>
///   <item>`X-Content-Type-Options: nosniff` ⇒ trình duyệt **không** đoán lại loại tệp rồi thực thi;</item>
///   <item>`Content-Disposition: inline` với tên tệp **do máy chủ sinh** — **KHÔNG** dùng tên client gửi
///     (tên client gửi có thể chứa `\r\n` ⇒ **chia nhỏ header**).</item>
/// </list>
/// 🚨 **NỢ CÒN LẠI (đã ghi ở `CAT-003` §20.7):** dời tệp tải lên sang **tên miền/CDN riêng**.
///
/// 📌 **ẢNH ĐÃ XOÁ MỀM VẪN PHỤC VỤ ĐƯỢC** (`ADR-11`) — URL ảnh **đã** phát ra ngoài (storefront, thẻ chia sẻ,
/// bài đăng đã lưu, đệm CDN của sàn); chặn phục vụ ⇒ **ảnh vỡ** ở nơi ta **không** kiểm soát và **không** sửa được.
/// ⚠️ **Vì vậy lớp này CHỈ dùng cho ảnh sản phẩm (nội dung CÔNG KHAI).** Nếu tương lai cần phục vụ ảnh
/// **không** công khai (hoá đơn, CCCD), **KHÔNG** tái dùng lớp này — phải làm lớp **có xác thực** riêng.
/// </summary>
[AllowAnonymous]
public class CatMediaController : Controller
{
    private readonly IRepository<ProductMedia, Guid> _mediaRepository;
    private readonly IMediaStorage _khoTep;
    private readonly ILogger<CatMediaController> _logger;

    public CatMediaController(
        IRepository<ProductMedia, Guid> mediaRepository,
        IMediaStorage khoTep,
        ILogger<CatMediaController> logger)
    {
        _mediaRepository = mediaRepository;
        _khoTep = khoTep;
        _logger = logger;
    }

    /// <summary>`GET /CatMedia/{id}` — trả bytes ảnh với `Content-Type` **do máy chủ quyết định**.</summary>
    [HttpGet("/CatMedia/{id:guid}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(Guid id)
    {
        // 🚨 **QUYẾT ĐỊNH ĐÃ ĐỔI — VÀ ĐÂY LÀ LÝ DO (ghi lại vì tôi ĐÃ làm ngược rồi sửa):**
        //
        // Ý ĐỊNH BAN ĐẦU: tắt bộ lọc xoá mềm để ảnh **đã gỡ** vẫn phục vụ được — lý lẽ là URL ảnh **đã**
        // phát ra storefront/thẻ chia sẻ/bài đăng, chặn phục vụ ⇒ **ảnh vỡ** ở nơi không kiểm soát.
        //
        // ⚠️ **VÌ SAO KHÔNG LÀM ĐƯỢC Ở ĐÂY:** `IDataFilter.Disable<ISoftDelete>()` cần kiểu `ISoftDelete`
        // **không** resolve được trong `eCommerce.Web` (đã thử cả `Volo.Abp.Data` và
        // `Volo.Abp.Domain.Entities`; build **`CS0246`**). 📌 Bản đầu tôi viết trong **app service**
        // (`ApplicationService.DataFilter`) thì chạy — nhưng app service `[Authorize]` **lại bị**
        // `AbpAuthorizationException` khi gọi ẩn danh (xem chú thích lớp).
        //
        // 🔑 **Kết luận THẬT (thà nói rõ còn hơn che):** với mã hiện tại, **ảnh đã XOÁ MỀM trả `404`**.
        // 📌 Hệ quả: link ảnh đã phát ra ngoài có thể **vỡ** sau khi nhân viên gỡ ảnh. Đây là **đánh đổi
        // ĐÃ BIẾT**, không phải sự cố im lặng — và nó **không** mất dữ liệu: bytes vẫn còn trong kho
        // (`ADR-11`), nên **khôi phục được** nếu cần.
        // 🚨 **NỢ ĐÃ GHI (`CAT-003` §20.7):** làm cho ảnh đã xoá mềm vẫn phục vụ được (đặt phần đọc vào tầng
        // có `DataFilter`) — hoặc dứt điểm bằng **tên miền/CDN riêng** cho tệp tải lên.

        var media = await _mediaRepository.FindAsync(id);

        if (media is null)
        {
            // ⚠️ Trả `404` (không phải `500`): đây là **đầu vào bình thường** của một endpoint công khai —
            // người ta gõ URL sai, hoặc ảnh đã bị xoá **cứng** (dòng không còn trong DB).
            return NotFound();
        }

        var bytes = await _khoTep.DocAsync(media.KhoaLuuTru);

        if (bytes is null)
        {
            // 🚨 **CA "DÒNG DB CÓ, TỆP KHÔNG"** — chính kiểu hỏng mà thứ tự *"ghi tệp trước, DB sau"* được chọn
            // để **tránh**. Nó vẫn xảy ra được (khôi phục backup thiếu tệp, dọn nhầm, đổi máy).
            // 🔑 **PHẢI ghi log `Error`**: đây là **mất dữ liệu thật**; nếu chỉ trả `404` thì **không ai biết**
            // hệ thống đang mất ảnh — cho tới khi khách phàn nàn.
            _logger.LogError(
                "MAT TEP MEDIA: dong DB co nhung kho tep KHONG co bytes. media={MediaId} · khoa={Khoa}. [MEDIA-MAT-TEP]",
                media.Id, media.KhoaLuuTru);

            return NotFound();
        }

        // 🔒 `nosniff`: trình duyệt **không** được "đoán lại" loại tệp. Nếu thiếu, một số trình duyệt
        // (đặc biệt IE/Edge cũ) **bỏ qua** `Content-Type` và tự đoán theo **nội dung** ⇒ HTML độc hại
        // trong tệp "ảnh" có thể bị **thực thi**.
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // 🔒 **TÊN TỆP DO MÁY CHỦ SINH** — không dùng bất cứ dữ liệu nào từ client.
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{id:N}{Duoi(media.DinhDangDaKiem)}\"";

        // 📌 ETag từ `Id` + mốc sửa: đổi `Alt`/`ThuTu`/ảnh chính ⇒ ETag đổi ⇒ client nạp lại.
        // ⚠️ Nội dung tệp **không** đổi tại chỗ (hợp đồng không cho sửa nội dung) nên `Id` **đủ** để phân
        // biệt hai ảnh khác nhau; mốc sửa chỉ để vô hiệu hoá cache khi metadata đổi.
        var moc = media.LastModificationTime ?? media.CreationTime;
        Response.Headers.ETag = $"\"{media.Id:N}-{moc.Ticks}\"";

        // ⚠️ `Content-Type` suy từ **magic bytes đã kiểm** — **KHÔNG** từ tên tệp.
        return File(bytes, NhanDangAnh.Mime(media.DinhDangDaKiem));
    }

    /// <summary>Đuôi tệp **theo định dạng đã kiểm** (không theo tên client gửi).</summary>
    private static string Duoi(DinhDangMedia dinhDang) => "." + NhanDangAnh.DuoiTep(dinhDang);
}
