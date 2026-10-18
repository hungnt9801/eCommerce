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

    /// <summary>🆕 (`S4`) **MÃ THAM CHIẾU KHÔNG HỢP LỆ** — rỗng, hoặc sau khi chuẩn hoá không còn ký tự dùng được.
    /// 🎯 Hành động: **sửa `Order.Number`/số đơn đầu vào** rồi tạo lại — **không** được tự sinh mã ngẫu nhiên
    /// để "cho chạy" (vi phạm `BR-PAY-002`: mã phải **ỔN ĐỊNH** qua mọi lần thử).
    /// 🚨 Vì sao phải chặn Ở ĐÂY (entity + hàm sinh mã): mã rỗng **lọt** xuống cổng nghĩa là ta gửi
    /// `vnp_TxnRef=""` — VNPay **không** từ chối rõ ràng, còn ta thì **không tra lại được** giao dịch.</summary>
    public const string MaThamChieuKhongHopLe = Nhom + "0006";

    /// <summary>🆕 (`S4`) **SỐ TIỀN GIAO DỊCH KHÔNG HỢP LỆ** — `≤ 0`.
    /// 🎯 Hành động: **kiểm lại số tiền của đơn**; số 0/âm gửi sang cổng là **lỗi dữ liệu**, không phải ca biên.
    /// 📌 Trước `S4`, constructor **không** kiểm ⇒ `amount ≤ 0` **lọt** (nợ đã ghi ở `PAY-001` §3.1 dòng 1).</summary>
    public const string SoTienGiaoDichKhongHopLe = Nhom + "0007";

    /// <summary>🆕 (`S4`) **SỐ TIỀN CỦA CỔNG LỆCH SỐ TIỀN ĐÃ LẬP GIAO DỊCH** — đóng `F-PAY-2`.
    /// 🎯 Hành động: **đối chiếu với cổng** (sao kê) trước khi làm gì tiếp; **KHÔNG** đánh dấu đơn đã trả.
    /// 🔒 Chỗ gọi **không** được ném lỗi làm rollback bản ghi giao dịch: tiền **đã** vào, xoá dấu vết là
    /// mất bằng chứng. 📌 Log `Error` có neo ASCII + giữ giao dịch, để người vận hành xử lý.</summary>
    public const string LechSoTienVoiDon = Nhom + "0008";

    /// <summary>🆕 (`S4`) **THIẾU THAM SỐ BẮT BUỘC KHI TẠO GIAO DỊCH** — `ReturnUrl`/`IpnUrl` rỗng, hoặc số tiền
    /// không phải `decimal` hữu hạn.
    /// 🎯 Hành động: **sửa tham số rồi gửi lại** (khác hẳn `0006`/`0007` — hai mã kia nói *dữ liệu ĐƠN sai*,
    /// mã này nói *yêu cầu gọi API thiếu thứ cổng bắt buộc*).
    /// 📌 Vì sao cần mã riêng: `vnp_ReturnUrl`/`vnp_IpnUrl` **rỗng** thì cổng trả *"URL không hợp lệ"* — người
    /// vận hành sẽ đi kiểm cấu hình cổng, trong khi lỗi thật là **nơi gọi quên truyền** (đúng bài học
    /// `INV-003` **`0042`**: mượn mã chung ⇒ CSKH nhận chỉ dẫn sai).</summary>
    public const string ThieuThamSoTaoGiaoDich = Nhom + "0009";

    /// <summary>🆕 (`S5`) **DÒNG HOÀN TIỀN KHÔNG HỢP LỆ** — thiếu `PaymentTransactionId`/`RefundRequestId`, số thứ
    /// tự lần hoàn `≤ 0`, **hoặc** đã tồn tại dòng hoàn cho đúng lần thứ tự đó (unique index
    /// `IX_PayRefunds_TenantId_PaymentTransactionId_RefundRequestId`).
    /// 🎯 Hành động: **kiểm `PayRefunds` của giao dịch** rồi mới gửi lại — **không** phải sửa số tiền (`0004`),
    /// **không** phải đối chiếu cổng (`0002`).
    /// 📌 **Vì sao cần mã riêng:** mã này chỉ xuất hiện khi có **đường ghi tắt** (job/seed/test dựng tay), khi
    /// hai luồng cùng tính ra một số thứ tự, hoặc do lỗi lập trình ở chỗ gọi — nó **không** phải ca nghiệp vụ
    /// bình thường như `0002`/`0003`/`0004`. Gộp vào `0004` sẽ khiến CSKH đọc *"sửa số tiền"* trong khi lỗi
    /// thật là *"thiếu/trùng mã yêu cầu"* (bài học `INV-003` **`0042`**: mượn mã chung ⇒ chỉ dẫn sai).</summary>
    public const string DongHoanTienKhongHopLe = Nhom + "0010";

    // ⚠️ **`eCommerce:Payment:0001` (`PaymentSignatureInvalid`) CỐ Ý KHÔNG KHAI LẠI Ở ĐÂY.**
    // Bản chuẩn của nó nằm ở `eCommerceConsts.ErrorCodes.PaymentSignatureInvalid`
    // (`src/eCommerce.Domain.Shared/eCommerceConsts.cs:324`) và **tầng Application** dùng được nó
    // (`eCommerce.PaymentHub.Application` **có** tham chiếu `eCommerce.Domain.Shared`; tầng **Domain** thì không).
    // 📌 Khai lại ở đây là **HAI nguồn sự thật** cho cùng một mã — đúng họ lỗi `XC-003` **F2**.
    // 🔑 Ghi nhận đáng chú ý: tính tới `S4`, mã `0001` **chưa từng được dùng ở đâu** (grep = 1 kết quả, chính là
    // chỗ khai báo). Nay `XuLyKetQuaCongService` dùng nó cho ca **từ chối payload chưa qua xác thực chữ ký**.
}
