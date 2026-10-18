using eCommerce.PaymentHub.Payments;
using Volo.Abp.Application.Dtos;

namespace eCommerce.PaymentHub;

/// <summary>Giao dịch thanh toán trả ra ngoài (API vận hành) — `SAL-006`.</summary>
public class PaymentTransactionDto
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = default!;
    public Guid? OrderId { get; set; }
    public string MerchantReference { get; set; } = default!;

    /// <summary>Mã giao dịch do **CỔNG** cấp — ⚠️ **BẮT BUỘC** để hoàn tiền tra được giao dịch gốc
    /// (`vnp_TransactionNo`). `null` nghĩa là **chưa từng nhận IPN hợp lệ** ⇒ hoàn tiền sẽ phải để cổng tự tra
    /// và **có thể thất bại**.</summary>
    public string? ProviderTransactionId { get; set; }

    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }

    /// <summary>🔒 **Số tiền CÒN hoàn được** — client cần biết **trước khi** gửi, thay vì gửi rồi nhận lỗi.</summary>
    public decimal ConHoanDuoc { get; set; }

    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = default!;
    public DateTime? PaidAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public int WebhookReceivedCount { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>`true` ⇒ đã ở **trạng thái CUỐI** (đã thu / đã hoàn / một phần) ⇒ **không** hoàn thêm được
    /// nếu <see cref="ConHoanDuoc"/> bằng 0.</summary>
    public bool DaThuTien { get; set; }
}

/// <summary>
/// 🚨 **YÊU CẦU HOÀN TIỀN** (`SAL-006`).
///
/// 🔒 **Ba trường bắt buộc và vì sao:**
/// <list type="bullet">
///   <item><see cref="SoTien"/> — hoàn bao nhiêu (phải `> 0` và `≤` số còn hoàn được);</item>
///   <item><see cref="LyDo"/> — **BẮT BUỘC**: hoàn tiền mà không ghi lý do thì 6 tháng sau **không ai**
///     giải thích được vì sao tiền ra (và đây cũng là `vnp_OrderInfo` mà cổng yêu cầu);</item>
///   <item><see cref="NguoiThucHien"/> — **ai** yêu cầu (audit).</item>
/// </list>
/// </summary>
public class HoanTienInput
{
    public decimal SoTien { get; set; }
    public string LyDo { get; set; } = default!;
    public string NguoiThucHien { get; set; } = default!;
}

/// <summary>
/// 🚨 **KẾT QUẢ HOÀN TIỀN TRẢ RA API** — **KHÔNG** có trường `Success` trần.
///
/// **VÌ SAO (đây là điểm dễ gây mất tiền nhất của cả tính năng):** hoàn tiền là giao dịch **BẤT ĐỒNG BỘ**.
/// Cổng **nhận** yêu cầu (mã `00`) rồi xử lý **sau**. Nếu API trả `{ "success": true }` thì người vận hành
/// **đọc là "đã hoàn xong"** ⇒ **không ai** theo dõi bước xác nhận ⇒ khách **chưa** nhận tiền mà hệ thống
/// **đã** ghi `Refunded` ⇒ **sổ sách nói dối**.
/// ⇒ Trả **ba** trạng thái tách biệt, buộc người đọc phải phân biệt:
/// <see cref="DaNhanYeuCau"/> (cổng đã nhận) · <see cref="DaHoanThat"/> (tiền **đã ra**, đã ghi sổ) ·
/// <see cref="ChoXacNhan"/> (**phải** đối soát tiếp).
/// </summary>
public class HoanTienKetQuaDto
{
    public Guid PaymentTransactionId { get; set; }
    public string MerchantReference { get; set; } = default!;

    /// <summary>Cổng **đã nhận** yêu cầu. ⚠️ **KHÔNG** đồng nghĩa tiền đã ra.</summary>
    public bool DaNhanYeuCau { get; set; }

