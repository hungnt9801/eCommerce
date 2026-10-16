using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Catalog.Dtos;

/// <summary>Ảnh sản phẩm trả ra API — **metadata**, ⚠️ **KHÔNG** kèm bytes (`CAT-003` `ADR-02`).</summary>
public class ProductMediaDto : EntityDto<Guid>
{
    public Guid ProductId { get; set; }

    /// <summary>
    /// 🔒 **URL PHỤC VỤ TỆP** do **máy chủ** dựng (`/CatMedia/{id}`) — ⚠️ **KHÔNG** phải URL của kho lưu trữ.
    /// 📌 Vì sao trả URL chứ không trả `KhoaLuuTru`: khoá lưu trữ là **chi tiết hiện thực** (đổi sang S3 là
    /// khoá đổi dạng); URL phục vụ là **hợp đồng** với client. 🔒 Và client **không** được tự ghép URL từ
    /// khoá — nếu được, nó có thể ghép một khoá bất kỳ và **dò** tệp của người khác.
    /// </summary>
    public string Url { get; set; } = default!;

    public string? Alt { get; set; }

    public int ThuTu { get; set; }

    public bool LaAnhChinh { get; set; }

    public long KichThuocByte { get; set; }

    /// <summary>Định dạng **đã kiểm bằng magic bytes** (`ADR-13`) — giá trị là **tên enum** (đọc được).</summary>
    public string DinhDang { get; set; } = default!;

    /// <summary>`Content-Type` **đúng** để client dùng — 🔒 suy ra từ <see cref="DinhDang"/>, **không** từ tên tệp.</summary>
    public string ContentType { get; set; } = default!;

    public DateTime CreationTime { get; set; }
}

/// <summary>
/// 🚨 **DỮ LIỆU TẢI ẢNH LÊN** — đi qua HTTP body, **KHÔNG** đi qua form field.
/// 📌 Vì sao **không** nhận `byte[]` trong một DTO JSON: ảnh **10 MB** mã hoá base64 thành **~13,3 MB**
/// văn bản (+33 %) rồi mới tới được server, và toàn bộ nằm trong **một chuỗi JSON** ⇒ vừa **tốn băng thông**
/// vừa **tốn RAM gấp đôi** (chuỗi base64 + mảng byte). 🔒 Dùng `multipart/form-data` (hoặc body nhị phân thô)
/// ⇒ bytes đi thẳng vào luồng, và **trần dung lượng được kiểm TRƯỚC khi đọc hết** (`CAT-003` §E6:
/// *"không đọc hết luồng; chặn theo `Content-Length` trước"*).
/// 📌 Trần và định dạng cho phép hiển thị ở UI **trước** khi người dùng chọn tệp (§611).
/// </summary>
public class TaiAnhInput
{
    /// <summary>Nội dung tệp. 🔒 Kiểm **trần dung lượng trước**, rồi **magic bytes** (`ADR-13`).</summary>
    [Required]
    public byte[] NoiDung { get; set; } = default!;

    /// <summary>
    /// Văn bản thay thế (SEO + trợ năng).
    /// 🔒 **BẮT BUỘC nếu đây là ảnh chính** — `ProductMedia.DatLaAnhChinh` **NÉM** khi thiếu (xem chú thích ở đó).
    /// </summary>
    [StringLength(eCommerceConsts.CatalogLimits.AltAnhToiDa)]
    public string? Alt { get; set; }

    /// <summary>
    /// Đặt luôn làm ảnh chính. 📌 Ảnh **đầu tiên** của sản phẩm **tự động** thành ảnh chính (§Basic Flow bước 4)
    /// — nếu không, sản phẩm sẽ **không có ảnh chính** cho tới khi ai đó bấm thêm một lần nữa.
    /// </summary>
    public bool LaAnhChinh { get; set; }
}

/// <summary>Sửa metadata ảnh — ⚠️ **KHÔNG** sửa được nội dung tệp (muốn đổi ảnh thì tải ảnh mới).</summary>
public class SuaAnhInput
{
    [StringLength(eCommerceConsts.CatalogLimits.AltAnhToiDa)]
    public string? Alt { get; set; }

    public int ThuTu { get; set; }
}

/// <summary>Lọc danh sách ảnh của một sản phẩm.</summary>
public class LocAnhInput : PagedAndSortedResultRequestDto
{
    public Guid ProductId { get; set; }
}
