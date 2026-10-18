namespace eCommerce.Marketplace;

/// <summary>Mã lỗi miền **nhận đơn từ sàn** (`SAL-005`), quy ước `eCommerce:Marketplace:nnnn`.</summary>
public static class MarketplaceErrorCodes
{
    /// <summary>Thiếu thông tin bắt buộc của đơn sàn.</summary>
    public const string ThieuThongTinBatBuoc = "eCommerce:Marketplace:0001";

    /// <summary>Số đơn tất định vượt 64 ký tự ⇒ NÉM LỖI, **không** cắt bớt.</summary>
    public const string SoDonQuaDai = "eCommerce:Marketplace:0002";

    /// <summary>Đơn sàn không có dòng hàng ⇒ không thể là đơn thật.</summary>
    public const string DonKhongCoDongHang = "eCommerce:Marketplace:0003";

    /// <summary>Khoá idempotency trùng nhưng **payload khác** ⇒ không trả kết quả cũ, không ghi đè.</summary>
    public const string XungDotPayload = "eCommerce:Marketplace:0004";

    /// <summary>
    /// 🆕 `S6` — **hạ cờ `GapSuspected` mà KHÔNG có lý do.** Hạ cờ là **khẳng định** *"đã đối soát và dữ liệu
    /// khớp"* ⇒ thiếu lý do thì 6 tháng sau **không ai** biết vì sao đơn từng bị nghi ngờ thiếu bản trung gian
    /// (`MARKETPLACE-INTAKE` §6.1 `MarkReconciledAsync`, cùng khuôn `ReconRunMismatch`).
    /// 📌 Entity **cũng** ném (`ArgumentException`) — nhưng đó là thông điệp cho lập trình viên; API phải trả
    /// **mã nghiệp vụ** thì người gọi mới biết **sửa gì**.
    /// ⚠️ ABP biến `BusinessException` thành **`403`** trên API (không phải `400`) — bài học `S2-3`; probe đo
    /// đúng `403` để không "kiểm một thứ không bao giờ xảy ra".
    /// </summary>
    public const string ThieuLyDoDoiSoat = "eCommerce:Marketplace:0005";
}