    /// <summary>🚨 Tiền **ĐÃ RA** và **ĐÃ ghi sổ** (`RefundedAmount` đã tăng). Chỉ `true` khi cổng xác nhận
    /// hoàn tiền **thành công ngay** (một số ngân hàng nội bộ). Với VNPay điển hình: **`false`**.</summary>
    public bool DaHoanThat { get; set; }

    /// <summary>🚨 `true` ⇒ **yêu cầu đã gửi nhưng kết quả CHƯA BIẾT** ⇒ **phải** đối soát (`querydr`).
    /// 🔒 Khi cờ này bật, `RefundedAmount` **KHÔNG** được tăng — và đó là **hành vi đúng**, không phải lỗi.</summary>
    public bool ChoXacNhan { get; set; }

    /// <summary>Số tiền **còn hoàn được** sau thao tác này (client hiển thị ngay).</summary>
    public decimal ConHoanDuoc { get; set; }

    public decimal DaHoanTong { get; set; }
    public string? MaPhanHoi { get; set; }
    public string? ThongDiep { get; set; }
    public string? MaGiaoDichHoan { get; set; }

    /// <summary>Lý do thất bại — **không** chứa dữ liệu nhạy cảm.</summary>
    public string? LyDoThatBai { get; set; }

    /// <summary>⚠️ **LỖI HẠ TẦNG** (timeout/không kết nối được): *"KHÔNG BIẾT cổng đã nhận hay chưa"*.
    /// **CẤM** người gọi *"thử lại"* — có thể hoàn **HAI LẦN**. Đường đúng: gọi
    /// <c>truy-van-trang-thai</c> để xác định trước.</summary>
    public string? LoiHaTang { get; set; }

    /// <summary>Trạng thái giao dịch **sau** thao tác (đọc từ DB thật, không phải suy diễn).</summary>
    public string TrangThaiSau { get; set; } = default!;
}

/// <summary>
/// 🚨 **APP SERVICE THANH TOÁN — VẬN HÀNH** (`SAL-006`).
///
/// 📌 **VÌ SAO CẦN (trước 16/10/2026 KHÔNG có app service nào cho PaymentHub):**
/// `PaymentTransaction` + `IPaymentProvider` tồn tại **chỉ trong module**; **không** có endpoint, **không**
/// có webhook controller ⇒ *"hoàn tiền"* — **cổng kiểm soát "KHÔNG MẤT TIỀN"** của GĐ1 — **không có đường
/// nào gọi tới**. Xem `sal-006` §17.
/// </summary>
public interface IPaymentAppService
{
    /// <summary>Chi tiết một giao dịch (`merchantReference`) — để tra trước khi hoàn.</summary>
    Task<PaymentTransactionDto?> GetTheoMaAsync(string merchantReference);

    /// <summary>Danh sách giao dịch (mới nhất trước) — tra theo `merchantReference`/trạng thái, có trần.</summary>
    Task<List<PaymentTransactionDto>> GetDanhSachAsync(string? merchantReference = null, string? trangThai = null, int toiDa = 50);

