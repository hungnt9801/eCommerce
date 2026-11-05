namespace eCommerce.Customers;

/// <summary>
/// **LOẠI KHÁCH** — `WP-07` (`07 §WP-07`).
///
/// 📌 **SỐ LÀ HỢP ĐỒNG DỮ LIỆU** (cùng nguyên tắc `D-33`/`D-38`): cột `CusCustomers.Type` lưu **số**;
/// đổi số của giá trị cũ là **diễn giải lại toàn bộ khách đã phân loại** (khách buôn thành khách lẻ)
/// mà **không có lỗi nào**. 🔒 CHỈ APPEND (D-17).
/// </summary>
public enum CustomerType
{
    /// <summary>0 — Khách lẻ (mặc định; đúng cho phần lớn hồ sơ).</summary>
    Retail = 0,

    /// <summary>1 — Khách buôn (mua số lượng, hưởng giá sỉ).</summary>
    Wholesale = 1,

    /// <summary>2 — Đại lý / nhà phân phối (có hợp đồng, công nợ theo kỳ).</summary>
    Agency = 2
}

/// <summary>
/// **LOẠI CHĂM SÓC** — `WP-07` (`07 §WP-07`: *"`CustomerCare` (log 5 loại)"*).
///
/// 📌 Đúng **5 giá trị** như kế hoạch ghi — **không** tự chế thêm để "đủ đẹp" (`D-20`/`D-38`).
/// 🔑 Mỗi loại là một **hành động khác nhau** của nhân viên CSKH, nên nó phải là **dữ liệu**
/// (đếm được *"tháng này gọi được bao nhiêu khách"*), không phải một cột ghi chú tự do.
/// 🔒 CHỈ APPEND (D-17).
/// </summary>
public enum CustomerCareType
{
    /// <summary>0 — Gọi điện.</summary>
    Call = 0,

    /// <summary>1 — Nhắn tin (SMS/Zalo).</summary>
    Message = 1,

    /// <summary>2 — Email.</summary>
    Email = 2,

    /// <summary>3 — Gặp trực tiếp.</summary>
    Meeting = 3,

    /// <summary>4 — Khác (ghi rõ ở nội dung).</summary>
    Other = 4
}

/// <summary>
/// **PHẠM VI DỮ LIỆU CỦA MỘT VAI TRÒ** — `WP-07` (`07 §WP-07`: *"`RoleDataScope` (own/store/all)"*).
///
/// 🚨 **ĐÂY LÀ MỘT GUARD BẢO MẬT, KHÔNG PHẢI BỘ LỌC TIỆN DỤNG:** nếu nhân viên bật *"chỉ xem dữ liệu
/// của mình"* mà server **vẫn trả** đơn của người khác (chỉ ẩn ở UI) thì dữ liệu doanh thu/khách vẫn
/// **rời khỏi hệ thống** qua DevTools — đúng lỗi *"lọc ở client"* mà `07 §WP-07` cấm
/// (*"lọc server-side, không tin client"*).
///
/// 🔒 **SỐ LÀ HỢP ĐỒNG DỮ LIỆU** và **thứ tự số là thứ tự NỚI RỘNG** (`All = 0` rộng nhất,
/// `Own = 2` hẹp nhất) — khi một người có nhiều vai trò, phạm vi hiệu lực là phạm vi **số NHỎ NHẤT**
/// (rộng nhất) mà người đó được cấp; 📌 nhờ vậy phép gộp là `Min(...)` và **không** phải một chuỗi `if`
/// dễ sai. 🔒 CHỈ APPEND (D-17).
/// </summary>
public enum PhamViDuLieu
{
    /// <summary>0 — Tất cả (mặc định khi **chưa** cấu hình: hệ thống đang chạy phải không đổi hành vi).</summary>
    All = 0,

    /// <summary>1 — Chỉ dữ liệu của **cửa hàng** mình (lọc theo kho của cửa hàng).</summary>
    Store = 1,

    /// <summary>2 — Chỉ dữ liệu **do chính mình tạo** (`CreatorId` = người đang đăng nhập).</summary>
    Own = 2
}
