using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Promotions.Contracts;

/// <summary>
/// **App service quản trị cây khuyến mãi — ĐÚNG 10 method** (`PRO-002` §6.2, allowlist `TC-31`).
/// 🚫 **KHÔNG** có `UpdateAsync` (`D-150`) — trường giá trị/giới hạn chỉ đặt ở `CreateAsync`; muốn đổi ⇒ tạo mới.
/// </summary>
public interface IPromotionAppService : IApplicationService
{
    Task<PromotionDetailDto> GetAsync(Guid id);

    Task<PagedResultDto<PromotionListItemDto>> GetListAsync(GetPromotionListInput input);

    Task<PromotionDto> CreateAsync(CreatePromotionDto input);

    Task<PromotionEvaluationResultDto> EvaluateAsync(PromotionEvaluateInput input);

    Task<PromotionTreeValidationResultDto> SaveTreeAsync(Guid id, PromotionTreeInput input);

    Task<PromotionDto> ApproveAsync(Guid id);

    Task<PromotionDto> SetStatusAsync(Guid id, SetPromotionStatusInput input);

    Task<VoucherValidationDto> ValidateVoucherAsync(ValidateVoucherInput input);

    Task<PagedResultDto<RedemptionDto>> GetRedemptionsAsync(Guid orderId);

    Task<ImportResultDto> ImportRuleCostsAsync(ImportRuleCostsInput input);
}

// ── DTO đọc ──────────────────────────────────────────────────────────────────────────────────────

public class PromotionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public PromotionKind Kind { get; set; }
    public PromotionStatus Status { get; set; }
    public bool IsCumulative { get; set; }
    public int Priority { get; set; }
    public string? ChannelScope { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public int TreeVersion { get; set; }
    public int? MaxUsageTotal { get; set; }
    public int UsedCount { get; set; }
    public int ReservedCount { get; set; }
    public int? MaxUsagePerCustomer { get; set; }
    public PromotionDiscountKind DiscountKind { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public string Currency { get; set; } = default!;
}

public class PromotionDetailDto : PromotionDto
{
    public List<PromotionTreeNodeDto> Nodes { get; set; } = new();

    /// <summary>
    /// **Trần cây theo tenant** (`D-288`) — **4** trần đọc từ ABP Setting qua `DocLimitsAsync()`;
    /// FE dùng **một nguồn duy nhất** này, **không** chép hằng số `64/8/32/1024` sang app (`D-33`).
    /// </summary>
    public PromotionTreeLimitsDto Limits { get; set; } = new();
}

/// <summary>
/// **4 trần cây điều kiện** theo tenant (`D-288`, trường additive trên <see cref="PromotionDetailDto"/>).
/// Không gồm ngân sách chạy (`MaxCandidatesPerCart`/`LeafVisitBudget`/`ComparisonBudget`) — chúng không
/// hiển thị ở "đồng hồ giới hạn".
/// </summary>
public class PromotionTreeLimitsDto
{
    public int MaxNodes { get; set; }
    public int MaxDepth { get; set; }
    public int MaxLeaves { get; set; }
    public int MaxDeclaredCombinations { get; set; }
}

public class PromotionListItemDto : PromotionDto
{
}

public class PromotionTreeNodeDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public PromotionConditionGroupType? InteractionType { get; set; }
    public string? RuleSystemName { get; set; }
    public string? ConfigJson { get; set; }
    public int SortOrder { get; set; }
    public int Depth { get; set; }
}

public class GetPromotionListInput : PagedAndSortedResultRequestDto
{
    public PromotionStatus? Status { get; set; }
    public PromotionKind? Kind { get; set; }
}

// ── DTO vào ──────────────────────────────────────────────────────────────────────────────────────

public class CreatePromotionDto
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public PromotionKind Kind { get; set; } = PromotionKind.Discount;
    public bool IsCumulative { get; set; }
    public int Priority { get; set; }
    public string? ChannelScope { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public PromotionDiscountKind DiscountKind { get; set; } = PromotionDiscountKind.FixedAmount;
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public int? MaxUsageTotal { get; set; }
    public int? MaxUsagePerCustomer { get; set; }
}

public class PromotionTreeNodeInput
{
    /// <summary>Bỏ trống ⇒ server sinh `Guid` khi lưu cây.</summary>
    public Guid? Id { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public PromotionConditionGroupType? InteractionType { get; set; }
    public string? RuleSystemName { get; set; }
    public string? ConfigJson { get; set; }
    public int SortOrder { get; set; }
    public int Depth { get; set; }
}

public class PromotionTreeInput
{
    public List<PromotionTreeNodeInput> Nodes { get; set; } = new();
}

public class SetPromotionStatusInput
{
    /// <summary>Chỉ nhận `Paused`/`Active`/`Cancelled`; `Approved`/`Expired` ⇒ `0006` (`PRO-002` §6.2).</summary>
    public PromotionStatus Status { get; set; }
}

public class PromotionEvaluateInput
{
    public Guid CheckoutSessionId { get; set; }
    public List<PromotionCartLine> Lines { get; set; } = new();
    public string ChannelCode { get; set; } = default!;
    public string Currency { get; set; } = "VND";
    public decimal Subtotal { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public bool? IsFirstOrder { get; set; }
    public string? VoucherCode { get; set; }
}

public class ValidateVoucherInput
{
    public string Code { get; set; } = default!;
    public Guid CheckoutSessionId { get; set; }
    public List<PromotionCartLine> Lines { get; set; } = new();
    public string ChannelCode { get; set; } = default!;
    public string Currency { get; set; } = "VND";
    public decimal Subtotal { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public bool? IsFirstOrder { get; set; }
}

public class ImportRuleCostsInput
{
    /// <summary>Nội dung tệp seed (CSV/JSON) — dùng để băm `Import:{tenantId}:{sha256}`.</summary>
    public string FileContent { get; set; } = default!;
}

// ── DTO kết quả ──────────────────────────────────────────────────────────────────────────────────

public class PromotionEvaluationResultDto
{
    public bool Applied { get; set; }
    public decimal DiscountTotal { get; set; }
    public string Currency { get; set; } = default!;
    public List<PromotionBreakdownItem> Breakdown { get; set; } = new();
    public PromotionRejectionReason? RejectionReason { get; set; }
    public int LeafVisits { get; set; }
    public int Comparisons { get; set; }
}

public class PromotionTreeValidationResultDto
{
    public int NodeCount { get; set; }
    public int Depth { get; set; }
    public int LeafCount { get; set; }
    public int DeclaredCost { get; set; }
    public int MaxDeclaredCombinations { get; set; }
    public bool AlreadySaved { get; set; }
    public List<string> ViPham { get; set; } = new();
}

public class VoucherValidationDto
{
    public bool IsValid { get; set; }
    public Guid? PromotionId { get; set; }
    public decimal DiscountTotal { get; set; }
    public string Currency { get; set; } = default!;
    public PromotionRejectionReason? RejectionReason { get; set; }
}

public class RedemptionDto
{
    public Guid Id { get; set; }
    public Guid PromotionId { get; set; }
    public Guid? VoucherCodeId { get; set; }
    public Guid? OrderId { get; set; }
    public PromotionRedemptionStatus Status { get; set; }
    public int UsageSequence { get; set; }
    public string? ReasonCode { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Currency { get; set; } = default!;
    public int TreeVersion { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
}

public class ImportResultDto
{
    public int CapNhat { get; set; }
    public int BoQua { get; set; }
    public bool AlreadyImported { get; set; }
}
