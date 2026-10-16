namespace eCommerce.Inventory;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MIỀN KHO** (`INV-001`) — dải riêng `eCommerce:Inventory:0xxx`.
///
/// 📌 **VÌ SAO LÀ DẢI RIÊNG, KHÔNG DÙNG LẠI MÃ CỦA MIỀN KHÁC:** client/CSKH phân loại sự cố theo mã
/// (bài học `XC-003` **F2** — *"hai nguồn sự thật cho cùng một mã"* đã gây nhầm ở miền kênh).
/// Mã kho phải nói được **việc kho**, không phải việc giá/đơn.
/// </summary>
public static class InventoryErrorCodes
{
    private const string Nhom = "eCommerce:Inventory:";

    /// <summary>🚨 **KHÔNG đủ tồn kho** để đáp ứng yêu cầu (đã có từ GĐ0 — nay là **bản chuẩn DUY NHẤT**).</summary>
    public const string KhongDuTon = Nhom + "0001";

    /// <summary>Kho không tồn tại hoặc **đã ngừng dùng** (`INV-001` E1).</summary>
    public const string KhoNgungHoatDong = Nhom + "0002";

    /// <summary>Chứng từ **không ở trạng thái `Draft`** ⇒ không sửa/ghi sổ/huỷ được.</summary>
    public const string ChungTuKhongOTrangThaiNhap = Nhom + "0010";

    /// <summary>Chứng từ **chưa có dòng** nào ⇒ ghi sổ vô nghĩa (không sinh bút toán nào).</summary>
    public const string ChungTuThieuDong = Nhom + "0011";

    /// <summary>Chứng từ **đã ghi sổ** ⇒ **KHÔNG** sửa nội dung (append-only).</summary>
    public const string ChungTuDaGhiSoKhongSuaDuoc = Nhom + "0012";

    /// <summary>Chứng từ **đã ghi sổ** ⇒ **KHÔNG** huỷ (muốn đảo phải tạo chứng từ ĐẢO).</summary>
    public const string ChungTuDaGhiSoKhongHuyDuoc = Nhom + "0013";

    // ---- `INV-002` — giữ chỗ (reservation) ----

    /// <summary>Lượt giữ chỗ **không còn `Held`** ⇒ không nhả/chốt được lần nữa.</summary>
    public const string GiuChoKhongConGiu = Nhom + "0030";

    /// <summary>Giữ chỗ **đã hết hạn** mà vẫn cố chốt ⇒ nguy cơ **bán hai lần cùng một món**.</summary>
    public const string GiuChoDaHetHan = Nhom + "0031";

    /// <summary>**Không đủ hàng** để giữ chỗ (dùng chung mã `0001` với "không đủ tồn" — cùng một nguyên nhân).</summary>
    public const string KhongDuHangDeGiu = KhongDuTon;

    /// <summary>Mã kho **đã tồn tại** trong tenant (`INV-001` ADR-01).</summary>
    public const string MaKhoDaTonTai = Nhom + "0003";

    /// <summary>Số chứng từ kho **đã tồn tại** trong tenant.</summary>
    public const string SoChungTuDaTonTai = Nhom + "0014";

    /// <summary>SKU **đã có dòng** trong cùng chứng từ (phải gộp số lượng vào MỘT dòng).</summary>
    public const string SkuDaCoTrongChungTu = Nhom + "0015";

    /// <summary>Nguồn (giỏ/đơn) **đã giữ chỗ** cho SKU này rồi (`INV-002`).</summary>
    public const string DaGiuChoNguonNay = Nhom + "0032";

    /// <summary>Phiếu **chuyển kho** mà **thiếu kho đích**.</summary>
    public const string ChuyenKhoThieuKhoDich = Nhom + "0020";

    /// <summary>Phiếu **chuyển kho** mà kho nguồn **trùng** kho đích.</summary>
    public const string ChuyenKhoCungMotKho = Nhom + "0021";

    /// <summary>Loại chứng từ **không phải chuyển kho** mà lại có kho đích.</summary>
    public const string ChuyenKhoThuaKhoDich = Nhom + "0022";

    /// <summary>Số lượng dòng chứng từ phải **≥ 1** (0 hoặc âm sẽ tạo bút toán vô nghĩa/đảo chiều ngầm).</summary>
    public const string SoLuongPhaiDuong = Nhom + "0023";

    /// <summary>
    /// 🚨 **Khách trả NHIỀU HƠN số đã mua** (tính **luỹ kế** các lần trả trước) — `SAL-004`.
    /// 📌 **VÌ SAO CẦN MÃ RIÊNG (không mượn `0001`/`KhongDuTon`):** khi ca này trả `0001` (*"không đủ tồn"*),
    /// client hiển thị **SAI thông điệp** — *"hết hàng, chờ nhập"* — trong khi **tồn kho KHÔNG hề thiếu**:
    /// vấn đề là **dữ liệu đầu vào** (khách trả quá số đã mua).
    /// 📌 Mã **đúng kỹ thuật** nhưng **sai nghiệp vụ** ⇒ người dùng nhận **chỉ dẫn sai**, và **không test nào
    /// phát hiện** vì HTTP vẫn `403` (`inv-001` §34.2).
    /// </summary>
    public const string TraVuotSoDaMua = Nhom + "0040";

