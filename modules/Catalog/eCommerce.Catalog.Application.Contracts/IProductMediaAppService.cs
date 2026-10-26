using eCommerce.Catalog.Dtos;
using Volo.Abp.Application.Dtos;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **QUẢN LÝ ẢNH SẢN PHẨM** (`CAT-003` §6.1) — tải lên · sửa metadata · đặt ảnh chính · xoá.
///
/// 🔐 Quyền `Products.ManageMedia` (xem chú thích ở `eCommerceConsts.Permissions.Products.ManageMedia`
/// về **vì sao tách khỏi `Edit`**). ⚠️ Định nghĩa permission ở **provider** là **bắt buộc** — thiếu ⇒
/// `403` cho **mọi** người kể cả admin (`XC-004` D2/E3).
///
/// 📌 **Hợp đồng này KHÔNG có đường sửa NỘI DUNG tệp** — muốn đổi ảnh thì **tải ảnh mới rồi xoá ảnh cũ**.
/// 🔒 Vì sao: đổi nội dung tại chỗ làm **cùng một URL** trả **hai ảnh khác nhau** theo thời gian ⇒
/// CDN/cache của sàn và của máy tìm kiếm giữ **ảnh cũ**, và **không có cách nào** biết ai đang thấy ảnh nào.
/// </summary>
public interface IProductMediaAppService
{
    /// <summary>Danh sách ảnh của một sản phẩm, sắp theo `ThuTu`.</summary>
    Task<PagedResultDto<ProductMediaDto>> GetDanhSachAsync(LocAnhInput input);

    /// <summary>
    /// 🚨 **TẢI ẢNH LÊN** — kiểm **trần dung lượng** (`NFR-CAT-003-003`: 10 MB) rồi **magic bytes**
    /// (`ADR-13`) rồi **trần số ảnh** (`NFR-CAT-003-004`: 50/sản phẩm), ghi bytes qua `IMediaStorage`,
    /// rồi tạo `ProductMedia`.
    ///
    /// 🔒 **THỨ TỰ KIỂM LÀ MỘT PHẦN CỦA ĐẶC TẢ:** trần dung lượng **TRƯỚC** magic bytes — vì đọc hết một tệp
    /// 2 GB để rồi phát hiện nó không phải ảnh là **tự làm mất dịch vụ** (đúng họ lỗi `DoS`).
    /// 📌 Trần số ảnh kiểm **trước khi ghi tệp** ⇒ vượt trần thì **không** để lại tệp mồ côi.
    ///
    /// 📌 **Ảnh ĐẦU TIÊN tự động thành ảnh chính** (§Basic Flow bước 4): nếu không, sản phẩm sẽ **không có
    /// ảnh chính** cho tới khi ai đó bấm thêm một lần nữa — và `Publish` (đòi ≥ 1 ảnh chính) sẽ **chặn**
    /// vì một lý do **không ai đoán được**.
    /// </summary>
    Task<ProductMediaDto> PostTaiAnhAsync(Guid productId, TaiAnhInput input);

    /// <summary>Sửa `Alt` + `ThuTu`. ⚠️ Không đổi được nội dung tệp.</summary>
    Task<ProductMediaDto> PutAnhAsync(Guid id, SuaAnhInput input);

    /// <summary>
    /// 🚨 **ĐẶT LÀM ẢNH CHÍNH** — **một transaction**: bỏ cờ ở ảnh chính cũ → đặt cờ ở ảnh này.
    /// 🔒 Hai việc **phải** cùng transaction vì unique index có bộ lọc ở DB chỉ cho **một** ảnh chính;
    /// làm rời nhau thì giữa hai bước **không có** ảnh chính nào (storefront hiện **ảnh trống**), hoặc
    /// bước thứ hai **va ràng buộc** ⇒ `0204` cho một thao tác **hoàn toàn hợp lệ**.
    /// </summary>
    Task<ProductMediaDto> PostDatAnhChinhAsync(Guid id);

    /// <summary>
    /// Xoá ảnh (xoá **mềm** — `ADR-11`). 🔒 **KHÔNG** xoá bytes trong cùng request: URL ảnh **đã** phát ra
    /// storefront/sàn/CDN, xoá bytes ngay ⇒ **ảnh vỡ** ở nơi chưa kịp cập nhật, và **không có log**.
    /// Bytes do job dọn xử lý sau (chưa làm — xem `CAT-003` §20.7).
    /// </summary>
    Task XoaAnhAsync(Guid id);
}