    /// <summary>
    /// 🆕 (`S4`) 🚨 **TẠO GIAO DỊCH THANH TOÁN** — đường vào **DUY NHẤT** của `PayTransactions` ở tầng sản phẩm,
    /// và là **điều kiện** để ipn/webhook (`S5`) có thứ để tra.
    ///
    /// 🔑 **Idempotent theo ĐƠN:** nếu đơn đã có giao dịch **đang mở** (`Initiated`/`Pending`) thì **dùng lại** —
    /// trả **cùng** `MerchantReference`, `LaGiaoDichDaCo = true`, **không** tạo dòng thứ hai. 📌 Nếu không,
    /// khách bấm *"Thanh toán"* hai lần (hoặc client retry sau timeout) sẽ tạo **hai** `vnp_TxnRef` ⇒ hệ thống
    /// **không** nhận ra chúng là cùng một ý định thu tiền ⇒ nguy cơ **thu hai lần**; và VNPay có thể từ chối mã
    /// thứ hai vì trùng trong ngày.
    ///
    /// 🔒 **Hai bất biến:**
    /// <list type="number">
    ///   <item>**Mã ổn định** — sinh từ `SoDon` + ngày + **số lần thử** (`SinhMaThamChieu`), **không** ngẫu nhiên
    ///     (`BR-PAY-002`, `XC-002` **BR-007**);</item>
    ///   <item>**Khoá phân tán theo đơn** trong suốt thao tác — hai request đồng thời **không** cùng tạo giao dịch
    ///     (request sau nhận `eCommerce:Lock:0001`, **không** phải `23505` khó hiểu).</item>
    /// </list>
    /// ⚠️ Trả về cả `CheckoutUrl` — nếu cổng **chưa cấu hình**, giao dịch **vẫn** được tạo (bằng chứng ý định thu
    /// tiền) và `LoiTaoUrl` nói rõ lý do; gọi lại API sau khi cấu hình sẽ **dùng lại** đúng giao dịch đó.
    /// </summary>
    Task<TaoGiaoDichKetQuaDto> TaoGiaoDichAsync(TaoGiaoDichInput input);

    /// <summary>
    /// 🚨 **YÊU CẦU HOÀN TIỀN** — gọi cổng, rồi **chỉ ghi sổ** khi <c>DaHoanThat = true</c>.
    /// ⚠️ Trả ba trạng thái tách biệt; xem <see cref="HoanTienKetQuaDto"/>.
    /// </summary>
    Task<HoanTienKetQuaDto> HoanTienAsync(Guid paymentTransactionId, HoanTienInput input);

    /// <summary>
    /// **TRUY VẤN TRẠNG THÁI TỪ CỔNG** (`querydr`) và cập nhật DB nếu cổng nói đã thu tiền.
    /// 🔑 Đây là đường **xác nhận** cho các yêu cầu hoàn tiền đang `ChoXacNhan`, và là lưới an toàn khi
    /// IPN **mất** (`REC-001`).
    /// </summary>
    Task<PaymentTransactionDto> PostTruyVanTrangThaiAsync(Guid paymentTransactionId);

    /// <summary>
    /// 🚨 **ĐỐI SOÁT LÔ TỪ CỔNG** (`querydr` hàng loạt) — `REC-001` mục 7.
    ///
    /// ══════════════════════════════════════════════════════════════════════════════════════════════
    /// 🔑 **VÌ SAO CẦN HÀM LÔ (không đủ hàm MỘT):** đối soát là công việc **ĐỊNH KỲ**, không phải tra cứu
    /// từng ca. 📌 Một hàm một (`PostTruyVanTrangThaiAsync`) buộc người vận hành **biết trước** giao dịch
    /// nào cần kiểm — mà điều đó **chính là** thứ họ **không** biết (nếu biết thì đã không cần đối soát).
    /// ⚠️ Hệ quả nếu chỉ có hàm một: **IPN mất** ⇒ giao dịch kẹt ở `Pending` ⇒ **không ai** phát hiện
    /// ⇒ 🔒 tiền đã vào nhưng hệ thống **không** biết, và **không có lỗi nào**.
    ///
    /// 🔒 **HAI BẤT BIẾN:**
    /// <list type="number">
    ///   <item>**CHỈ xét giao dịch CHƯA ở trạng thái cuối** — ⚠️ gọi cổng cho giao dịch `Succeeded`/`Refunded`
    ///     là **lãng phí quota** (cổng có giới hạn) mà **không** đổi được gì;</item>
    ///   <item>**CHỈ TIẾN, KHÔNG LÙI** — việc này do `PostTruyVanTrangThaiAsync` bảo đảm (nó **không** hạ trạng
    ///     thái khi cổng nói *"chưa biết"*), 🔑 và hàm lô **không** được bỏ qua đường đó: nó **gọi lại** chính
    ///     hàm một, không tự cập nhật DB.</item>
    /// </list>
    /// ⚠️ **GIỚI HẠN ĐÃ BIẾT:** nếu cổng **chưa cấu hình** (`HttpClient` rỗng), nhà cung cấp trả `Pending`
    /// cho **mọi** giao dịch ⇒ hàm này trả **`SoKhongXacDinh` = tất cả** và **`SoDoiTrangThai` = 0**.
    /// 🚨 Đó **không phải** *"mọi thứ đều ổn"* — 📌 nó là *"KHÔNG ĐỐI SOÁT ĐƯỢC"*. 🔒 Vì vậy DTO tách
    /// **`SoDoiTrangThai`** khỏi **`SoKhongXacDinh`**: gộp hai con số đó lại sẽ biến *"không kiểm được"*
    /// thành *"đã kiểm, không sao"* — đúng loại nói dối mà dự án đang chống.
    /// </summary>
    /// <param name="toiDa">Trần số giao dịch một lượt quét. 📌 Có trần vì mỗi giao dịch là **một lời gọi
    /// HTTP ra ngoài** — quét vô hạn sẽ **treo** request và **tiêu quota** của cổng.</param>
    Task<KetQuaDoiSoatThanhToanDto> PostDoiSoatLoatAsync(int toiDa = 50);
}


