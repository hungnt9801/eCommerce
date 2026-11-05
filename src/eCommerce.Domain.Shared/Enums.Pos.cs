namespace eCommerce.Pos;

/// <summary>
/// **TRẠNG THÁI PHIÊN QUẦY (ca thu ngân)** — `WP-06` (`D-42`).
///
/// 📌 **VÌ SAO CHỈ 2 GIÁ TRỊ:** Nhanh mô tả ca là *"mở ca → bán → kết ca"* và **cảnh báo rõ**
/// *"đã kết ca là không quay ngược được"* (`nhanh-pos-module-map.md` §5.3). Không có trạng thái
/// *"tạm dừng"* hay *"mở lại"* ⇒ thêm giá trị vào đây là **hứa một nghiệp vụ không có trong tài liệu**
/// (đúng thứ `D-20` cấm: báo cáo có nhóm không có thật).
/// 🔒 Cột lưu dạng **số** ⇒ **CHỈ APPEND** giá trị mới (D-17).
/// </summary>
public enum CashSessionStatus
{
    /// <summary>Đang mở — hoá đơn mới được ghi vào ca này.</summary>
    Open = 0,

    /// <summary>Đã kết ca — **không** bán tiếp, **không** huỷ hoá đơn của ca (bất biến `D-42`).</summary>
    Closed = 1
}

/// <summary>
/// **TRẠNG THÁI HOÁ ĐƠN BÁN LẺ** — `WP-06`.
/// 📌 Hoá đơn POS được tạo **đã hoàn tất** (lưu là ghi sổ ngay — Nhanh: F9 lưu ảnh hưởng tồn ngay),
/// nên **không** có `Draft`: một hoá đơn nháp mà đã trừ tồn là mâu thuẫn, còn nháp không trừ tồn thì
/// giỏ hàng IndexedDB **đã** làm việc đó (không cần bảng).
/// 🔒 CHỈ APPEND (D-17).
/// </summary>
public enum RetailBillStatus
{
    /// <summary>Đã bán — đã ghi sổ kho + đã ghi nhận tiền (kể cả khi còn nợ).</summary>
    Completed = 0,

    /// <summary>Đã huỷ (đảo tồn + đảo tiền/điểm). Huỷ **một lần**; hoá đơn của ca đã kết thì **không** huỷ.</summary>
    Voided = 1
}

/// <summary>
/// **PHƯƠNG THỨC THANH TOÁN TẠI QUẦY** — `WP-06`.
///
/// 🚨 **SỐ LÀ HỢP ĐỒNG DỮ LIỆU** (cùng nguyên tắc `D-33`/`D-38`): bảng `PosRetailBillPayments.Method`
/// lưu **số**; đổi số của giá trị cũ là **diễn giải lại mọi hoá đơn đã bán** (doanh thu tiền mặt
/// thành chuyển khoản) mà **không có lỗi nào**.
/// 📌 **Nguồn số:** 6 icon thanh toán của Nhanh (`nhanh-pos-module-map.md` §5.1). Trong đó
/// *"Tiền khách đưa"* **không** phải phương thức — đó là **số tiền khách đưa** (tiền thối = đưa − phải trả),
/// nên nó là cột `TienKhachDua` trên hoá đơn, không phải một dòng thanh toán.
/// 🔒 CHỈ APPEND (D-17).
/// </summary>
public enum RetailPaymentMethod
{
    /// <summary>1 — Tiền mặt.</summary>
    Cash = 1,

    /// <summary>2 — Chuyển khoản (QR/Internet Banking).</summary>
    BankTransfer = 2,

    /// <summary>3 — Quẹt thẻ (POS card).</summary>
    Card = 3,

    /// <summary>4 — Tiêu điểm tích luỹ (quy đổi theo tỉ lệ điểm → tiền).</summary>
    Points = 4,

    /// <summary>5 — Trả góp (công ty tài chính).</summary>
    Installment = 5,

    /// <summary>20 — Khác (ví điện tử, phiếu quà tặng…).</summary>
    Other = 20
}

/// <summary>
/// **TRẠNG THÁI PHIẾU TRẢ HÀNG** — `WP-06` (màn #14 `pos/return`).
///
/// 📌 Phiếu trả được tạo **đã ghi sổ** (hàng về kho + tiền/điểm đã đảo) — cùng lý do với
/// <see cref="RetailBillStatus"/>: không có bước duyệt nào trong luồng quầy.
/// 🔒 CHỈ APPEND (D-17).
/// </summary>
public enum RetailReturnStatus
{
    /// <summary>Đã trả hàng — đã nhập lại kho + đã đảo tiền/công nợ/điểm.</summary>
    Posted = 0,

    /// <summary>Đã huỷ phiếu trả (đảo lại: xuất hàng ra khỏi kho lần nữa). Hiện **chưa có đường ghi**.</summary>
    Voided = 1
}
