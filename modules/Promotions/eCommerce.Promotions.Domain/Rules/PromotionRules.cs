using Volo.Abp.DependencyInjection;

namespace eCommerce.Promotions.Rules;

/// <summary>4 rule lá của v1 (`PRO-002` §3.4 / `D-149`). Mỗi rule THUẦN, đồng bộ, không I/O.</summary>
/// <remarks>
/// 🚨 **`[ExposeServices(typeof(IPromotionConditionRule))]` LÀ BẮT BUỘC** (bẫy đã đo được — khuôn
/// `modules/PaymentHub/eCommerce.PaymentHub.Application/XuLyKetQuaCongService.cs:38-56`): ABP đăng ký
/// conventional theo **TÊN** (`I&lt;TênLớp&gt;`), mà `IPromotionConditionRule` **không** khớp tên lớp
/// `MinOrderAmountRule`/… ⇒ 4 rule **không** được expose dưới `IPromotionConditionRule` ⇒
/// `PromotionRuleRegistry(IEnumerable&lt;IPromotionConditionRule&gt;)` **rỗng** trong Host runtime ⇒
/// `GET /api/v1/promotion/rule-catalog` trả `[]` (dù test dựng registry tay vẫn xanh). Chỉ lộ khi chạy Host thật.
/// </remarks>
[ExposeServices(typeof(Contracts.IPromotionConditionRule))]
public sealed class MinOrderAmountRule : Contracts.IPromotionConditionRule, ISingletonDependency
{
    public const string Ten = "MinOrderAmount";

    public string SystemName => Ten;

    /// <summary>`Subtotal` (tiền hàng trước thuế/phí ship) **≥** ngưỡng của promotion. Không đặt ngưỡng ⇒ thoả.</summary>
    public bool IsSatisfied(Contracts.PromotionRuleContext context)
        => context.MinOrderAmount is null || context.Subtotal >= context.MinOrderAmount.Value;
}

/// <summary>`null` = mọi kênh của `Order`; ngược lại so **ordinal** với `ChannelCode` của đơn.</summary>
[ExposeServices(typeof(Contracts.IPromotionConditionRule))]
public sealed class ChannelScopeRule : Contracts.IPromotionConditionRule, ISingletonDependency
{
    public const string Ten = "ChannelScope";

    public string SystemName => Ten;

    public bool IsSatisfied(Contracts.PromotionRuleContext context)
        => context.ChannelScope is null || string.Equals(context.ChannelScope, context.ChannelCode, StringComparison.Ordinal);
}

/// <summary>
/// Khách phải **có** `CustomerId` **và** `CustomerGroupId` (v1 không có giá trị "khách lẻ mặc định").
/// `CustomerId = NULL` ⇒ `false`, **không** đọc trường khách, **không** ném (`D-162`, `BR-08`).
/// </summary>
[ExposeServices(typeof(Contracts.IPromotionConditionRule))]
public sealed class CustomerGroupRule : Contracts.IPromotionConditionRule, ISingletonDependency
{
    public const string Ten = "CustomerGroup";

    public string SystemName => Ten;

    public bool IsSatisfied(Contracts.PromotionRuleContext context)
        => context.CustomerId is not null && context.CustomerGroupId is not null;
}

/// <summary>
/// Khách **có** `CustomerId` và `IsFirstOrder == true`. `CustomerId = NULL` ⇒ `false`
/// (`D-162`/`D-163`, `BR-08`; `FirstOrderOnly` + vãng lai = `false`, không phải `true`).
/// </summary>
[ExposeServices(typeof(Contracts.IPromotionConditionRule))]
public sealed class FirstOrderOnlyRule : Contracts.IPromotionConditionRule, ISingletonDependency
{
    public const string Ten = "FirstOrderOnly";

    public string SystemName => Ten;

    public bool IsSatisfied(Contracts.PromotionRuleContext context)
        => context.CustomerId is not null && context.IsFirstOrder == true;
}
