using System.ComponentModel.DataAnnotations;
using Volo.Abp;

namespace eCommerce.Promotions.Contracts;

/// <summary>
/// **Hợp đồng NỘI BỘ cho `Sales`** (`PRO-001-arch` §4.2) — gọi **in-process**, **cùng UoW** với checkout/tạo đơn
/// (`ADR-13`). 🚨 **KHÔNG** kế thừa `IApplicationService` ⇒ `ConventionalControllers` **không** sinh route HTTP
/// (một request HTTP riêng **không** chung UoW với `Order`). `[RemoteService(IsEnabled = false)]` là **lưới thứ hai**.
/// Quyền do endpoint checkout của `Sales` kiểm (`[Authorize]` ở đó).
/// </summary>
[RemoteService(IsEnabled = false)]
public interface IVoucherRedemptionService
{
    Task<VoucherQuoteResultDto> ReserveAsync(ReserveVoucherDto input, CancellationToken cancellationToken = default);

    Task<VoucherAppliedDto> CommitAsync(CommitVoucherDto input, CancellationToken cancellationToken = default);

    Task ReleaseAsync(ReleaseVoucherDto input, CancellationToken cancellationToken = default);

    Task ReverseAsync(ReverseVoucherDto input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Giữ lượt khi báo giá. 🚨 `VoucherRedemptionService` **không** phải `IApplicationService` ⇒ interceptor
/// DataAnnotations của ABP **không tự chạy** ⇒ service guard **tường minh** (ném `AbpValidationException` = 400).
/// Thuộc tính khai dạng `[property: …]` để rơi vào **property** của record.
/// </summary>
public sealed record ReserveVoucherDto(
    string VoucherCode,
    [property: Required]
    [property: StringLength(128, MinimumLength = 1)]
    string CheckoutSessionId,
    Guid? CustomerId,
    string ChannelCode,
    IReadOnlyList<PromotionCartLine> Lines,
    [property: Range(typeof(decimal), "0.01", "99999999999999.9999")]
    decimal Subtotal,
    [property: Required]
    [property: RegularExpression("^[A-Z]{3}$")]
    string Currency,
    [property: Range(60, 86400)]
    int TtlSeconds);

/// <summary>
/// Chốt lượt — `CheckoutSessionId` dùng để đối chiếu khoá `Checkout:{tenant}:{session}:{code}` đã ghi.
/// 🆕 `D-305` — `OrderDiscountTotal` = `Order.DiscountTotal` tại điểm chốt (để kiểm `BR-06`/`0111`).
/// **Tham số cuối**, giữ nguyên thứ tự 4 trường cũ (cấm đổi thứ tự record).
/// </summary>
public sealed record CommitVoucherDto(
    Guid RedemptionId,
    Guid OrderId,
    string VoucherCode,
    [property: Required]
    [property: StringLength(128, MinimumLength = 1)]
    string CheckoutSessionId,
    [property: Range(typeof(decimal), "0.00", "99999999999999.9999")]
    decimal OrderDiscountTotal);

/// <summary>Nhả lượt — **chỉ** `Reserved → Released` (hết TTL / `MANUAL`); `Applied → Released` **không** qua đây.</summary>
public sealed record ReleaseVoucherDto(Guid RedemptionId, string ReasonCode);

/// <summary>Đảo lượt khi trả hàng — **KHÔNG** hoàn lượt (`UsedCount` giữ nguyên).</summary>
public sealed record ReverseVoucherDto(Guid OrderId, string ReasonCode);

/// <summary>
/// Kết quả báo giá voucher — **chỉ là số giảm**, **không** phải "số phải trả"; **không** có
/// `ShippingDiscountTotal` ở v1 (`D-158`). `RedemptionId = null` ⇒ không giữ lượt (cây điều kiện sai).
/// </summary>
public class VoucherQuoteResultDto
{
    public Guid? RedemptionId { get; set; }
    public decimal DiscountTotal { get; set; }
    public string Currency { get; set; } = default!;
    public PromotionRejectionReason? ReasonCode { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>Kết quả chốt lượt; `AlreadyApplied = true` ⇒ lặp cùng payload (idempotent replay).</summary>
public class VoucherAppliedDto
{
    public Guid RedemptionId { get; set; }
    public Guid OrderId { get; set; }
    public PromotionRedemptionStatus Status { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Currency { get; set; } = default!;
    public bool AlreadyApplied { get; set; }
}
