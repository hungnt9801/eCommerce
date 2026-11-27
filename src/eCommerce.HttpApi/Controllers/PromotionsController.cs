using eCommerce.HttpApi.Dtos;
using eCommerce.Promotions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace eCommerce.HttpApi.Controllers;

/// <summary>
/// **`PRO-002` / `W24-3`** — bề mặt `/api/v1/promotion/**` (**đúng 7 route**) cho màn dựng cây điều kiện
/// Web Admin (SRS §10.1; `D-285` ①).
///
/// 🔐 **Quyền tách theo VIỆC** (`eCommerceConsts.Permissions.Promotions`, SRS §9):
/// <list type="bullet">
///   <item>`GET` (danh sách/chi tiết) · `GET …/rule-catalog` ⇒ `Promotions.View`;</item>
///   <item>`POST` · `POST …/save-tree` · `POST …/set-status` ⇒ `Promotions.Manage`;</item>
///   <item>`POST …/approve` ⇒ `Promotions.Approve` (tách người dựng ≠ người duyệt).</item>
/// </list>
///
/// 🚨 **Mọi đường đọc/ghi đi qua app service** — controller **không** đụng repository/`DbContext`, **không**
/// lọc/truy vấn (khuôn `NotificationTemplatesController`). Sai cây ⇒ app service **NÉM** `0001`…`0004`/`0006`/
/// `0010` (HTTP map ở Host `eCommerceHttpApiHostModule.cs:449-458`) ⇒ controller **không** tự map.
///
/// 📌 Route literal `rule-catalog` **không** bị `GET /{id:guid}` nuốt nhờ ràng buộc `:guid` (`D-285`).
/// 📌 Giữ **nguyên** 10 route auto `/api/app/promotion/**`; đây là bề mặt thứ hai cho BFF + `gen:api`.
/// </summary>
[ApiController]
[Route("api/v1/promotion")]
[Authorize(eCommerceConsts.Permissions.Promotions.Default)]
public class PromotionsController : AbpControllerBase
{
    private readonly IPromotionAppService _promotions;
    private readonly IPromotionRuleCatalogAppService _ruleCatalog;

    public PromotionsController(
        IPromotionAppService promotions,
        IPromotionRuleCatalogAppService ruleCatalog)
    {
        _promotions = promotions;
        _ruleCatalog = ruleCatalog;
    }

    /// <summary>
    /// Danh sách khuyến mãi có phân trang — lọc `Status`/`Kind`. 🔐 `Promotions.View`.
    /// 📌 Trả `ApiPage&lt;T&gt;` (`Items`/`Total`/`Page`/`PageSize`) — không phải `PagedResultDto` của ABP.
    /// </summary>
    [HttpGet]
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public async Task<ApiPage<PromotionListItemDto>> GetListAsync([FromQuery] PromotionListQuery input)
    {
        var (page, pageSize, skipCount, maxResultCount) =
            PromotionApiPaging.ChuanHoa(input.Page, input.PageSize);

        var result = await _promotions.GetListAsync(new GetPromotionListInput
        {
            Status = input.Status,
            Kind = input.Kind,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount,
            Sorting = string.IsNullOrWhiteSpace(input.Sort) ? null : input.Sort
        });

        return new ApiPage<PromotionListItemDto>(result.Items, result.TotalCount, page, pageSize);
    }

    /// <summary>
    /// Rule catalog (chọn lá) — nhãn tiếng Việt từ code + `IsSeeded` (`D-289`/`D-290`). 🔐 `Promotions.View`.
    /// 📌 Đặt **trước** `GET /{id:guid}`; `:guid` bảo đảm literal không bị nuốt.
    /// </summary>
    [HttpGet("rule-catalog")]
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public Task<IReadOnlyList<PromotionRuleCatalogItemDto>> GetRuleCatalogAsync()
        => _ruleCatalog.GetRulesAsync();

    /// <summary>Chi tiết khuyến mãi kèm cây `Nodes` + `Limits` (trần theo tenant — `D-288`). 🔐 `Promotions.View`.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(eCommerceConsts.Permissions.Promotions.View)]
    public Task<PromotionDetailDto> GetAsync(Guid id)
        => _promotions.GetAsync(id);

    /// <summary>Tạo khuyến mãi ở `Draft`. 🔐 `Promotions.Manage`.</summary>
    [HttpPost]
    [Authorize(eCommerceConsts.Permissions.Promotions.Manage)]
    public Task<PromotionDto> CreateAsync([FromBody] CreatePromotionDto input)
        => _promotions.CreateAsync(input);

    /// <summary>
    /// Lưu cây điều kiện — **200 chỉ khi** cây hợp lệ; validator **NÉM** `0001`…`0004`/`0006`/`0010`, **không**
    /// trả `ViPham`. 🔐 `Promotions.Manage`.
    /// </summary>
    [HttpPost("{id:guid}/save-tree")]
    [Authorize(eCommerceConsts.Permissions.Promotions.Manage)]
    public Task<PromotionTreeValidationResultDto> SaveTreeAsync(Guid id, [FromBody] PromotionTreeInput input)
        => _promotions.SaveTreeAsync(id, input);

    /// <summary>Duyệt cây (`Draft → Approved`). 🔐 `Promotions.Approve`.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(eCommerceConsts.Permissions.Promotions.Approve)]
    public Task<PromotionDto> ApproveAsync(Guid id)
        => _promotions.ApproveAsync(id);

    /// <summary>
    /// Đổi trạng thái — **chỉ** `Paused`/`Active`/`Cancelled`; `Approved`/`Expired` ⇒ app service **NÉM** `0006`.
    /// 🔐 `Promotions.Manage`.
    /// </summary>
    [HttpPost("{id:guid}/set-status")]
    [Authorize(eCommerceConsts.Permissions.Promotions.Manage)]
    public Task<PromotionDto> SetStatusAsync(Guid id, [FromBody] SetPromotionStatusInput input)
        => _promotions.SetStatusAsync(id, input);
}
