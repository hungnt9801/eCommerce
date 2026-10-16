using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.IO;

namespace eCommerce.Catalog;

/// <summary>
/// 🚨 **KHO TỆP TRÊN ĐĨA CỤC BỘ** — hiện thực <see cref="IMediaStorage"/> (`CAT-003` **ADR-02**).
///
/// **VÌ SAO ĐĨA CỤC BỘ LÀ BƯỚC ĐẦU ĐÚNG (không nhảy thẳng lên S3):**
/// hợp đồng `IMediaStorage` đã tách **nghiệp vụ** khỏi **nơi lưu** ⇒ bắt đầu bằng đĩa cho phép **kiểm
/// toàn bộ** đường tải lên/đọc/xoá **ngay trên máy dev**, không cần tài khoản/credential cloud, không cần
/// mạng. 📌 Khi lên thật, thay bằng bản S3/Azure **không phải sửa một dòng** nghiệp vụ nào.
/// 🔒 Và điều này **KHÔNG** vi phạm `ADR-02`: bytes nằm **ngoài** PostgreSQL, bảng chỉ giữ **metadata**.
///
/// **BA BẤT BIẾN AN TOÀN CỦA LỚP NÀY (mỗi cái chặn một kiểu tấn công/ hỏng):**
/// <list type="number">
///   <item><b>KHOÁ DO HỆ THỐNG SINH, KHÔNG do client</b> — khoá là `media/{tenant}/{product}/{guid}.{ext}`
///     với `guid` **mới** mỗi lần lưu. 📌 Nếu dùng tên tệp client gửi thì hai tệp khác nhau **ghi đè** nhau
///     (mất ảnh của người khác, **không lỗi nào**), và client **điều khiển đường dẫn**;</item>
///   <item><b>CHẶN THOÁT THƯ MỤC (`../`)** — mọi khoá đi qua <see cref="DuongDanAnToan"/>: từ chối khoá
///     tuyệt đối, khoá chứa `..`, và sau khi ghép **kiểm lại** rằng đường dẫn kết quả vẫn **nằm trong**
///     gốc kho. 🚨 Đây là lớp chặn **đường ghi ra ngoài thư mục** (ghi đè tệp hệ thống, hoặc ghi tệp thực thi
///     vào thư mục web) — lỗ hổng nghiêm trọng nhất của mọi chức năng tải tệp;</item>
///   <item><b>`XoaAsync` IDEMPOTENT</b> — xoá tệp không tồn tại là **thành công** (không ném). Xem lý do ở
///     hợp đồng <see cref="IMediaStorage"/>.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT:**
///   (a) **không** dùng được khi chạy nhiều instance (mỗi instance một đĩa) ⇒ phải thay bằng kho dùng chung
///     **trước khi** scale ngang;
///   (b) **không** phục vụ tệp qua HTTP — việc đó thuộc tầng phục vụ, và **phải** đặt `Content-Type` từ
///     `DinhDangDaKiem` + `nosniff` + **không** dùng cùng tên miền (xem giới hạn `polyglot` ở `NhanDangAnh`).
/// </summary>
public class MediaStorageNoiBo : IMediaStorage, ITransientDependency
{
    /// <summary>Tên thư mục gốc trong `ContentRoot` — đổi được qua `MediaStorage:ThuMucGoc`.</summary>
    public const string ThuMucMacDinh = "media-noi-bo";

    private readonly string _goc;
    private readonly ILogger<MediaStorageNoiBo> _logger;