    /// <summary>
    /// **SKU trả hàng KHÔNG thuộc đơn** (`SAL-004`). 📌 Nếu **im lặng bỏ qua** thì khách **tưởng đã trả**
    /// mà hệ thống **không ghi gì** ⇒ mất hàng, mất tiền, không dấu vết.
    /// </summary>
    public const string SkuKhongThuocDon = Nhom + "0041";

    /// <summary>
    /// 🚨 **VƯỢT HẠN MỨC THEO KÊNH** (`INV-003` — phần **cưỡng chế**).
    /// 📌 **VÌ SAO MÃ RIÊNG (không mượn `0001`):** ``0001`` nghĩa *"kho không đủ hàng"* ⇒ client sẽ hiển thị
    /// *"hết hàng, chờ nhập"* — trong khi **tồn kho còn thừa**: vấn đề là **chính sách phân bổ của kênh**
    /// (đúng họ lỗi đã trả giá ở ``TraVuotSoDaMua`` = ``0040``: **mã đúng kỹ thuật nhưng sai nghiệp vụ**).
    /// 🔒 Đây là lỗi **fail-closed** theo mặc định (``Inv.ChannelLimit.Action = Block``) — đổi sang ``Warn``
    /// thì **KHÔNG** ném mã này mà chỉ **ghi cảnh báo**.
    /// </summary>
    public const string VuotHanMucKenh = Nhom + "0042";

    /// <summary>
    /// **Hạn mức theo kênh THIẾU mã kênh** (`INV-003` — bảng `InvChannelLimits`).
    /// 📌 **VÌ SAO LÀ LỖI, KHÔNG PHẢI "MẶC ĐỊNH CHO QUA":** một dòng hạn mức **không gắn kênh** sẽ **không bao giờ
    /// khớp** lúc giữ chỗ ⇒ người vận hành tưởng đã siết kênh, thực tế **không có gì được siết**.
    /// Đây đúng họ lỗi "cấu hình sai mà im lặng" mà `INV-003` sinh ra để chặn.
    /// </summary>
    public const string HanMucKenhThieuMaKenh = Nhom + "0043";

    /// <summary>
    /// **Hạn mức theo kênh có giá trị ÂM** (`INV-003`).
    /// 📌 Quy ước toàn hệ thống: `0` = **TẮT**; cho phép số âm tạo ra **hai cách diễn đạt cùng một ý**
    /// (âm và 0 đều là tắt) ⇒ chỗ đọc hiểu khác nhau ⇒ hạn mức **âm thầm** khác nhau giữa các đường.
    /// </summary>
    public const string HanMucKenhGiaTriAm = Nhom + "0044";

    /// <summary>
    /// **Hạn mức theo kênh ĐÃ TỒN TẠI cho phạm vi đó** (`INV-003`) — hàng rào DB
    /// `IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId` (`NULLS NOT DISTINCT`).
    /// 📌 **VÌ SAO MÃ RIÊNG (không mượn `0043`):** `0043` nghĩa *"thiếu mã kênh"* — một **lỗi dữ liệu vào**.
    /// Ca này là **xung đột/trùng lặp**: người vận hành cần nghe *"sửa dòng đang có"*, **không** phải
    /// *"bạn quên mã kênh"*. Trả sai chỉ dẫn ở đây làm người dùng **đi sửa trường không sai**
    /// (bài học `0040`/`0042`: **mã đúng kỹ thuật nhưng sai nghiệp vụ**).
    /// 🔒 Cũng **không** được nuốt thành `500`: hai request đồng thời / màn hình bấm hai lần là **bình thường**.
    /// </summary>
    public const string HanMucKenhDaTonTai = Nhom + "0045";

    /// <summary>
    /// 🚨 **Mã kênh KHÔNG tồn tại** khi cấu hình hạn mức (`INV-003` — đường ghi admin).
    /// 📌 **VÌ SAO LÀ LỖI, KHÔNG PHẢI "CỨ LƯU RỒI TÍNH SAU":** hạn mức gắn một mã kênh **không có thật** sẽ
    /// **không bao giờ khớp** lúc giữ chỗ (`StockReservationService` lọc theo `ChannelCode` của đơn)
    /// ⇒ người vận hành **tin** đã siết kênh trong khi **không có gì** được siết — đúng họ "tắt im lặng"
    /// mà dự án đã trả giá nhiều lần. 📌 Nếu kênh **mới** chưa có trong `ChnChannels`, việc đúng là
    /// **thêm kênh trước**, không phải bỏ qua kiểm.
    /// 🔒 Không mượn `0043` (*"thiếu mã kênh"*) cũng không mượn `0045` (*"đã tồn tại"*): ba ca này cần
    /// **ba chỉ dẫn khác nhau** cho người dùng (bài học `0040`/`0042`: mã đúng kỹ thuật nhưng sai nghiệp vụ).
    /// </summary>
    public const string HanMucKenhKhongTonTai = Nhom + "0046";

}
