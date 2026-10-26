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
}
