namespace eCommerce.Web.Navigation;

/// <summary>
/// **MỘT MỤC TRÊN MENU ĐIỀU HƯỚNG** — dữ liệu **THUẦN** (không phụ thuộc ABP/Razor ⇒ test được).
///
/// 🔒 **VÌ SAO CÓ TRƯỜNG `QuyenCanCo` (không chỉ `Url` + `Nhan`):**
/// `WEB-001` yêu cầu menu hiện **theo quyền**. Nếu mục menu chỉ có nhãn + URL thì **không có chỗ** khai
/// *"mục này cần quyền gì"* ⇒ mọi mục **hiện cho mọi người** ⇒ người dùng bấm vào và nhận
/// **`403`/trang trắng**. 📌 Đây là trạng thái **trước** lượt này: Basic theme render menu **không lọc quyền**
/// (đã kiểm bằng `strings` trên DLL: view component `Menu_` chỉ có `Items`/`IsLeaf`; hàm
/// `CheckPermissionsAsync` **chỉ** tồn tại ở `ToolbarManager`, **không** ở menu).
/// </summary>
public sealed record MucDieuHuong
{
    /// <summary>Nhãn hiển thị (tiếng Việt).</summary>
    public required string Nhan { get; init; }

    /// <summary>Đường dẫn tương đối (bắt đầu bằng `/`).</summary>
    public required string Url { get; init; }

    /// <summary>
    /// 🔑 Tên quyền **BẮT BUỘC** để mục này hiện.
    /// ⚠️ **`null`/rỗng = hiện cho MỌI người đã đăng nhập** — và **cấm** để rỗng một cách vô ý:
    /// mục không gắn quyền mà trỏ tới trang cần quyền thì người dùng **vẫn** nhận `403` sau khi bấm.
    /// 🛡️ `BatBienDieuHuongTests` khoá bất biến *"mọi mục trỏ tới trang cần quyền PHẢI khai quyền"*.
    /// </summary>
    public string? QuyenCanCo { get; init; }

    /// <summary>Mô tả ngắn (tooltip/`title`) — giúp người dùng biết mục dùng để làm gì.</summary>
    public string? MoTa { get; init; }
}

/// <summary>
/// 🚨 **BỘ LỌC MENU THEO QUYỀN** — **LÕI THUẦN**, test được **mọi** nhánh không cần HTTP/DB.
///
/// **VÌ SAO LÀ HÀM THUẦN (không viết thẳng trong view):** quy tắc *"mục nào hiện cho ai"* là **quy tắc
/// nghiệp vụ** (ai được thấy màn hình nào). Viết trong `.cshtml` thì **không test được**, và mỗi lần sửa
/// phải dựng cả Web để kiểm — nên nó sẽ **không được kiểm**.
/// 📌 Đây đúng bài học đã trả giá ở `CAT-003` (`SinhSlug`) và `SAL-005` (`LuatWatermark`):
/// **tách lõi quyết định ra hàm thuần** ⇒ có test cho **mọi** nhánh.
///
/// 🔒 **Bất biến trung tâm — FAIL-CLOSED:** khi **không xác định được** người dùng có quyền hay không
/// (`KiemTraQuyen` trả `false`, hoặc **ném**), mục đó **KHÔNG** hiện.
/// 📌 Vì sao **fail-closed** chứ không fail-open: hiện một mục mà người dùng không có quyền ⇒ họ **bấm vào**
/// ⇒ `403`/trang trắng ⇒ **trông như hệ thống hỏng**. Ẩn nhầm một mục ⇒ người dùng chỉ **không thấy**
/// (và quản trị viên cấp quyền là xong). **Hỏng theo hướng ẩn thì rẻ hơn nhiều.**
/// </summary>
public static class BoLocDieuHuong
{
    /// <summary>
    /// Lọc danh sách mục theo hàm kiểm quyền.
    /// </summary>
    /// <param name="tatCa">Toàn bộ mục đã khai (thứ tự khai được giữ nguyên).</param>
    /// <param name="kiemTraQuyen">
    /// Hàm kiểm quyền: nhận tên quyền, trả `true` nếu người dùng hiện tại **có** quyền đó.
    /// ⚠️ Hàm này **có thể ném** (ví dụ mất kết nối tới kho quyền) ⇒ xem cách xử lý bên dưới.
    /// </param>
    public static IReadOnlyList<MucDieuHuong> Loc(
        IEnumerable<MucDieuHuong> tatCa,
        Func<string, bool> kiemTraQuyen)
    {
        ArgumentNullException.ThrowIfNull(tatCa);
        ArgumentNullException.ThrowIfNull(kiemTraQuyen);

        var ketQua = new List<MucDieuHuong>();

        foreach (var muc in tatCa)
        {
            if (muc is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(muc.QuyenCanCo))
            {
                // Mục KHÔNG khai quyền ⇒ hiện cho mọi người đã đăng nhập (xem cảnh báo ở `MucDieuHuong.QuyenCanCo`).
                ketQua.Add(muc);

                continue;
            }

            bool coQuyen;

            try
            {
                coQuyen = kiemTraQuyen(muc.QuyenCanCo);
            }
            catch
            {
                // 🚨 **FAIL-CLOSED** — không xác định được thì **ẨN**.
                // 📌 Nuốt lỗi ở đây là **có chủ ý**: một lỗi tạm thời của kho quyền **không** được làm
                // sập cả trang chủ (người dùng vẫn cần dùng được những mục khác), NHƯNG cũng **không**
                // được phép **hiện** một mục mà ta không chắc người dùng có quyền.
                coQuyen = false;
            }

            if (coQuyen)
            {
                ketQua.Add(muc);
            }
        }

        return ketQua;
    }
}
