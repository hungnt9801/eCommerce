namespace eCommerce.Customers;

/// <summary>
/// **Mã lỗi miền KHÁCH HÀNG** — dải riêng `eCommerce:Customers:0xxx` (`CUS-001` §6.4).
///
/// 📌 **VÌ SAO DẢI RIÊNG (không mượn mã miền khác):** mã lỗi là thứ **CSKH đọc để phân loại sự cố**.
/// Bốn mã dưới đây ứng với **bốn hành động khác nhau** của người vận hành — gộp lại là **chỉ dẫn sai**
/// (bài học `INV-003` **`0042`**: mượn `0001` ⇒ CSKH hiển thị *"hết hàng"* trong khi tồn kho còn thừa).
/// </summary>
public static class CustomerErrorCodes
{
    private const string Nhom = "eCommerce:Customers:";

    /// <summary>Không tìm thấy khách (hoặc đã bị **xoá mềm** — xem `ADR-02`: xoá khách là xoá mềm).</summary>
    public const string KhongTimThayKhach = Nhom + "0001";

    /// <summary>**Chuyển trạng thái KHÔNG hợp lệ** (`ADR-04`) — ví dụ `Inactive → Blocked` thẳng,
    /// hoặc `Blocked → Active` không qua `Unblock` (cần quyền + lý do).
    /// 🚨 Đây là guard **phải NÉM**, không được im lặng bỏ qua (quy ước #11).</summary>
    public const string ChuyenTrangThaiKhongHopLe = Nhom + "0002";

    /// <summary>**Liên hệ (email/SĐT) đã thuộc khách KHÁC** trong cùng tenant.
    /// 📌 Đây là `23505` của unique index, được ánh xạ thành mã nghiệp vụ để client biết cần **tra khách cũ**
    /// thay vì tạo mới (nếu trả mã chung thì client **retry** ⇒ tạo rác).</summary>
    public const string LienHeDaTonTai = Nhom + "0003";

    /// <summary>**Khách đã có chủ** (`IdentityUserId` khác `null`) ⇒ **KHÔNG nâng cấp (claim) được**
    /// (`ADR-07`: claim là **đổi chủ sở hữu**, không tạo bản ghi mới).
    /// 🚨 Nếu im lặng cho qua thì **hai khách cho một con người** ⇒ lịch sử mua/điểm thưởng **phân mảnh**.</summary>
    public const string KhachDaCoChu = Nhom + "0004";

    /// <summary>**Khách đang bị CHẶN** (`Blocked`) ⇒ không được nâng cấp/ghi nhận giao dịch mới
    /// (`ADR-04`: khách chây nợ/gian lận COD). 🚨 Tách khỏi `0004` vì **hành động khác hẳn**:
    /// `0004` ⇒ *"đã có tài khoản rồi"*, mã này ⇒ *"liên hệ quản trị để gỡ chặn"*.</summary>
    public const string KhachDangBiChan = Nhom + "0005";

    /// <summary>**Thiếu thông tin bắt buộc** (tên trống, liên hệ trống…).</summary>
    public const string ThieuThongTinBatBuoc = Nhom + "0006";

    /// <summary>**Liên hệ không hợp lệ** (email/SĐT sai định dạng). 📌 Tách khỏi `0006` vì
    /// `0006` ⇒ *"điền thêm"*, mã này ⇒ *"sửa định dạng"*.</summary>
    public const string LienHeKhongHopLe = Nhom + "0007";

    /// <summary>**Mã nhóm khách đã tồn tại** trong tenant (`ADR-09` — nhóm là **dữ liệu**, không hard-code).
    /// 📌 🔑 **TÁCH KHỎI `0003` (`LienHeDaTonTai`) DÙ CẢ HAI LÀ `23505`:** hai ca này **khác hẳn nhau về
    /// người dùng** — `0003` nói *"người này đã là khách"* (⇒ tra hồ sơ cũ), `0008` nói *"mã nhóm đã dùng"*
    /// (⇒ **chọn mã khác**). Trả lẫn mã ⇒ người vận hành **đi tra khách** trong khi lỗi nằm ở **mã nhóm**
    /// (đúng bài học `INV-003` **`0042`**: *mã đúng kỹ thuật nhưng sai nghiệp vụ*).
    /// ⚠️ Đây cũng là lý do **KHÔNG** mượn `0025`-kiểu mã của miền khác.</summary>
    public const string MaNhomDaTonTai = Nhom + "0008";

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // SỔ ĐỊA CHỈ CỦA KHÁCH (`ADDR-004` phần B). 🔒 Dải **0010…0016** — theo `D6`: **code là nguồn
    // sự thật**; `0008` **đã** có nghĩa khác (`MaNhomDaTonTai`), nên `cus-001` §3.2/§6.2 (đánh số cũ
    // `0008`/`0009`) **không** được dùng làm căn cứ (`ADDR-004` §15.2 **F6**).
    // 📌 Mỗi mã ứng với một HÀNH ĐỘNG khác nhau của người vận hành — gộp mã là chỉ dẫn sai (`INV-003` `0042`).
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// **Liên kết trùng**: cùng `AddressId` + `AddressType` **còn hiệu lực** trong sổ của khách.
    /// 🔑 Trả kèm `linkId` đang có để UI **tô sáng mục đó** thay vì bắt khách thêm lại từ đầu
    /// (`ADDR-004` §12). Đây là `23505` của unique index được **ánh xạ** thành mã nghiệp vụ.
    /// </summary>
    public const string LienKetDiaChiDaTonTai = Nhom + "0010";

    /// <summary>**Không có liên kết** (hoặc đã gỡ) — `404`, cùng tinh thần `0001`.</summary>
    public const string KhongTimThayLienKet = Nhom + "0011";

    /// <summary>
    /// **Chuyển trạng thái mặc định sai** — hoặc `UPDATE` bỏ cờ ảnh hưởng **0 dòng** (luồng khác vừa đổi).
    /// 🚨 `ADDR-004` §12: **KHÔNG** được coi `0 dòng` là thành công — nếu im lặng thì khách tin đã đổi
    /// mặc định trong khi **mặc định vẫn là mục cũ**.
    /// </summary>
    public const string MacDinhKhongHopLe = Nhom + "0012";

    /// <summary>**Cố sửa `AddressId`/`AddressType`** của liên kết đã có — `BR-008` (append-only).</summary>
    public const string KhongSuaDuocLienKet = Nhom + "0013";

    /// <summary>**Cố "bỏ xoá"** một liên kết đã gỡ — phải thêm **mục mới** (`BR-008`).</summary>
    public const string KhongPhucHoiDuocLienKet = Nhom + "0014";

    /// <summary>
    /// **Vượt trần số địa chỉ của một khách** (`ADDR-004` `Q1`: mặc định **20**, là **cấu hình**).
    /// 📌 Trả kèm `toiDa` để UI nói **con số đang áp** thay vì một câu chung.
    /// </summary>
    public const string VuotTranDiaChi = Nhom + "0015";

    /// <summary>
    /// **`AddressId` không tồn tại / không còn dùng được** (`CoDungDuoc = false`).
    /// 🔑 Tách khỏi `0011`: `0011` ⇒ *"mục này không có trong sổ"*, mã này ⇒ *"địa chỉ này đã chết, chọn cái khác"*.
    /// </summary>
    public const string DiaChiKhongDungDuoc = Nhom + "0016";
}
