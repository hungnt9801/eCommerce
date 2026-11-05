using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **ẢNH CỦA SẢN PHẨM** (`CatProductMedia`) — `CAT-003`.
///
/// **BA BẤT BIẾN ĐỊNH HÌNH THIẾT KẾ (đọc trước khi sửa):**
/// <list type="number">
///   <item>🚨 **`ADR-02`: metadata ở PostgreSQL, BYTES ở object storage** — entity này **KHÔNG** có cột
///     `byte[]`/`bytea` nào. Lưu blob trong DB làm **phình** bảng (mỗi ảnh vài trăm KB × N ảnh × N sản phẩm),
///     khiến `pg_dump`/backup **chậm và đắt**, và **mọi** truy vấn vào bảng bị ảnh hưởng vì
///     PostgreSQL đọc theo **trang 8 KB** ⇒ đọc 1 dòng metadata cũng kéo theo trang chứa blob.
///     🔒 `KhoaLuuTru` là **con trỏ** tới bytes; `IMediaStorage` là hợp đồng duy nhất chạm tới bytes.</item>
///   <item>🚨 **`ADR-12`: mỗi sản phẩm TỐI ĐA MỘT ảnh chính** (`LaAnhChinh = true`). Bất biến này được
///     cưỡng chế ở **DB** bằng unique index **có bộ lọc** (`WHERE "LaAnhChinh"`), không chỉ ở tầng ứng dụng —
///     vì hai request đồng thời (đặt ảnh chính) sẽ **cùng** qua được kiểm tra ở tầng ứng dụng.</item>
///   <item>🚨 **`ADR-13`: định dạng tệp do MÁY CHỦ quyết định** từ **magic bytes** (`NhanDangAnh`), không từ
///     `Content-Type`/đuôi tệp mà client khai. Cột <see cref="DinhDangDaKiem"/> lưu kết quả **đã kiểm**;
///     khi phục vụ tệp, `Content-Type` **phải** lấy từ đây (qua `NhanDangAnh.Mime`) — xem chú thích ở
///     `NhanDangAnh` về **XSS lưu trữ**.</item>
/// </list>
/// </summary>
public class ProductMedia : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Độ dài tối đa của `Alt` (văn bản thay thế — **bắt buộc** cho ảnh chính: SEO + trợ năng).</summary>
    public const int DoDaiAltToiDa = 512;

    /// <summary>Độ dài tối đa của khoá lưu trữ — khớp `HasMaxLength(512)`.</summary>
    public const int DoDaiKhoaLuuTruToiDa = 512;

    public Guid? TenantId { get; protected set; }

    public Guid ProductId { get; protected set; }

    /// <summary>
    /// 🔒 **CON TRỎ tới bytes** trong kho lưu trữ (`IMediaStorage`). ⚠️ **KHÔNG** phải URL công khai:
    /// URL công khai là việc của **tầng phục vụ** (và **phải** đi qua miền/kho riêng — xem `NhanDangAnh` §giới hạn).
    /// 📌 Lưu khoá (không lưu URL) để **đổi nhà cung cấp lưu trữ** không phải sửa dữ liệu.
    /// </summary>
    public string KhoaLuuTru { get; protected set; } = default!;

    /// <summary>
    /// 🚨 **ĐỊNH DẠNG ĐÃ KIỂM BẰNG MAGIC BYTES** (`ADR-13`).
    /// 🔒 Khi phục vụ tệp, `Content-Type` **phải** suy ra từ đây — **KHÔNG** từ tên tệp hay từ header client gửi.
    /// 📌 Lưu **enum** (không lưu chuỗi MIME thô) ⇒ chỉ có **một** nguồn cho `Content-Type`, và thêm định dạng
    /// mới là **thay đổi có kiểm soát** ở `NhanDangAnh`.
    /// </summary>
    public DinhDangMedia DinhDangDaKiem { get; protected set; }

    /// <summary>Kích thước tệp (byte) — **đã kiểm** ở đường tải lên (`ADR-11`: có trần dung lượng).</summary>
    public long KichThuocByte { get; protected set; }

    /// <summary>Văn bản thay thế. 🔒 **BẮT BUỘC khi là ảnh chính** — xem <see cref="DatLaAnhChinh"/>.</summary>
    public string? Alt { get; protected set; }

    /// <summary>Thứ tự hiển thị (nhỏ trước). Dùng `int` (không `double`) để thứ tự **tất định** giữa các lần đọc.</summary>
    public int ThuTu { get; protected set; }

    /// <summary>
    /// 🚨 **ẢNH CHÍNH** — tối đa **MỘT** cho mỗi sản phẩm (`ADR-12`, cưỡng chế ở DB bằng unique index có lọc).
    /// ⚠️ `protected set` ⇒ **không** gán thẳng từ ngoài: đổi cờ này **phải** đi qua <see cref="DatLaAnhChinh"/>
    /// (để cưỡng chế luật *"ảnh chính phải có `Alt`"*). 📌 Cùng khuôn `Product.Slug` (`CAT-003` §16.3):
    /// một đường ghi duy nhất thì luật **chỉ có một chỗ**.
    /// </summary>
    public bool LaAnhChinh { get; protected set; }

    protected ProductMedia() { }

    public ProductMedia(
        Guid id,
        Guid productId,
        string khoaLuuTru,
        DinhDangMedia dinhDangDaKiem,
        long kichThuocByte,
        string? alt = null,
        int thuTu = 0) : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(khoaLuuTru))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(khoaLuuTru))
                .WithData("lyDo", "Khoá lưu trữ rỗng ⇒ không có cách nào lấy lại bytes.");
        }

        if (khoaLuuTru.Trim().Length > DoDaiKhoaLuuTruToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(khoaLuuTru)).WithData("toiDa", DoDaiKhoaLuuTruToiDa);
        }

        if (kichThuocByte <= 0)
        {
            // ⚠️ `0` byte là ca thật (tải lên tệp rỗng) và nó **luôn** là lỗi: tệp rỗng **không thể** có
            // magic bytes hợp lệ ⇒ mã `TepKhongPhaiAnh` (chỉ dẫn đúng: *"chọn tệp khác"*).
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh)
                .WithData("kichThuocByte", kichThuocByte)
                .WithData("lyDo", "Tệp rỗng (0 byte) không thể là ảnh hợp lệ.");
        }

        ProductId = productId;
        KhoaLuuTru = khoaLuuTru.Trim();
        DinhDangDaKiem = dinhDangDaKiem;
        KichThuocByte = kichThuocByte;
        ThuTu = thuTu;

        if (!string.IsNullOrWhiteSpace(alt))
        {
            DatAlt(alt);
        }
    }

    /// <summary>Đặt văn bản thay thế. Rỗng ⇒ **xoá** (`null`) — không lưu chuỗi khoảng trắng.</summary>
    public void DatAlt(string? alt)
    {
        var chuan = alt?.Trim();

        if (!string.IsNullOrEmpty(chuan) && chuan.Length > DoDaiAltToiDa)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(alt)).WithData("toiDa", DoDaiAltToiDa);
        }

        Alt = string.IsNullOrEmpty(chuan) ? null : chuan;
    }

    public void DatThuTu(int thuTu) => ThuTu = thuTu;

    /// <summary>
    /// 🚨 **ĐẶT LÀM ẢNH CHÍNH** — cưỡng chế luật: **ảnh chính PHẢI có `Alt`**.
    ///
    /// **VÌ SAO `Alt` BẮT BUỘC CHO ẢNH CHÍNH (không phải "cho đẹp"):** ảnh chính là ảnh hiện ở **danh sách
    /// sản phẩm**, **thẻ chia sẻ mạng xã hội**, và **kết quả tìm kiếm ảnh**. Thiếu `Alt`:
    /// <list type="bullet">
    ///   <item>**máy tìm kiếm** không có mô tả ⇒ mất một kênh tiếp cận (đúng mục tiêu SEO của `CAT-003`);</item>
    ///   <item>**trình đọc màn hình** đọc URL tệp ⇒ người dùng khiếm thị **không biết** ảnh là gì — đây là
    ///     **nghĩa vụ trợ năng**, không phải tuỳ chọn.</item>
    /// </list>
    /// 📌 Vì vậy luật được đặt ở **đường DUY NHẤT** đổi cờ (`LaAnhChinh` là `protected set`), không ở app service:
    /// app service có thể có **nhiều** đường (tải lên, sửa, nhập từ file) và đường nào quên là **im lặng** sai.
    ///
    /// ⚠️ **KHÔNG** tự bỏ đánh dấu ảnh chính **cũ** ở đây: entity này **không** biết các ảnh khác của sản phẩm
    /// (chúng là **aggregate khác**). Việc bỏ đánh dấu cũ thuộc **app service**, **trong cùng transaction** —
    /// và nếu quên thì **unique index có lọc ở DB** sẽ chặn (đó là lý do index tồn tại, xem `ADR-12`).
    /// </summary>
    public void DatLaAnhChinh(bool laAnhChinh)
    {
        if (laAnhChinh && string.IsNullOrWhiteSpace(Alt))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(Alt))
                .WithData("lyDo", "ẢNH CHÍNH bắt buộc có `Alt` (SEO + trợ năng: trình đọc màn hình dùng `Alt`).");
        }

        LaAnhChinh = laAnhChinh;
    }
}
