namespace eCommerce.Pos;

/// <summary>
/// Mã lỗi của **POS — hoá đơn bán lẻ + ca thu ngân + trả hàng** (`WP-06`).
///
/// 📌 **Dải riêng `eCommerce:Pos:*`** — tách khỏi `eCommerce:Order:*` (đơn online) và
/// `eCommerce:Inventory:*` (chứng từ kho). 🔑 Mã là **hợp đồng với client**: đổi mã = đổi API.
///
/// ⚠️ **VÌ SAO PHẢI CÓ MÃ, KHÔNG NÉM `Exception` TRẦN:** mỗi mã ứng với **một cách xử lý khác nhau** ở UI:
/// `0004` (chiết khấu 2 tầng) = bỏ chiết khấu dòng; `0010` (ca đã kết) = mở ca mới; `0012` (thiếu tồn) =
/// sửa số lượng. Gộp thành một mã là mất khả năng phân biệt — đúng họ lỗi `CAT-001` §17.3 (a).
/// </summary>
public static class PosErrorCodes
{
    private const string Nhom = "eCommerce:Pos:";

    /// <summary>Hoá đơn **không có dòng nào** — không thể bán một hoá đơn rỗng.</summary>
    public const string HoaDonThieuDong = Nhom + "0001";

    /// <summary>Số lượng dòng ≤ 0 (hoặc vượt số lẻ cho phép).</summary>
    public const string SoLuongKhongHopLe = Nhom + "0002";

    /// <summary>Đơn giá âm / chiết khấu âm / chiết khấu vượt thành tiền dòng.</summary>
    public const string TienKhongHopLe = Nhom + "0003";

    /// <summary>
    /// 🚨 **Chiết khấu CẢ HAI TẦNG** — Nhanh: *"một hoá đơn **chỉ được** dùng chiết khấu theo SP **hoặc**
    /// chiết khấu tổng hoá đơn, không dùng đồng thời"* (`nhanh-pos-module-map.md` §5.1).
    /// </summary>
    public const string ChietKhauHaiTang = Nhom + "0004";

    /// <summary>Chiết khấu tổng hoá đơn vượt tiền hàng.</summary>
    public const string ChietKhauVuotTienHang = Nhom + "0005";

    /// <summary>Hoá đơn còn nợ nhưng **thiếu khách hàng** hoặc **thiếu hạn thanh toán**.</summary>
    public const string ThieuThongTinCongNo = Nhom + "0006";

    /// <summary>Tổng thanh toán ra **âm** (chiết khấu + điểm vượt quá tiền hàng).</summary>
    public const string TongThanhToanAm = Nhom + "0007";

    /// <summary>Phương thức thanh toán **không nằm trong danh mục** (`RetailPaymentMethod`).</summary>
    public const string PhuongThucKhongHopLe = Nhom + "0008";

    /// <summary>Hoá đơn **đã huỷ** mà còn thao tác trên nó.</summary>
    public const string HoaDonDaHuy = Nhom + "0009";

    /// <summary>
    /// 🚨 Hoá đơn thuộc **ca đã kết** — Nhanh: *"đã kết ca là không quay ngược được"*.
    /// 📌 Huỷ lúc này sẽ làm **tiền mặt trong két đã bàn giao** sai lệch với báo cáo ⇒ phải làm phiếu trả hàng
    /// (có chứng từ) thay vì huỷ.
    /// </summary>
    public const string CaDaKetThuc = Nhom + "0010";

    /// <summary>Không có ca nào đang mở ⇒ không bán được (phải mở ca trước khi thu tiền).</summary>
    public const string KhongCoCaDangMo = Nhom + "0011";

    /// <summary>Đã có ca đang mở cho kho này (một quầy một ca) — phải kết ca cũ trước.</summary>
    public const string DaCoCaDangMo = Nhom + "0012";

    /// <summary>Tiền đầu ca / tiền bàn giao âm.</summary>
    public const string TienCaKhongHopLe = Nhom + "0013";

    /// <summary>Ca **không phải của mình** (kết ca người khác) — cần quyền quản lý ca.</summary>
    public const string KhongPhaiCaCuaMinh = Nhom + "0014";

