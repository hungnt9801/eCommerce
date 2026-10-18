using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.PaymentHub.Payments;

/// <summary>
/// **TRẠNG THÁI CỦA MỘT DÒNG HOÀN TIỀN** (`S5` — `PAY-001` §5.6 #1).
///
/// 🔑 **VÌ SAO PHẢI CÓ BỐN GIÁ TRỊ, KHÔNG PHẢI `bool DaHoan`:** hoàn tiền ở cổng Việt Nam là
/// **bất đồng bộ** — cổng **nhận** yêu cầu (mã `00`) rồi mới chuyển tiền sau. Một cột `bool` sẽ
/// **gộp** ba ca **rất khác nhau** vào cùng một giá trị `false`:
/// <list type="bullet">
///   <item><b>`TuChoi`</b> — cổng nói **rõ** "không hoàn" ⇒ biết chắc, **sửa tham số rồi gửi lại được**;</item>
///   <item><b>`ChoXacNhan`</b> — cổng **đã nhận**, tiền **chưa** ra ⇒ **phải** đối soát, **cấm** thử lại mù;</item>
///   <item><b>`KhongXacDinh`</b> — lỗi hạ tầng ⇒ **KHÔNG BIẾT** cổng có nhận hay không ⇒ **cấm** thử lại mù.</item>
/// </list>
/// 📌 Gộp ba ca đó là **đúng họ lỗi** mà `RefundResult` sinh ra để chống (`PAY-001` §4.2): người vận hành
/// đọc *"chưa hoàn"* rồi bấm lại ⇒ **hoàn HAI LẦN** (mất tiền thật).
/// 🔒 Thứ tự số **giữ nguyên vĩnh viễn** (thêm giá trị phải thêm ở CUỐI) — cùng luật với
/// <see cref="PaymentTransactionStatus"/>.
/// </summary>
public enum TrangThaiDongHoanTien
{
    /// <summary>Cổng **đã nhận** yêu cầu (mã `00`) nhưng tiền **CHƯA** ra — ca **bình thường** của VNPay.</summary>
    ChoXacNhan = 0,

    /// <summary>Tiền **ĐÃ RA** và **sổ đã ghi** (`PaymentTransaction.RegisterRefund` đã chạy).</summary>
    DaHoan = 1,

    /// <summary>Cổng **TỪ CHỐI** (mã khác `00`) ⇒ biết chắc **chưa** hoàn ⇒ sửa tham số rồi gửi lại được.</summary>
    TuChoi = 2,

    /// <summary>🚨 **Lỗi hạ tầng** (timeout / không kết nối / phản hồi không đọc được) ⇒ **KHÔNG BIẾT**
    /// cổng đã nhận yêu cầu hay chưa. **CẤM** thử lại mù; **phải** tra `querydr` trước.</summary>
    KhongXacDinh = 3
}

