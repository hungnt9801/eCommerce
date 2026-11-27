using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Promotions.Contracts;

/// <summary>
/// **Quản trị voucher/mã giảm giá** (`PRO-001` §6.1) — **ĐÚNG 7 method** (`D-302` ①(3), `TC-40`).
/// 🚫 **KHÔNG** có `UpdateAsync` (`D-150`, `BR-18`); ngoại lệ **duy nhất** là <see cref="OverrideLimitAsync"/>.
/// 🚫 **KHÔNG** thêm method nào vào <see cref="IPromotionAppService"/> (`TC-31` giữ **10**).
/// </summary>
public interface IVoucherAdminAppService : IApplicationService
{
    Task<PagedResultDto<VoucherDto>> GetListAsync(VoucherFilterDto input);

    Task<VoucherDetailDto> GetAsync(Guid id);

    Task<GeneratedVouchersDto> GenerateAsync(GenerateVouchersDto input);

    Task<VoucherDto> IssueAsync(Guid id);

    Task<VoucherDto> RevokeAsync(Guid id, string reason);

    Task<VoucherDto> OverrideLimitAsync(Guid id, int newMaxUsage, string reason);

    Task<PagedResultDto<RedemptionDto>> GetRedemptionsAsync(RedemptionFilterDto input);
}

// ── DTO đọc ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>`VoucherDto` — mã + bộ đếm; `RemainingUses`/`IsExpired`/`IsExhausted` là trường **suy ra**.</summary>
public class VoucherDto
{
    public Guid Id { get; set; }
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = default!;
    public int MaxUsage { get; set; }
    public int UsedCount { get; set; }
    public int ReservedCount { get; set; }
    public VoucherCodeStatus Status { get; set; }

    /// <summary>`MaxUsage − UsedCount − ReservedCount` (**không** lưu cột).</summary>
    public int RemainingUses { get; set; }

    public bool IsExpired { get; set; }
    public bool IsExhausted { get; set; }
    public string Currency { get; set; } = default!;
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
}

public class VoucherDetailDto : VoucherDto
{
    public int? MaxUsagePerCustomer { get; set; }
    public string PromotionCode { get; set; } = default!;
}

/// <summary>Lọc danh sách mã (phân trang).</summary>
public class VoucherFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? PromotionId { get; set; }
    public VoucherCodeStatus? Status { get; set; }
    public string? Code { get; set; }
}

/// <summary>
/// 🆕 Lọc sổ lượt (`D-302` ①(3)) — **KHÔNG** trả `IdempotencyKey`. Dùng cửa có sẵn của `PRO-002`,
/// **không** mở route mới; `Status = Reserved` phục vụ CSKH xem lượt đang giữ.
/// </summary>
public class RedemptionFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? OrderId { get; set; }
    public PromotionRedemptionStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? PromotionId { get; set; }
}

// ── DTO vào ──────────────────────────────────────────────────────────────────────────────────────

public class GenerateVouchersDto
{
    public Guid PromotionId { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int Length { get; set; } = 12;
    public int Quantity { get; set; }
    public int MaxUsage { get; set; } = 1;
}

// ── DTO kết quả ──────────────────────────────────────────────────────────────────────────────────

public class GeneratedVouchersDto
{
    public Guid PromotionId { get; set; }
    public List<string> Codes { get; set; } = new();
    public int Count { get; set; }
}