    /// <summary>Điểm tích luỹ: số điểm âm, hoặc giá trị quy đổi điểm vượt tiền hàng.</summary>
    public const string DiemKhongHopLe = Nhom + "0015";

    /// <summary>
    /// 🚨 `ClientBillId` **đã dùng cho một hoá đơn khác nội dung** (idempotency **không** khớp):
    /// cùng khoá mà payload khác ⇒ **KHÔNG** trả hoá đơn cũ (làm vậy là nuốt hoá đơn mới của khách).
    /// </summary>
    public const string TrungKhoaKhacNoiDung = Nhom + "0016";

    /// <summary>Không tìm thấy hoá đơn bán lẻ (hoặc không thuộc tenant hiện tại).</summary>
    public const string KhongTimThayHoaDon = Nhom + "0017";

    /// <summary>Phiếu trả **không có dòng** hoặc dòng có số lượng ≤ 0.</summary>
    public const string PhieuTraThieuDong = Nhom + "0018";

    /// <summary>Trả **vượt** số đã mua (trừ phần đã trả trước đó).</summary>
    public const string TraVuotSoDaMua = Nhom + "0019";

    /// <summary>Phí trả hàng / tiền hoàn âm, hoặc phương thức hoàn tiền không hợp lệ.</summary>
    public const string TienHoanKhongHopLe = Nhom + "0020";

    /// <summary>Hoá đơn gốc **đã huỷ** ⇒ không tạo được phiếu trả.</summary>
    public const string HoaDonGocDaHuy = Nhom + "0021";

    /// <summary>Thiếu **kho** cho hoá đơn/phiếu trả (không biết trừ/nhập kho nào).</summary>
    public const string ThieuKho = Nhom + "0022";

    /// <summary>
    /// Điểm tiêu **vượt** số điểm đang có trên hoá đơn/nợ điểm — WP-06 **chưa** có sổ điểm khách
    /// (thuộc `WP-07`) nên chỉ kiểm được phần **trong hoá đơn**; ghi rõ ở `handover-n7` §5.
    /// </summary>
    public const string DiemVuotSoDu = Nhom + "0023";

    /// <summary>
    /// 🚨 **Số hoá đơn bị trùng** (unique `(TenantId, Number)`): hai máy POS cùng lúc sinh cùng số.
    /// 📌 Cách xử lý ở UI: **gửi lại** (số mới được cấp) — không phải lỗi nghiệp vụ.
    /// </summary>
    public const string SoHoaDonTrung = Nhom + "0024";

    /// <summary>Số phiếu trả bị trùng — cùng cách xử lý với <see cref="SoHoaDonTrung"/>.</summary>
    public const string SoPhieuTraTrung = Nhom + "0025";

    /// <summary>
    /// 🚨 **Đã có hoá đơn cho đúng `ClientBillId`** — ca **gửi SONG SONG** (offline retry trong lúc request
    /// đầu chưa commit) ⇒ app service chưa kịp thấy bản ghi cũ, DB chặn bằng unique index.
    /// 📌 Cách xử lý ở client: **tải lại DS/tra theo `ClientBillId`** — hoá đơn **đã** được lưu.
    /// </summary>
    public const string HoaDonDaTonTaiTheoKhoa = Nhom + "0026";

    /// <summary>Số ca thu ngân bị trùng (`POS-CA-yyyyMMdd-NNN`) — hai máy cùng mở ca một lúc.</summary>
    public const string SoCaTrung = Nhom + "0027";

    /// <summary>
    /// 🆕 **`WP-07`** — **bản ghi NGOÀI PHẠM VI DỮ LIỆU** của người đang đăng nhập.
    ///
    /// 🔒 **VÌ SAO KHÔNG trả "không tìm thấy" trần:** người dùng cần phân biệt *"hoá đơn không tồn tại"*
    /// (gõ sai link) với *"bạn không có quyền xem hoá đơn này"* (cần người có phạm vi rộng hơn) — hai hành
    /// động khác nhau. 📌 Nhưng **KHÔNG** trả kèm nội dung bản ghi (chỉ id) để không **rò rỉ dữ liệu**
    /// ngoài phạm vi. Đây là điểm cân bằng có chủ ý giữa *"dễ hiểu"* và *"không lộ"*.
    /// </summary>
    public const string NgoaiPhamViDuLieu = Nhom + "0028";
}
