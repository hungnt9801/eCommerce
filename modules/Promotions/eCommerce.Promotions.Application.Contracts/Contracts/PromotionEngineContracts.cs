namespace eCommerce.Promotions.Contracts;

/// <summary>
/// **Hợp đồng DUY NHẤT mà `Sales` được dùng** (`PRO-002` §6.1, `ADR-07`). `evaluate` **CHỈ ĐỌC**:
/// không ghi `UsedCount`/`ReservedCount`/`Redemption` (`CR-7`, `D-151`); **không** bọc `IIdempotencyGuard`.
/// </summary>
public interface IPromotionEngine
{
    Task<PromotionEvaluationResult> EvaluateAsync(
        PromotionEvaluationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Đầu vào đánh giá khuyến mãi (chép nguyên SRS §6.1).</summary>
public sealed record PromotionEvaluationRequest(
    Guid CheckoutSessionId,
    IReadOnlyList<PromotionCartLine> Lines,
    string ChannelCode,
    string Currency,
    decimal Subtotal,
    Guid? CustomerId,
    Guid? CustomerGroupId,
    bool? IsFirstOrder,
    string? VoucherCode,
    PromotionApplyMode Mode);

/// <summary>`CustomerRequested ⇔ VoucherCode ≠ null` (ràng buộc HAI CHIỀU — `PRO-002` §6.1 quy tắc 4).</summary>
public enum PromotionApplyMode
{
    CustomerRequested = 0,
    AutoApply = 1
}

/// <summary>Một dòng giải thích đường đi quyết định (`Satisfied`) hoặc lý do candidate bị loại.</summary>
public sealed record PromotionBreakdownItem(
    Guid PromotionId,
    Guid? NodeId,
    bool Satisfied,
    PromotionRejectionReason? RejectionReason,
    string Source);

/// <summary>
/// Kết quả đánh giá — **MỘT** khoản giảm cấp đơn `≤ Subtotal` (`D-152`/`D-158`), **không** cộng phí ship.
/// 🚨 **KHÔNG** có `PerLine`/`PromotionLineDiscount` ở v1 (`D-152`): phân bổ theo dòng do `Sales` làm.
/// </summary>
public sealed record PromotionEvaluationResult(
    bool Applied,
    decimal DiscountTotal,
    string Currency,
    IReadOnlyList<PromotionBreakdownItem> Breakdown,
    PromotionRejectionReason? RejectionReason,
    int LeafVisits,
    int Comparisons,
    // 🆕 `D-291` ② P2 #3/#4 — trường **ADDITIVE**: kết quả của RIÊNG candidate voucher (null khi request
    // không có mã). `ValidateVoucherAsync` chỉ đọc trường này để `IsValid`/`PromotionId`/`DiscountTotal`
    // phản ánh **chính voucher**, không lấy bừa candidate tự động; và trả lý do từ chối phía voucher kể cả
    // khi một khuyến mãi tự động khác được áp (không còn "im lặng").
    PromotionVoucherOutcome? VoucherOutcome = null);

/// <summary>
/// Kết quả của **riêng candidate đến từ `VoucherCode`** (`D-291` ② P2 #3/#4) — trường additive của
/// <see cref="PromotionEvaluationResult"/>. `Applied = true` **chỉ khi** chính voucher được nhận.
/// </summary>
public sealed record PromotionVoucherOutcome(
    Guid PromotionId,
    bool Applied,
    decimal DiscountTotal,
    PromotionRejectionReason? RejectionReason);

/// <summary>
/// **Lý do loại** — **ghim ordinal `0…11`**, chỉ thêm ở **cuối** (`D-17`, `TC-46` (iv)).
/// Không lưu DB (chỉ breakdown/log/metric `reason`).
/// </summary>
public enum PromotionRejectionReason
{
    NoCandidate = 0,
    ConditionFalse = 1,
    OutOfWindow = 2,
    ChannelMismatch = 3,
    UsageLimitHit = 4,
    BudgetExceeded = 5,
    CandidatesTruncated = 6,
    UnverifiedCod = 7,
    SupersededByVoucher = 8,
    DiscountCapExceeded = 9,
    ExcludedByNonCumulative = 10,
    CustomerRequired = 11
}