    public MediaStorageNoiBo(IHostEnvironment moiTruong, ILogger<MediaStorageNoiBo> logger)
    {
        // 📌 `ContentRootPath` (không phải `AppContext.BaseDirectory`): trong `dotnet run`, `BaseDirectory`
        // là `bin/Debug/...` ⇒ tệp **biến mất** khi `dotnet clean`/build lại ⇒ mất ảnh **không lỗi nào**.
        _goc = Path.Combine(moiTruong.ContentRootPath, ThuMucMacDinh);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<KetQuaLuuMedia> LuuAsync(
        ReadOnlyMemory<byte> noiDung,
        DinhDangMedia dinhDangDaKiem,
        Guid? tenantId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (noiDung.IsEmpty)
        {
            // 🔒 Lớp này **kiểm lại** dù entity cũng kiểm: nó là **hàng rào cuối** cho kho tệp, và ghi tệp
            // 0 byte vào đĩa là **rác vĩnh viễn** (không ai biết để xoá).
            throw new Volo.Abp.BusinessException(eCommerceConsts.ErrorCodes.Catalog.TepKhongPhaiAnh)
                .WithData("lyDo", "Nội dung tệp rỗng ⇒ không ghi gì vào kho.");
        }

        var phanTenant = tenantId?.ToString("N") ?? "chung";
        var ten = Guid.NewGuid().ToString("N");
        var duongDanTuongDoi = $"media/{phanTenant}/{productId:N}/{ten}.{NhanDangAnh.DuoiTep(dinhDangDaKiem)}";

        var duongDanDay = DuongDanAnToan(duongDanTuongDoi);

        DirectoryHelper.CreateIfNotExists(Path.GetDirectoryName(duongDanDay)!);

        await File.WriteAllBytesAsync(duongDanDay, noiDung.ToArray(), cancellationToken);

        _logger.LogInformation(
            "ĐÃ LƯU TỆP MEDIA: khoá={Khoa} · định dạng={DinhDang} · {SoByte} byte. [MEDIA-DA-LUU]",
            duongDanTuongDoi, dinhDangDaKiem, noiDung.Length);

        return new KetQuaLuuMedia(duongDanTuongDoi);
    }

    /// <inheritdoc />
    public async Task<byte[]?> DocAsync(string khoaLuuTru, CancellationToken cancellationToken = default)
    {
        var duongDanDay = DuongDanAnToan(khoaLuuTru);

        if (!File.Exists(duongDanDay))
        {
            // 📌 Trả `null` (không ném): *"tệp không còn"* là ca **có thật** (dọn nhầm, đổi máy, khôi phục
            // backup thiếu tệp), và chỗ gọi cần **phân biệt** nó với *"lỗi đọc"* để hiện ảnh thay thế.
            return null;
        }

        return await File.ReadAllBytesAsync(duongDanDay, cancellationToken);
    }

    /// <inheritdoc />
    public Task XoaAsync(string khoaLuuTru, CancellationToken cancellationToken = default)
    {
        try
        {
            var duongDanDay = DuongDanAnToan(khoaLuuTru);

            if (!File.Exists(duongDanDay))
            {
                // 🔒 Idempotent: xoá tệp không tồn tại ⇒ THÀNH CÔNG.
                return Task.CompletedTask;
            }

            File.Delete(duongDanDay);

            _logger.LogInformation("ĐÃ XOÁ TỆP MEDIA: khoá={Khoa}. [MEDIA-DA-XOA]", khoaLuuTru);
        }
        catch (IOException ex)
        {
            // ⚠️ Lỗi I/O khi xoá **KHÔNG** được làm hỏng việc xoá metadata trong DB: thứ tự đúng là
            // **xoá DB trước, xoá tệp sau** (nếu tệp còn thì chỉ là **rác tốn dung lượng**, dọn được bằng job;
            // còn nếu ném ở đây thì dòng DB **không** bị xoá và người dùng **mãi** thấy ảnh đó).
            // 📌 Ghi log để job dọn tệp mồ côi tìm được.
            _logger.LogWarning(ex,
                "KHÔNG XOÁ ĐƯỢC tệp media (sẽ là RÁC cần dọn): khoá={Khoa}. [MEDIA-XOA-LOI]", khoaLuuTru);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 🚨 **GHÉP KHOÁ THÀNH ĐƯỜNG DẪN VÀ CHẶN THOÁT THƯ MỤC** — lớp bảo vệ **quan trọng nhất** của tệp này.
    ///
    /// **VÌ SAO PHẢI CÓ (đây là lỗ hổng nghiêm trọng nhất của mọi chức năng tải tệp):** nếu khoá đến từ
    /// dữ liệu người dùng kiểm soát, một khoá như `../../appsettings.json` hoặc `../../wwwroot/x.html`
    /// sẽ khiến hệ thống **ghi/đọc/xoá tệp NGOÀI thư mục kho** — ghi đè cấu hình, hoặc **đặt tệp thực thi
    /// vào thư mục web**. 📌 Trong lớp này khoá **do hệ thống sinh**, nhưng khoá **được lưu vào DB**
    /// và **đọc lại** ở mọi lần phục vụ/xoá ⇒ nó là **dữ liệu**, nên phải kiểm **như dữ liệu không tin được**.
    ///
    /// 🔒 **Bốn lớp kiểm (làm hết, không lớp nào thay được lớp nào):**
    /// <list type="number">
    ///   <item>khoá **không rỗng**;</item>
    ///   <item>khoá **không tuyệt đối** (`/`, `\`, `C:`) — đường dẫn tuyệt đối **bỏ qua** gốc kho;</item>
    ///   <item>khoá **không chứa `..`** ở **bất kỳ** đoạn nào (kiểm trên đoạn đã tách, không trên chuỗi thô);</item>
    ///   <item>sau khi ghép, **kiểm lại** đường dẫn kết quả **vẫn nằm trong** gốc kho — đây là **lưới an toàn
    ///     cuối** cho những cách viết `..` mà ba lớp trên chưa nghĩ tới (mã hoá, ký tự lạ, `.` lặp).</item>
    /// </list>
    /// </summary>
    private string DuongDanAnToan(string khoaLuuTru)
    {
        if (string.IsNullOrWhiteSpace(khoaLuuTru))
        {
            throw new Volo.Abp.BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh)
                .WithData("lyDo", "Khoá lưu trữ rỗng.");
        }

        if (Path.IsPathRooted(khoaLuuTru))
        {
            throw new Volo.Abp.BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh)
                .WithData("khoa", khoaLuuTru)
                .WithData("lyDo", "Khoá lưu trữ KHÔNG được là đường dẫn tuyệt đối.");
        }

        var khoaChuan = khoaLuuTru.Replace('\\', '/');

        if (khoaChuan.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(d => d == ".."))
        {
            throw new Volo.Abp.BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh)
                .WithData("khoa", khoaLuuTru)
                .WithData("lyDo", "Khoá lưu trữ chứa `..` ⇒ có ý định thoát khỏi thư mục kho.");
        }

        var gocDay = Path.GetFullPath(_goc);
        var day = Path.GetFullPath(Path.Combine(gocDay, khoaChuan));

        // 🔒 LƯỚI AN TOÀN CUỐI: so **tiền tố** với dấu phân cách ở cuối. `StartsWith(gocDay)` trần là **SAI**:
        // `/kho-media-khac` **cũng** bắt đầu bằng `/kho-media` ⇒ phải có dấu phân cách.
        var gocCoDau = gocDay.EndsWith(Path.DirectorySeparatorChar)
            ? gocDay
            : gocDay + Path.DirectorySeparatorChar;

        if (!day.StartsWith(gocCoDau, StringComparison.Ordinal))
        {
            throw new Volo.Abp.BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThayAnh)
                .WithData("khoa", khoaLuuTru)
                .WithData("goc", gocDay)
                .WithData("lyDo", "Đường dẫn kết quả NẰM NGOÀI thư mục kho ⇒ từ chối (chống thoát thư mục).");
        }

        return day;
    }
}
