using eCommerce.HttpApi.Dtos;
using eCommerce.Promotions;
using eCommerce.Promotions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// **`PRO-001` / `W25-2`** — bề mặt `/api/v1/voucher/**` (**đúng 7 route**, `D-299`). Khuôn `PromotionsController`.
///
/// 🔐 `[Authorize]` cấp **CLASS** (`Promotions.Default`) + permission hẹp từng method:
/// <list type="bullet">
///   <item>`GET` danh sách/chi tiết · `GET …/redemptions` ⇒ `Promotions.View`;</item>
///   <item>`POST …/generate` ⇒ `.Voucher.Generate`; `POST …/issue` + `POST …/revoke` ⇒ `.Voucher.Publish`;</item>
///   <item>`POST …/override-limit` ⇒ `.Voucher.OverrideLimit`.</item>
/// </list>
///
/// 🚫 **KHÔNG** có route `reserve`/`commit` (`TC-34`, `ADR-13`) — `IVoucherRedemptionService` **không** là app service.
/// 📌 Giữ **nguyên** bề mặt auto `/api/app/voucher-admin/**` (hai bề mặt, `D-285`).
/// </summary>
[ApiController]
[Route("api/v1/voucher")]
[Authorize(eCommerceConsts.Permissions.Promotions.Default)]
public class VoucherController : AbpControllerBase
{
    private readonly IVoucherAdminAppService _vouchers;

    public VoucherController(IVoucherAdminAppService vouchers)
    {
        _vouchers = vouchers;
    }

    /// <summary>Danh sách mã có phân trang. 🔐 `Promotions.View`. 📌 Trả `ApiPage&lt;T&gt;` (`Items`/`Total`/`Page`/`PageSize`).</summary>
    [HttpGet]
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<ApiPage<VoucherDto>> GetListAsync([FromQuery] VoucherListQuery input)
    {
        var (page, pageSize, skipCount, maxResultCount) = PromotionApiPaging.ChuanHoa(input.Page, input.PageSize);

        var result = await _vouchers.GetListAsync(new VoucherFilterDto
        {
            PromotionId = input.PromotionId,
            Status = input.Status,
            Code = input.Code,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            Sorting = input.Sort
        });

        return new ApiPage<VoucherDto>(result.Items, result.TotalCount, page, pageSize);
    }

    /// <summary>Chi tiết mã. 🔐 `Promotions.View`.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public Task<VoucherDetailDto> GetAsync(Guid id)
        => _vouchers.GetAsync(id);

    /// <summary>Sinh lô mã (guard `0108` trước khi ghi). 🔐 `.Voucher.Generate`.</summary>
    [HttpPost("generate")]
    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherGenerate)]
    public Task<GeneratedVouchersDto> GenerateAsync([FromBody] GenerateVouchersDto input)
        => _vouchers.GenerateAsync(input);

    /// <summary>Phát hành mã (`Generated → Issued`). 🔐 `.Voucher.Publish`.</summary>
    [HttpPost("{id:guid}/issue")]
    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherPublish)]
    public Task<VoucherDto> IssueAsync(Guid id)
        => _vouchers.IssueAsync(id);

    /// <summary>Thu hồi mã (`Issued → Revoked`); `reason` ≥ 10 ký tự. 🔐 `.Voucher.Publish`.</summary>
    [HttpPost("{id:guid}/revoke")]
    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherPublish)]
    public Task<VoucherDto> RevokeAsync(Guid id, [FromBody] VoucherReasonInput input)
        => _vouchers.RevokeAsync(id, input.Reason);

    /// <summary>Ngoại lệ duy nhất `BR-18`: đổi `MaxUsage`; `newMaxUsage &lt; Used + Reserved` ⇒ `0116`. 🔐 `.Voucher.OverrideLimit`.</summary>
    [HttpPost("{id:guid}/override-limit")]
    [Authorize(eCommerceConsts.Permissions.Promotions.VoucherOverrideLimit)]
    public Task<VoucherDto> OverrideLimitAsync(Guid id, [FromBody] VoucherOverrideLimitInput input)
        => _vouchers.OverrideLimitAsync(id, input.NewMaxUsage, input.Reason);

    /// <summary>Sổ lượt (lọc `Status=Reserved` cho CSKH). 🔐 `Promotions.View`.</summary>
    [HttpGet("redemptions")]
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<ApiPage<RedemptionDto>> GetRedemptionsAsync([FromQuery] VoucherRedemptionQuery input)
    {
        var (page, pageSize, skipCount, maxResultCount) = PromotionApiPaging.ChuanHoa(input.Page, input.PageSize);

        var result = await _vouchers.GetRedemptionsAsync(new RedemptionFilterDto
        {
            OrderId = input.OrderId,
            Status = input.Status,
            CustomerId = input.CustomerId,
            PromotionId = input.PromotionId,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            Sorting = input.Sort
        });

        return new ApiPage<RedemptionDto>(result.Items, result.TotalCount, page, pageSize);
    }
}

/// <summary>Tham số danh sách mã (`GET /api/v1/voucher`).</summary>
public sealed class VoucherListQuery
{
    public Guid? PromotionId { get; set; }
    public VoucherCodeStatus? Status { get; set; }
    public string? Code { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = PromotionApiPaging.PageSizeMacDinh;
    public string? Sort { get; set; }
}

/// <summary>Tham số sổ lượt (`GET /api/v1/voucher/redemptions`).</summary>
public sealed class VoucherRedemptionQuery
{
    public Guid? OrderId { get; set; }
    public PromotionRedemptionStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? PromotionId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = PromotionApiPaging.PageSizeMacDinh;
    public string? Sort { get; set; }
}

public sealed class VoucherReasonInput
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class VoucherOverrideLimitInput
{
    public int NewMaxUsage { get; set; }
    public string Reason { get; set; } = string.Empty;
}
