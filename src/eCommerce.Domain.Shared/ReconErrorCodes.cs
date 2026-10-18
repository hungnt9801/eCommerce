namespace eCommerce.Reconciliation;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MIỀN ĐỐI SOÁT** (`REC-001`) — dải riêng `eCommerce:Recon:0xxx`.
///
/// 📌 **VÌ SAO DẢI RIÊNG (không mượn mã của miền kho):** mã lỗi là thứ **CSKH đọc để phân loại sự cố**.
/// Ca dưới đây **không** phải *"kho thiếu hàng"* cũng **không** phải *"hạn mức kênh"* — nó là *"hai lượt
/// ghi cùng chốt một cặp lệch"*, tức là việc của **đối soát**. Mượn mã miền khác ⇒ người nhận **chỉ dẫn sai**
/// (bài học đã trả giá **hai lần**: `0040` và `0042` — `docs/tiep-tuc-phien-moi.md` §4.1 #5).
/// </summary>
public static class ReconErrorCodes
{
    private const string Nhom = "eCommerce:Recon:";

    /// <summary>
    /// **Cặp `(kho, SKU)` ĐÃ có dòng lệch trong CÙNG một lượt đối soát** — hàng rào DB
    /// `IX_ReconRunMismatches_TenantId_Run_Kho_Sku` (`NULLS NOT DISTINCT`).
    /// 📌 Ca thật: job chạy lại / hai instance cùng quét (khoá phân tán hết hạn) / đường ghi tay.
    /// 🔒 **Không** được nuốt thành `500`: đây là **tranh chấp bình thường** — nói rõ *"lượt này đã ghi cặp này rồi"*
    /// để người vận hành biết **không cần làm gì**, thay vì tưởng hệ thống hỏng và **chạy lại** (vòng lặp).
    /// </summary>
    public const string CapLechDaGhiTrongLuot = Nhom + "0001";

    /// <summary>
    /// 🚨 **XỬ LÝ LỆCH THIẾU LÝ DO (hoặc thiếu người xử lý)** — `ReconRunMismatch.DanhDauDaXuLy`.
    ///
    /// 📌 **VÌ SAO MÃ RIÊNG (không mượn `0001`):** `0001` nghĩa *"lượt này đã ghi cặp này rồi"* —
    /// **chỉ dẫn hoàn toàn khác** (*"không cần làm gì"*), trong khi mã này nghĩa
    /// *"điền lý do rồi gửi lại"*. Trả lẫn ⇒ người vận hành **tưởng đã xong** trong khi lệch **chưa** được xử lý.
    /// 🔒 Và đây là **chênh lệch tài sản**: không có lý do thì không ai phân biệt được
    /// *"đã kiểm kê và khớp lại"* với *"bấm nút cho hết danh sách"*.
    /// 📌 Cùng khuôn `INV-003` **`0043`/`0044`**: mã **hẹp** cho ca **hẹp**, để `WithData` nói được
    /// **cặp `(kho, SKU)` nào** và **lệch bao nhiêu**.
    /// </summary>
    public const string ThieuLyDoXuLyLech = Nhom + "0002";

    /// <summary>**KHÔNG TÌM THẤY** lượt đối soát hoặc dòng lệch theo Id.
    /// 📌 Chỉ dẫn khác hẳn *"điền lý do"*: *"kiểm lại Id / dữ liệu đã bị xoá"*.</summary>
    public const string KhongTimThayBanGhiDoiSoat = Nhom + "0003";

    /// <summary>
    /// 🚨 **LƯỢT KHÔNG Ở TRẠNG THÁI CHỜ XÁC NHẬN** (`S7` — quyết định `Q5` = **A**).
    ///
    /// 📌 **VÌ SAO MÃ RIÊNG:** chỉ dẫn là *"lượt này không cần xác nhận"* — **khác hẳn** `0002`
    /// (*"điền lý do rồi gửi lại"*) và `0005` (*"điền ghi chú"*). Trả lẫn ⇒ người vận hành **điền ghi chú
    /// rồi gửi lại mãi** trong khi việc cần làm là **không bấm nút**.
    /// 🔒 Đây là **ca thật**: nút xác nhận chỉ hiện cho lượt `VuotCanXacNhan`, nhưng **API thì ai cũng gọi được**
    /// ⇒ nếu im lặng bỏ qua thì hệ thống ghi *"đã xác nhận"* cho một lượt **chưa từng cần** xác nhận.
    /// </summary>
    public const string LuotKhongChoXacNhan = Nhom + "0004";

    /// <summary>**XÁC NHẬN LƯỢT THIẾU GHI CHÚ** — `ReconRun.XacNhan`.
    /// 📌 Lý do bắt buộc: xác nhận lệch = **chấp nhận chênh lệch tài sản**; không có lý do thì không ai
    /// phân biệt được *"đã kiểm và khớp lại"* với *"bấm nút cho hết danh sách"*.</summary>
    public const string ThieuGhiChuXacNhan = Nhom + "0005";

    /// <summary>
    /// 🚨 **CHÍNH SÁCH LƯU TRỮ (RETENTION) KHÔNG HỢP LỆ** (`S7`).
    /// 📌 **VÌ SAO PHẢI CÓ MÃ RIÊNG CHO CA NÀY:** `0` (hoặc âm) nghĩa là **TẮT retention** — không phải
    /// *"giữ 0 ngày"* (mà nếu hiểu vậy thì **một lần gõ sai xoá sạch bằng chứng đối soát**).
    /// 🔒 Vì vậy đường dọn **NÉM** khi nhận `<= 0` thay vì lặng lẽ coi là "xoá hết".
    /// </summary>
    public const string ChinhSachLuuTruKhongHopLe = Nhom + "0006";
}
