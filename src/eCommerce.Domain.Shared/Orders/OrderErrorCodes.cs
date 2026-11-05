namespace eCommerce.Orders;

/// <summary>
/// Mã lỗi của **state machine đơn hàng** (`SAL-003`).
///
/// 📌 **Dải riêng `eCommerce:Order:*`** (số ít) — tách khỏi `eCommerce:Orders:*` của ràng buộc DB
/// (`ViPhamRangBuocDb`). 🔑 Mã là **hợp đồng với client**: đổi mã = đổi API.
///
/// ⚠️ **VÌ SAO PHẢI CÓ MÃ, KHÔNG ĐƯỢC NÉM `Exception` TRẦN:** mỗi mã ứng với **một cách xử lý khác nhau**
/// ở phía client/UI (`0001` = bấm sai bước ⇒ ẩn nút; `0002`/`0003` = tải lại; `0006` = đơn đã kết thúc ⇒
/// gợi ý tạo đơn mới). Gộp thành một mã là mất khả năng phân biệt — đúng lỗi `CAT-001` §17.3 (a).
/// </summary>
public static class OrderErrorCodes
{
    private const string Nhom = "eCommerce:Order:";

    /// <summary>Chuyển trạng thái **không có cạnh** trong bảng chuyển (kèm `axis`/`from`/`to`).</summary>
    public const string ChuyenTrangThaiKhongHopLe = Nhom + "0001";

    /// <summary>`UPDATE … WHERE` ảnh hưởng **0 dòng** (RowVersion lệch) — xung đột đồng thời.</summary>
    public const string XungDotPhienBan = Nhom + "0002";

    /// <summary>`AggregateVersion` của trục lệch với bản client gửi.</summary>
    public const string PhienBanLech = Nhom + "0003";

    /// <summary>Thiếu `ReasonCode`/`ActorId` hoặc actor không được phép trên cạnh.</summary>
    public const string ThieuThongTinChuyenTrangThai = Nhom + "0004";

    /// <summary>`axis` không nằm trong 4 trục (hoặc cố ghi đè `DerivedStatus`).</summary>
    public const string TrucKhongHopLe = Nhom + "0005";

    /// <summary>Đơn đang ở trạng thái **hấp thụ** (đã huỷ/đã trả) mà cố chuyển tiếp.</summary>
    public const string TrangThaiHapThu = Nhom + "0006";

    /// <summary>Không ghi được lịch sử ⇒ rollback toàn bộ (không có "đã đổi mà thiếu lịch sử").</summary>
    public const string GhiLichSuThatBai = Nhom + "0007";

    /// <summary>Số tiền IPN lệch tổng đơn ⇒ **không** đánh dấu `Paid`.</summary>
    public const string TienKhongKhop = Nhom + "0008";

    /// <summary>
    /// 🆕 **`WP-05`** — đơn **không có kho xuất hàng** (`WarehouseId = null`) nhưng nghiệp vụ cần kho
    /// (nhập lại hàng khi đơn **hoàn**). 📌 Mã riêng vì người dùng phải làm **một việc cụ thể**: gán kho cho
    /// đơn rồi thao tác lại — không phải "thử lại" (0002) hay "bấm sai bước" (0001).
    /// </summary>
    public const string ThieuKhoKhiHoan = Nhom + "0009";

    /// <summary>
    /// 🆕 **`WP-05`** — trạng thái đích **không đi qua API chuyển trạng thái** (ví dụ 3 giá trị huỷ: phải
    /// gọi đường **huỷ** `Orders.Cancel` — hai quyền khác nhau, không thể mượn một cửa).
    /// </summary>
    public const string TrangThaiCanDuongKhac = Nhom + "0010";

    /// <summary>🆕 **`WP-05`** — dữ liệu đơn **không hợp lệ** (loại/nguồn/lý do ngoài danh mục, số âm…).</summary>
    public const string DuLieuDonKhongHopLe = Nhom + "0011";

    /// <summary>
    /// 🆕 **`WP-07`** — đơn **NGOÀI phạm vi dữ liệu** của người đang đăng nhập.
    /// 🔒 Cùng lý do mã `Pos:0028`: người dùng cần phân biệt *"đơn không tồn tại"* với *"bạn không có quyền
    /// xem đơn này"* — hai hành động khác nhau. 📌 Không trả kèm nội dung đơn (chỉ id) để không rò rỉ.
    /// </summary>
    public const string KhongTimThayDon = Nhom + "0012";
}
