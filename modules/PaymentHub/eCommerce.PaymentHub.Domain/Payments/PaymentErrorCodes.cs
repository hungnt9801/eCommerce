namespace eCommerce.PaymentHub.Payments;

/// <summary>
/// 🚨 **MÃ LỖI CỦA MIỀN THANH TOÁN** — dải riêng `eCommerce:Payment:0xxx` (`SAL-006` — hoàn tiền).
///
/// 📌 **VÌ SAO ĐẶT Ở MODULE (không ở `eCommerce.Domain.Shared`)**: `PaymentHub.Domain` **KHÔNG** tham chiếu
/// `eCommerce.Domain.Shared` — module **tự chứa** (quy ước #2: *"module sở hữu hằng số của mình"*), và entity
/// `PaymentTransaction` **cần ném** các mã này ngay trong `RegisterRefund`.
/// ⚠️ Nếu khai **thêm** một bản ở `eCommerceConsts.ErrorCodes` thì thành **HAI nguồn sự thật** cho cùng một mã
/// — đúng họ lỗi `XC-003` **F2** (đã xảy ra thật với mã miền KÊNH: `eCommerce:Channel:*` vs `ChannelHub:*`).
/// 📌 Tầng lọc ràng buộc DB (`shared/eCommerce.Kernel/Data/ViPhamRangBuocDb.cs`) khai mã bằng **chuỗi** nên
/// **không** phụ thuộc chỗ đặt hằng số — nó vẫn ánh xạ được.
///
/// 🔑 **VÌ SAO BA MÃ RIÊNG (không gộp một mã "hoàn tiền lỗi"):** mỗi mã dẫn tới **một hành động khác nhau**
/// của CSKH — gộp lại thì người nhận **chỉ dẫn sai** (bài học `INV-003` **`0042`**: mượn mã `0001` ⇒
/// CSKH hiển thị *"hết hàng"* trong khi tồn kho còn thừa).
/// </summary>
public static class PaymentErrorCodes
{
    private const string Nhom = "eCommerce:Payment:";

    /// <summary>🚨 **HOÀN TIỀN VƯỢT SỐ ĐÃ THU** — *"hoàn 5tr nhưng chỉ thu 3tr"*.
    /// 🎯 Hành động cho CSKH: **đối chiếu với cổng** trước khi làm gì tiếp; **KHÔNG** "thử lại".
    /// 📌 Đúng họ lỗi dự án đã trả giá hai lần: `SAL-004` **`0040`** (*"khách trả nhiều hơn số đã mua"*).</summary>
    public const string HoanVuotSoDaThu = Nhom + "0002";

    /// <summary>**CHƯA THU ĐƯỢC TIỀN ⇒ KHÔNG HOÀN ĐƯỢC** — giao dịch đang `Initiated`/`Failed`.
    /// 🎯 Hành động: **kiểm TRẠNG THÁI giao dịch**, không phải kiểm số tiền.
    /// 🚨 Hoàn cho giao dịch chưa thu = **chi tiền chưa từng nhận** (mất tiền thật).</summary>
    public const string ChuaThuDuocTienKhongHoanDuoc = Nhom + "0003";

    /// <summary>**SỐ TIỀN HOÀN KHÔNG HỢP LỆ** (`≤ 0`), **hoặc** thiếu `lyDo`/`NguoiThucHien`.
    /// 📌 Số âm không chỉ vô nghĩa — nó **GIẢM** `RefundedAmount` ⇒ **che mất** một lần hoàn đã xảy ra.
    /// 📌 Mã này cũng dùng cho **thiếu lý do**: cả hai ca đều là *"yêu cầu hoàn tiền không hợp lệ"* và
    /// chỉ dẫn cho người dùng **giống nhau** (*"sửa tham số rồi gửi lại"*).</summary>
    public const string SoTienHoanKhongHopLe = Nhom + "0004";

    /// <summary>**KHÔNG TÌM THẤY GIAO DỊCH** theo Id.
    /// 📌 Tách khỏi các mã khác vì chỉ dẫn khác hẳn: *"kiểm lại Id / giao dịch đã bị xoá"* —
    /// **không** phải *"sửa số tiền"* và **không** phải *"gỡ chặn"*.
    /// ⚠️ Trả `404`-nghĩa (không phải `400`) để client phân biệt *"sai tham số"* với *"không có đối tượng"*.</summary>
    public const string KhongTimThayGiaoDich = Nhom + "0005";
}
