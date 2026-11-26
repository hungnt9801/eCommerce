namespace eCommerce.Orders;

/// <summary>
/// 🆕 **`S14`** — mã lỗi của miền **bán hàng** dùng cho **phiếu trả hàng** (`SAL-006`).
///
/// 📌 **VÌ SAO MÃ RIÊNG, KHÔNG MƯỢN `eCommerce:Order:*`:** `SAL-003` sở hữu dải `eCommerce:Order:*` cho
/// **vòng đời đơn**; trả hàng là **aggregate khác** (`OrderReturn`) ⇒ client phân loại sự cố theo mã phải phân
/// biệt được *"đơn sai"* với *"phiếu trả sai"* (bài học `XC-003` F2).
/// 🔒 Dải `eCommerce:Sales:*` đang dùng: `0001` (ETO thiếu `update_time`), `0003` (watermark lùi).
/// </summary>
public static class SalesErrorCodes
{
    /// <summary>Dữ liệu phiếu trả **không hợp lệ** hoặc **sai guard** trạng thái (`SAL-006` BR-008/BR-014).</summary>
    public const string PhieuTraKhongHopLe = eCommerceConsts.ErrorCodes.PhieuTraKhongHopLe;

    /// <summary>
    /// 🚨 **Trả VƯỢT số đã mua** của một dòng đơn — hàng rào **nghiệp vụ** (DB có `CHECK` tương ứng, ADR-11).
    /// 📌 Mã riêng vì người dùng cần chỉ dẫn *"xem lại số lượng đã trả"*, không phải *"phiếu sai định dạng"*.
    /// </summary>
    public const string TraVuotSoDaMua = eCommerceConsts.ErrorCodes.TraVuotSoDaMua;

    /// <summary>
    /// 🆕 **`PRO-002` D6** — client gửi `CheckoutInput.OrderDiscount ≠ 0` ⇒ **NÉM** (`D-143` (2)).
    /// 📌 `DiscountTotal` của đơn **chỉ** đến từ engine khuyến mãi; nhận giảm giá tay từ client là **nguồn
    /// giảm giá thứ hai** ⇒ sai thuế/doanh thu. HTTP `400` (map ở Host).
    /// </summary>
    public const string KhongNhanGiamGiaNhapTay = "eCommerce:Sales:0004";
}