/// <summary>
/// 🚨 **KẾT QUẢ MỘT LƯỢT ĐỐI SOÁT LÔ** (`REC-001` mục 7) — 🔑 **tách ba con số, KHÔNG gộp**.
///
/// 📌 **VÌ SAO PHẢI TÁCH:** nếu chỉ trả *"đã đối soát N giao dịch"* thì ba tình huống **rất khác nhau**
/// trông **giống hệt nhau**:
/// <list type="bullet">
///   <item>**đã kiểm và không có gì đổi** (tốt);</item>
///   <item>🚨 **cổng chưa cấu hình** ⇒ **không kiểm được gì** (⚠️ **không phải** "tốt");</item>
///   <item>tiền **đã vào** mà hệ thống chưa biết ⇒ **vừa phát hiện và sửa** (cần người xem log).</item>
/// </list>
/// 🔒 Gộp ba ca đó thành một con số là biến *"không kiểm được"* thành *"đã kiểm, không sao"* — đúng loại
/// nói dối mà cả dự án đang chống.
/// </summary>
public class KetQuaDoiSoatThanhToanDto
{
    /// <summary>Tổng số giao dịch **đã xét** (chưa ở trạng thái cuối, trong trần `toiDa`).</summary>
    public int SoDaXet { get; set; }

    /// <summary>Số giao dịch **ĐỔI trạng thái** nhờ lượt này — 🔑 con số **HÀNH ĐỘNG ĐƯỢC**.
    /// 🚨 Lớn hơn `0` nghĩa là **IPN đã mất** (hoặc cổng chậm) ⇒ **cần người xem log**.</summary>
    public int SoDoiTrangThai { get; set; }

    /// <summary>🚨 Số giao dịch **KHÔNG xác định được** (cổng nói *"chưa biết"* / lỗi hạ tầng / **chưa cấu hình**).
    /// ⚠️ **KHÔNG** phải *"đã kiểm và ổn"*. 📌 Nếu con số này **bằng** `SoDaXet` thì lượt đối soát
    /// **không mang lại thông tin nào** — và đó là dấu hiệu **cấu hình sai**, không phải hệ thống khoẻ.</summary>
    public int SoKhongXacDinh { get; set; }

    /// <summary>Số giao dịch cổng xác nhận **đã thu tiền** trong lượt này — 🔑 lưới an toàn cho **IPN mất**.</summary>
    public int SoThuDuoc { get; set; }

    /// <summary>Danh sách ngắn `merchantReference` **đã đổi trạng thái** — để người vận hành **tra tiếp**
    /// mà không phải quét log. 🔒 Có trần (do chỗ gọi áp) để một lượt lớn không trả về hàng nghìn dòng.</summary>
    public List<string> MaDaDoi { get; set; } = [];
}
