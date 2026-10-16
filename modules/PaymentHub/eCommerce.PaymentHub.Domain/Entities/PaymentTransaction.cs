using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace eCommerce.PaymentHub.Payments;

/// <summary>Trạng thái giao dịch thanh toán.</summary>
public enum PaymentTransactionStatus
{
    /// <summary>Đã tạo yêu cầu thanh toán, chưa có tiền.</summary>
    Initiated = 0,

    /// <summary>Đang chờ khách thanh toán / chờ ngân hàng xác nhận.</summary>
    Pending = 1,

    /// <summary>Đã nhận tiền VÀ đã xác thực chữ ký.</summary>
    Succeeded = 2,

    Failed = 3,
    Cancelled = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}

/// <summary>
/// Một giao dịch thanh toán với một cổng.
///
/// ⚠️ NGUYÊN TẮC QUAN TRỌNG NHẤT CỦA MODULE NÀY:
///   "ReturnUrl / redirect page KHÔNG phải bằng chứng thanh toán thành công."
///   Người dùng có thể quay lại URL đó, hoặc tự tạo URL đó.
///   Chỉ IPN/webhook ĐÃ XÁC THỰC CHỮ KÝ (và khớp số tiền) mới được chuyển trạng thái sang Succeeded.
/// </summary>
public class PaymentTransaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    /// <summary>Mã cổng: "vnpay", "momo", "payos", "sepay", "casso"...</summary>
    public string ProviderCode { get; protected set; } = default!;

    /// <summary>Đơn hàng liên quan (có thể null nếu là giao dịch nạp ví riêng).</summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Mã giao dịch phía ta gửi sang cổng. Phải UNIQUE theo (TenantId, ProviderCode).
    /// VNPay yêu cầu <c>vnp_TxnRef</c> KHÔNG được trùng trong ngày.
    /// </summary>
    public string MerchantReference { get; protected set; } = default!;

    /// <summary>Mã giao dịch do cổng cấp (vnp_TransactionNo, payOS reference, MoMo transId...).</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>decimal — không dùng double cho tiền.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentTransactionStatus Status { get; protected set; } = PaymentTransactionStatus.Initiated;

    /// <summary>Thời điểm cổng xác nhận (KHÔNG phải thời điểm ta nhận webhook).</summary>
    public DateTime? PaidAtUtc { get; set; }

    public decimal RefundedAmount { get; set; }

    /// <summary>Payload thô của IPN/webhook cuối cùng — giữ để đối soát và xử lý tranh chấp.</summary>
    public string? LastWebhookPayload { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>Số lần đã nhận webhook cho giao dịch này (để phát hiện gửi trùng).</summary>
    public int WebhookReceivedCount { get; set; }

    /// <summary>
    /// Khoá chống trùng của cổng (mã giao dịch cổng cấp, hoặc id giao dịch ngân hàng với Casso/SePay).
    /// Dùng unique index để bảo đảm xử lý webhook idempotent.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    protected PaymentTransaction() { }

    public PaymentTransaction(Guid id, string providerCode, string merchantReference, decimal amount) : base(id)
    {
        ProviderCode = providerCode;
        MerchantReference = merchantReference;
        Amount = amount;
    }

    /// <summary>
    /// Ghi nhận webhook ĐÃ QUA XÁC THỰC CHỮ KÝ.
    /// Idempotent: nếu giao dịch đã ở trạng thái **CUỐI** thì chỉ tăng bộ đếm, **không** đổi trạng thái.
    /// </summary>
    /// <remarks>
    /// 🚨 **SỬA 16/10/2026 (`SAL-006`) — `Refunded`/`PartiallyRefunded` CŨNG LÀ TRẠNG THÁI CUỐI.**
    /// Phiên bản trước **chỉ** chặn `Succeeded` ⇒ webhook gửi lại (IPN là **at-least-once**) sau khi đã hoàn tiền
    /// sẽ **"hồi sinh"** giao dịch về `Succeeded`, trong khi `RefundedAmount` **vẫn** giữ số đã hoàn
    /// ⇒ **sổ sách tự mâu thuẫn**: *"Succeeded, đã thu tiền — mà đã hoàn 100 %"*.
    /// 📌 Hệ quả tiếp theo: job đối soát nhìn `Status = Succeeded` ⇒ **tưởng khách còn nợ / đơn còn phải giao**
    /// ⇒ có thể **thu thêm** hoặc **giao hàng** cho đơn đã hoàn. **Không có lỗi nào nổi lên.**
    /// </remarks>
    public bool MarkSucceeded(string? providerTransactionId, string? idempotencyKey, string rawPayload, DateTime? paidAtUtc)
    {
        WebhookReceivedCount++;
        LastWebhookPayload = Truncate(MaskSensitiveData(rawPayload), 8000);

        if (Status is PaymentTransactionStatus.Succeeded
            or PaymentTransactionStatus.PartiallyRefunded
            or PaymentTransactionStatus.Refunded)
        {
            return false; // webhook gửi trùng — không xử lý lại (kể cả sau khi đã hoàn tiền)
        }

        ProviderTransactionId = providerTransactionId ?? ProviderTransactionId;
        IdempotencyKey = idempotencyKey ?? IdempotencyKey;
        Status = PaymentTransactionStatus.Succeeded;
        PaidAtUtc = paidAtUtc ?? DateTime.UtcNow;
        FailureReason = null;
        return true;
    }

    public void MarkFailed(string reason, string? rawPayload = null)
    {
        WebhookReceivedCount++;
        LastWebhookPayload = Truncate(MaskSensitiveData(rawPayload ?? LastWebhookPayload), 8000);

        if (Status is PaymentTransactionStatus.Succeeded
            or PaymentTransactionStatus.PartiallyRefunded
            or PaymentTransactionStatus.Refunded)
        {
            // 🚨 **Đã có tiền rồi thì KHÔNG được ghi đè thành thất bại** — và điều đó **càng đúng** khi tiền
            // đã được **hoàn**: ghi `Failed` lên một giao dịch đã hoàn sẽ **xoá dấu vết dòng tiền**
            // (`RefundedAmount` vẫn còn nhưng `Status` nói "chưa từng thu") ⇒ kế toán **không đối chiếu được**.
            // 📌 Cùng lý do với `MarkSucceeded` (xem chú thích ở đó) — hai hàm phải **nhất quán** về
            // *"trạng thái nào là CUỐI"*; lệch nhau là mầm của sổ sách tự mâu thuẫn.
            return;
        }

        Status = PaymentTransactionStatus.Failed;
        FailureReason = Truncate(reason, 2000);
    }

    /// <summary>
    /// 🚨 **GHI NHẬN HOÀN TIỀN — CÓ KIỂM, KHÔNG ĐƯỢC HOÀN VƯỢT SỐ ĐÃ THU** (`SAL-006`).
    ///
    /// **VÌ SAO PHẢI KIỂM (đây là lỗi THẬT đã có trong mã, không phải phòng xa):**
    /// phiên bản trước là `RefundedAmount += amount;` **không kiểm gì cả** ⇒ hoàn **vượt** số đã thu **LỌT**,
    /// và <see cref="PaymentTransactionStatus"/> chuyển sang `Refunded` ⇒
    /// <list type="bullet">
    ///   <item>**KẾ TOÁN SAI**: hệ thống ghi *"đã hoàn nhiều hơn đã thu"* mà **không có lỗi nào**;</item>
    ///   <item>**MẤT TIỀN THẬT nếu cổng cho phép**: một lỗi gọi lặp/double-click ⇒ hoàn hai lần
    ///     ⇒ tiền ra khỏi tài khoản mà sổ nội bộ **không** phát hiện.</item>
    /// </list>
    /// 📌 Đây **đúng họ lỗi** mà dự án đã trả giá **hai lần**: `SAL-004` **`0040`** (*"khách trả nhiều hơn số đã mua"*)
    /// và `INV-003` **`0042`** (*"mượn mã `0001` ⇒ CSKH nhận chỉ dẫn sai"*).
    ///
    /// 🔒 **Ba bất biến được cưỡng chế Ở ĐÂY (không chỉ ở app service):**
    ///   (1) **chỉ hoàn được giao dịch ĐÃ THU TIỀN** (`Succeeded`/`PartiallyRefunded`/`Refunded`) —
    ///       hoàn một giao dịch `Initiated`/`Failed` nghĩa là **hoàn tiền chưa từng vào**;
    ///   (2) **`amount > 0`** — hoàn `0` hoặc số âm là vô nghĩa (số âm còn **giảm** số đã hoàn ⇒ che lỗi);
    ///   (3) **`RefundedAmount + amount <= Amount`** — tổng hoàn **không** được vượt số đã thu.
    ///
    /// ⚠️ **TÍNH TRƯỚC — KIỂM — RỒI MỚI GÁN** (bài học `InventoryItem.ApplyMovement`, 15/10/2026):
    /// nếu gán trước rồi kiểm, entity **ở lại trạng thái SAI trong bộ nhớ**; EF vẫn đang theo dõi nó ⇒
    /// một `SaveChanges` **vì việc khác** sẽ **ghi số sai xuống DB**. *"Ném lỗi mà để lại rác là gieo bug."*
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">`eCommerce:Payment:0002` khi hoàn vượt số đã thu.</exception>
    public void RegisterRefund(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new BusinessException(PaymentErrorCodes.SoTienHoanKhongHopLe)
                .WithData("soTienHoan", amount)
                .WithData("lyDo", "Số tiền hoàn phải LỚN HƠN 0 (số âm sẽ làm GIẢM số đã hoàn ⇒ che mất lỗi).");
        }

        if (Status is not (PaymentTransactionStatus.Succeeded
            or PaymentTransactionStatus.PartiallyRefunded
            or PaymentTransactionStatus.Refunded))
        {
            // 🚨 Hoàn tiền cho giao dịch CHƯA THU ĐƯỢC là **mất tiền thật**: hệ thống chi ra số tiền mà
            // chưa bao giờ nhận. Ném lỗi để người vận hành biết **ngay**, thay vì để sổ sách sai âm thầm.
            throw new BusinessException(PaymentErrorCodes.ChuaThuDuocTienKhongHoanDuoc)
                .WithData("trangThai", Status.ToString())
                .WithData("merchantReference", MerchantReference)
                .WithData("lyDo", "Chỉ hoàn được giao dịch ĐÃ THU TIỀN (Succeeded/PartiallyRefunded/Refunded).");
        }

        var tongSauKhiHoan = RefundedAmount + amount;

        if (tongSauKhiHoan > Amount)
        {
            throw new BusinessException(PaymentErrorCodes.HoanVuotSoDaThu)
                .WithData("soDaThu", Amount)
                .WithData("daHoan", RefundedAmount)
                .WithData("soTienHoanLanNay", amount)
                .WithData("tongSauKhiHoan", tongSauKhiHoan)
                .WithData("merchantReference", MerchantReference)
                .WithData("lyDo", $"Hoàn {amount} nhưng đã thu {Amount} và đã hoàn {RefundedAmount} " +
                    $"⇒ tổng {tongSauKhiHoan} VƯỢT số đã thu. Dừng lại và kiểm tra.");
        }

        RefundedAmount = tongSauKhiHoan;

        Status = RefundedAmount >= Amount
            ? PaymentTransactionStatus.Refunded
            : PaymentTransactionStatus.PartiallyRefunded;
    }

    /// <summary>
    /// **Số tiền CÒN hoàn được** (`Amount − RefundedAmount`). 🔒 Không bao giờ âm nhờ bất biến (3) ở
    /// <see cref="RegisterRefund"/> — nhưng vẫn kẹp về `0` để hàm này **không bao giờ** trả số âm cho UI.
    /// 📌 Dùng cho API hoàn tiền: client cần biết **còn bao nhiêu** trước khi gửi, thay vì nhận lỗi.
    /// </summary>
    public decimal ConHoanDuoc => Math.Max(0m, Amount - RefundedAmount);


    /// <summary>
    /// 🚨 B(C) — CHE DỮ LIỆU NHẠY CẢM trước khi lưu payload thô (phát hiện F2 của `XC-005`).
    ///
    /// VÌ SAO: <see cref="LastWebhookPayload"/> lưu payload IPN/webhook **tới 8.000 ký tự** để đối soát
    /// và xử lý tranh chấp. Payload thật của cổng thanh toán **có thể chứa** số thẻ/tài khoản, token,
    /// OTP... ⇒ đó là **dữ liệu nhạy cảm nằm trong DB**, và rất dễ bị **ghi tiếp ra log** khi gỡ lỗi.
    ///
    /// Che theo 2 lớp:
    ///   1. Các trường có tên nhạy cảm (`password`, `secret`, `token`, `cardNumber`, `cvv`, `otp`, `pin`…).
    ///   2. Chuỗi 12–19 chữ số (số thẻ/PAN), kể cả khi có dấu cách hoặc gạch — **giữ lại 4 số cuối**
    ///      để vẫn đối soát được mà không lưu toàn bộ số thẻ.
    ///
    /// 📌 Lưu ý: đây là **giảm thiểu**, không phải bảo đảm. Không bao giờ được coi payload đã lưu là
    /// "an toàn để in ra log" — xem `XC-005` BR-005.
    /// </summary>
    public static string? MaskSensitiveData(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return payload;
        }

        // Lớp 1: trường có tên nhạy cảm (JSON dạng "tenTruong":"giaTri").
        var masked = Regex.Replace(
            payload,
            """(?i)("(?:password|secret|client_secret|token|access_token|refresh_token|card_?number|cvv|cvc|otp|pin)"\s*:\s*")[^"]*(")""",
            "$1***$2");

        // Lớp 2: chuỗi 12–19 chữ số (số thẻ/PAN) — giữ 4 số cuối.
        // 🚨 BUG CỦA CHÍNH TÔI, DO TEST BẮT ĐƯỢC: mẫu đầu tiên `\b(?:\d[ -]?){12,19}\b` cho phép
        // phần tử CUỐI kết thúc bằng dấu cách ⇒ khớp luôn dấu cách phía sau (`"9704 2292 0000 0001 "`,
        // len=20) ⇒ `[^4..]` lấy `"001 "` và **mất chữ số cuối**. Mẫu đúng phải BẮT ĐẦU và KẾT THÚC
        // bằng chữ số: `\d(?:[ -]?\d){11,18}` (tổng 12–19 chữ số).
        masked = Regex.Replace(
            masked,
            @"\d(?:[ -]?\d){11,18}",
            m => "***" + m.Value[^4..]);

        return masked;
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : (value.Length <= max ? value : value[..max]);
}