/// <summary>
/// 🚨 **MỘT DÒNG HOÀN TIỀN** (`S5`) — bảng `PayRefunds`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI CÓ BẢNG NÀY (nợ `PAY-001` §5.6 #1, đã ghi từ 17/10/2026):** trước `S5`,
/// `PaymentTransaction.RefundedAmount` chỉ là **MỘT TỔNG**. Tổng không trả lời được ba câu hỏi mà **kế toán
/// luôn hỏi khi khách khiếu nại**:
/// <list type="number">
///   <item>hoàn **lúc nào** (mỗi lần một thời điểm, không phải một con số tổng);</item>
///   <item>hoàn **bao nhiêu mỗi lần** (3 lần × 100k khác hẳn 1 lần × 300k khi đối chiếu sao kê);</item>
///   <item>hoàn **vì sao, ai duyệt** (`RefundedAmount` không mang lý do lẫn người thực hiện).</item>
/// </list>
/// 📌 Trước đây ba câu đó chỉ trả lời được bằng **log** — nghĩa là **không** trả lời được bằng **dữ liệu**,
/// và log thì bị xoay vòng. 🔒 Với dòng tiền, *"chỉ có log"* là **không có bằng chứng**.
///
/// 🔒 **BẤT BIẾN QUAN TRỌNG NHẤT — DÒNG HOÀN TIỀN KHÔNG PHẢI LÀ "ĐÃ HOÀN":** chỉ khi
/// <see cref="TrangThaiDongHoanTien"/> là `DaHoan` thì `PaymentTransaction.RefundedAmount` mới được tăng.
/// Dòng ở `ChoXacNhan`/`KhongXacDinh` là **bằng chứng của một yêu cầu**, **không** phải bằng chứng của tiền
/// đã ra. 📌 Gộp hai thứ đó là **đúng lỗi** `PaymentAppService` §4.2 đã phải sửa: ghi sổ theo mã `00` ⇒
/// *"sổ sách nói dối"*.
///
/// 🔑 **VÌ SAO CÓ `RefundRequestId` VÀ UNIQUE INDEX TRÊN NÓ:** mã này là **số thứ tự lần hoàn của MỘT giao
/// dịch** (`RF001`, `RF002`…) — **ổn định** và **có ý nghĩa đọc được**. Nhờ nó, unique index
/// `(TenantId, PaymentTransactionId, RefundRequestId)` **thật sự** chặn được một lớp lỗi: hai luồng cùng
/// tính ra "lần hoàn thứ 2" cho cùng giao dịch ⇒ dòng thứ hai bị **`23505`** thay vì **âm thầm** thành hai
/// dòng cùng số thứ tự. ⚠️ Nếu dùng `Guid.NewGuid()` cho mỗi dòng thì index đó **trang trí** — hai dòng
/// trùng nội dung vẫn lọt, và **không lỗi nào nổi lên** (đúng bài học `BR-PAY-002` đã trả giá ở `S4`).
/// </summary>
public class PaymentRefund : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int DoDaiMaYeuCau = 64;
    public const int DoDaiLyDo = 2000;
    public const int DoDaiNguoiThucHien = 256;
    public const int DoDaiMaPhanHoi = 32;
    public const int DoDaiMaGiaoDichHoan = 128;

    public Guid? TenantId { get; protected set; }

    /// <summary>Giao dịch gốc. 🔒 **Tham chiếu MỀM** (không FK) — cùng quy ước `PaymentTransaction.OrderId`
    /// (`PAY-001` §5.1): dòng hoàn tiền phải **giữ được bằng chứng** kể cả khi bản ghi giao dịch bị xoá mềm
    /// nhầm (xoá mềm không xoá dòng hoàn ⇒ vẫn tra được *"tiền đã ra cho giao dịch nào"*).</summary>
    public Guid PaymentTransactionId { get; protected set; }

    /// <summary>Số thứ tự lần hoàn (`RF001`, `RF002`…) — **ổn định**, xem chú thích lớp.</summary>
    public string RefundRequestId { get; protected set; } = default!;

    /// <summary>Số tiền **của lần hoàn này** (đơn vị tiền thật). 🔒 `decimal` — **cấm** `double`.</summary>
    public decimal Amount { get; protected set; }

    /// <summary>Lý do — **bắt buộc** (audit + `vnp_OrderInfo` của cổng).</summary>
    public string Reason { get; protected set; } = default!;

    /// <summary>Người/lệnh yêu cầu hoàn — **bắt buộc** (audit: *"ai duyệt"*).</summary>
    public string PerformedBy { get; protected set; } = default!;

    public TrangThaiDongHoanTien Status { get; protected set; } = TrangThaiDongHoanTien.ChoXacNhan;

    /// <summary>Mã phản hồi của cổng (`vnp_ResponseCode`) — giữ **nguyên văn** để điều tra.</summary>
    public string? ProviderResponseCode { get; protected set; }

    /// <summary>Mã giao dịch hoàn do cổng cấp — tra chéo khi đối soát sao kê.</summary>
    public string? ProviderRefundTransactionId { get; protected set; }

    /// <summary>Lý do thất bại / lỗi hạ tầng (đã cắt 2000). 🔒 Không chứa dữ liệu nhạy cảm (`XC-005`).</summary>
    public string? FailureReason { get; protected set; }

    protected PaymentRefund() { }

    /// <summary>Tạo một dòng hoàn tiền ở trạng thái `ChoXacNhan` (**fail-closed**: chưa biết tiền ra hay chưa).</summary>
    /// <exception cref="Volo.Abp.BusinessException">`eCommerce:Payment:0004` khi số tiền `≤ 0` hoặc thiếu mã/lý do/người.</exception>
    public PaymentRefund(
        Guid id,
        Guid paymentTransactionId,
        string refundRequestId,
        decimal amount,
        string reason,
        string performedBy,
        Guid? tenantId = null)
        : base(id)
    {
        if (paymentTransactionId == Guid.Empty)
        {
            throw new BusinessException(PaymentErrorCodes.DongHoanTienKhongHopLe)
                .WithData("lyDo", "Thiếu `PaymentTransactionId` ⇒ dòng hoàn tiền không truy được về giao dịch gốc.");
        }

        if (string.IsNullOrWhiteSpace(refundRequestId))
        {
            throw new BusinessException(PaymentErrorCodes.DongHoanTienKhongHopLe)
                .WithData("lyDo", "Thiếu `RefundRequestId` ⇒ unique index chống trùng mất tác dụng.");
        }

        if (amount <= 0m)
        {
            throw new BusinessException(PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("soTienHoan", amount)
                .WithData("lyDo", "Số tiền hoàn phải LỚN HƠN 0 (dòng hoàn 0 đồng là vô nghĩa và che mất lỗi).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("lyDo", "Hoàn tiền BẮT BUỘC có lý do (audit + `vnp_OrderInfo`).");
        }

        if (string.IsNullOrWhiteSpace(performedBy))
        {
            throw new BusinessException(PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("lyDo", "Thiếu người thực hiện ⇒ 6 tháng sau không ai biết AI đã duyệt lần hoàn này.");
        }

        TenantId = tenantId;
        PaymentTransactionId = paymentTransactionId;
        RefundRequestId = Cat(refundRequestId.Trim(), DoDaiMaYeuCau)!;
        Amount = amount;
        Reason = Cat(reason.Trim(), DoDaiLyDo)!;
        PerformedBy = Cat(performedBy.Trim(), DoDaiNguoiThucHien)!;
    }

    /// <summary>Đã ở **trạng thái cuối** chưa (không được ghi đè nữa).</summary>
    /// <remarks>
    /// 🔒 `ChoXacNhan`/`KhongXacDinh` **KHÔNG** phải trạng thái cuối: cả hai đều *"chưa biết"* và **phải**
    /// còn đường đi tới `DaHoan` (đối soát `querydr` xác nhận tiền đã ra) hoặc `TuChoi`.
    /// 📌 Nếu coi chúng là cuối thì một dòng *"chờ xác nhận"* sẽ **vĩnh viễn** không xác nhận được —
    /// đúng nợ `Q6` của `PAY-001` §15.1, chỉ là đổi chỗ xuất hiện.
    /// </remarks>
    public bool LaTrangThaiCuoi => Status is TrangThaiDongHoanTien.DaHoan or TrangThaiDongHoanTien.TuChoi;

    /// <summary>
    /// 🚨 **GHI KẾT QUẢ CỦA CỔNG LÊN DÒNG HOÀN TIỀN — TRẢ `bool` VỀ VIỆC CÓ ĐỔI THẬT HAY KHÔNG.**
    ///
    /// 🔒 **Vì sao trả `bool` (cùng lý do `F-PAY-9` của `MarkFailed`):** chỗ gọi phải phân biệt *"vừa ghi"*
    /// với *"bị hấp thụ"* — nếu không, chỗ **đếm** lại báo *"đã đổi 5 trạng thái"* trong khi DB **không đổi
    /// dòng nào**, đúng loại báo cáo sai mà `S4` đã sửa một lần.
    ///
    /// ⚠️ **Trạng thái cuối hấp thụ:** một dòng đã `DaHoan` **không** bị hạ về `TuChoi` (và ngược lại) —
    /// lý do đến **sau** không phải là sự thật mới về **tiền**. 📌 Hai hướng đều nguy hiểm:
    /// `DaHoan → TuChoi` làm *"tiền đã ra"* biến mất khỏi sổ; `TuChoi → DaHoan` làm một lần **bị cổng từ
    /// chối** thành *"đã hoàn"*.
    /// </remarks>
    public bool GhiKetQua(
        TrangThaiDongHoanTien trangThai,
        string? maPhanHoi = null,
        string? maGiaoDichHoan = null,
        string? loiHaTang = null)
    {
        if (LaTrangThaiCuoi)
        {
            return false;
        }

        var daDoi = Status != trangThai;

        Status = trangThai;

        // 📌 `??=` chứ **không** `=`: mã phản hồi là **dữ kiện của cổng**, và một lần ghi sau không có mã
        // (`null`) không được **xoá** mã đã có — xoá là mất dấu vết để điều tra.
        ProviderResponseCode = Cat(maPhanHoi, DoDaiMaPhanHoi) ?? ProviderResponseCode;
        ProviderRefundTransactionId = Cat(maGiaoDichHoan, DoDaiMaGiaoDichHoan) ?? ProviderRefundTransactionId;

        if (!string.IsNullOrWhiteSpace(loiHaTang))
        {
            FailureReason = Cat(loiHaTang, DoDaiLyDo);
        }

        return daDoi;
    }

    /// <summary>
    /// **MÃ YÊU CẦU HOÀN — ỔN ĐỊNH THEO SỐ THỨ TỰ** (`RF001`, `RF002`…).
    /// 🔒 **CẤM** `Guid.NewGuid()` ở đây: mã ngẫu nhiên làm unique index
    /// `(TenantId, PaymentTransactionId, RefundRequestId)` **vô dụng** (mỗi lần một giá trị mới ⇒ không bao
    /// giờ trùng) ⇒ hai luồng cùng hoàn một giao dịch vẫn tạo được hai dòng cùng *"lần hoàn thứ 2"*.
    /// 📌 Cùng bài học `BR-PAY-002` mà `SinhMaThamChieu` đã đóng ở `S4`, áp cho dòng hoàn tiền.
    /// </summary>
    /// <param name="lanHoan">Số thứ tự lần hoàn (1, 2, 3…). `≤ 0` ⇒ ném (không có "lần hoàn thứ 0").</param>
    public static string SinhMaYeuCauHoan(int lanHoan)
    {
        if (lanHoan <= 0)
        {
            throw new BusinessException(PaymentErrorCodes.DongHoanTienKhongHopLe)
                .WithData("lanHoan", lanHoan)
                .WithData("lyDo", "Số thứ tự lần hoàn phải >= 1.");
        }

        return $"RF{lanHoan:D3}";
    }

    private static string? Cat(string? giaTri, int doDai)
        => giaTri is null ? null : (giaTri.Length <= doDai ? giaTri : giaTri[..doDai]);
}
