namespace eCommerce.Catalog;

/// <summary>
/// Kết quả lưu một tệp vào kho.
/// </summary>
/// <param name="KhoaLuuTru">🔒 **Con trỏ** tới bytes (khoá trong kho lưu trữ) — lưu vào `ProductMedia.KhoaLuuTru`.
/// ⚠️ **KHÔNG** phải URL công khai: URL là việc của tầng phục vụ, và **phải** đi qua miền/kho riêng
/// (xem chú thích *giới hạn* ở `NhanDangAnh` về polyglot + XSS lưu trữ).</param>
public readonly record struct KetQuaLuuMedia(string KhoaLuuTru);

/// <summary>
/// 🚨 **HỢP ĐỒNG LƯU TRỮ TỆP** (`CAT-003` **ADR-02**) — **bytes ở object storage**, chỉ **metadata** ở PostgreSQL.
///
/// **VÌ SAO PHẢI LÀ ABSTRACTION (không gọi thẳng SDK S3/Azure vào app service):**
/// <list type="number">
///   <item>**Cấm blob trong DB** (`ADR-02`): lưu `bytea` làm **phình** bảng, `pg_dump`/backup **chậm và đắt**,
///     và vì PostgreSQL đọc theo **trang 8 KB**, đọc **1 dòng metadata** cũng kéo theo trang chứa blob
///     ⇒ **mọi** truy vấn vào bảng bị ảnh hưởng;</item>
///   <item>**Đổi nhà cung cấp không phải sửa nghiệp vụ:** với abstraction, chuyển từ đĩa cục bộ sang
///     S3/Azure chỉ là **một lớp hiện thực mới**; gọi thẳng SDK trong app service thì mọi chỗ gọi phải sửa;</item>
///   <item>**Test được mà không cần hạ tầng:** bản hiện thực trong bộ nhớ/đĩa tạm cho phép kiểm **toàn bộ**
///     đường tải lên ngay trên máy dev.</item>
/// </list>
///
/// 🔒 **BA BẤT BIẾN CỦA HỢP ĐỒNG NÀY (mỗi cái chặn một kiểu hỏng):**
/// <list type="number">
///   <item><b>`LuuAsync` PHẢI kiểm `dinhDangDaKiem` trước khi ghi</b> — hợp đồng nhận **định dạng ĐÃ KIỂM**
///     (`NhanDangAnh`), **không** nhận `Content-Type` hay tên tệp. Nếu chỗ gọi truyền thẳng dữ liệu client
///     khai thì lớp này **không thể** cứu — nên kiểu tham số được chọn để **buộc** chỗ gọi phải đi qua
///     `NhanDangAnh` trước;</item>
///   <item><b>Khoá lưu trữ do HỆ THỐNG sinh, KHÔNG do client</b> — nếu dùng tên tệp client gửi thì hai tệp
///     khác nhau **ghi đè** nhau, và kẻ tấn công **điều khiển đường dẫn** (`../../` ⇒ ghi ra ngoài thư mục,
///     hoặc ghi đè tệp hệ thống). 🔒 Hợp đồng **không** nhận tham số tên tệp — đó là chủ ý;</item>
///   <item><b>`XoaAsync` là idempotent</b> — xoá tệp **không tồn tại** là **thành công**, không ném.
///     📌 Vì sao: xoá ảnh là thao tác **người dùng bấm lại** (double-click) và **có thể chạy lại** sau lỗi mạng;
///     ném lỗi ở lần thứ hai sẽ chặn **việc dọn dẹp** và để lại **tệp mồ côi** tốn tiền lưu trữ.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** hợp đồng này **không** bảo đảm tính **nguyên tử** giữa *"ghi tệp xong"* và
/// *"ghi metadata xong"* — hai việc ở **hai hệ thống khác nhau**. 📌 Thứ tự đúng ở chỗ gọi:
/// **ghi tệp TRƯỚC, ghi DB SAU**; nếu DB hỏng thì còn **tệp mồ côi** (tốn dung lượng, **sửa được** bằng job dọn),
/// còn nếu làm ngược lại thì có **dòng DB trỏ tới tệp không tồn tại** ⇒ **ảnh hỏng** mà **không ai biết**.
/// </summary>
public interface IMediaStorage
{
    /// <summary>
    /// Lưu bytes **đã qua kiểm magic bytes** và trả **khoá lưu trữ** do hệ thống sinh.
    /// </summary>
    /// <param name="noiDung">Nội dung tệp (chỉ đọc).</param>
    /// <param name="dinhDangDaKiem">🚨 Kết quả **`NhanDangAnh.Tu(...)`** — **KHÔNG** phải `Content-Type` của client.</param>
    /// <param name="tenantId">Tenant sở hữu (để **tách** tệp giữa các tenant — không để tenant A đọc được tệp tenant B).</param>
    /// <param name="productId">Sản phẩm sở hữu (để gom cụm và dễ dọn theo sản phẩm).</param>
    Task<KetQuaLuuMedia> LuuAsync(
        ReadOnlyMemory<byte> noiDung,
        DinhDangMedia dinhDangDaKiem,
        Guid? tenantId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>Đọc lại bytes (dùng khi phục vụ tệp hoặc khi cần kiểm lại). Trả `null` nếu **không tồn tại**.</summary>
    Task<byte[]?> DocAsync(string khoaLuuTru, CancellationToken cancellationToken = default);

    /// <summary>Xoá tệp. 🔒 **Idempotent**: xoá tệp không tồn tại ⇒ **thành công** (không ném).</summary>
    Task XoaAsync(string khoaLuuTru, CancellationToken cancellationToken = default);
}
