namespace eCommerce.Promotions.Contracts;

/// <summary>
/// Một dòng giỏ để đánh giá khuyến mãi (`PRO-002` §6.1). Số lượng là `decimal` (`D-31`); mọi giá trị tiền
/// **cùng `Currency`** của request, **cấm** `double`/`float`.
///
/// 📌 Kiểu này nằm ở **assembly Domain** nhưng giữ **namespace hợp đồng** `eCommerce.Promotions.Contracts`
/// (đúng SRS §6.1): rule lá (`IPromotionConditionRule`) cần đọc dòng giỏ, mà Domain **không** được tham chiếu
/// `Application.Contracts` (chiều phụ thuộc `Contracts → Domain`). Contracts thấy kiểu này nhờ tham chiếu Domain.
/// </summary>
public sealed record PromotionCartLine(
    string Sku,
    decimal Quantity,
    decimal UnitPrice,
    string? ProductGroup = null);

/// <summary>
/// **Hình chiếu CHỈ-ĐỌC** của `PromotionEvaluationContext` truyền cho rule lá (`PRO-002` §3.4 / §6.1).
///
/// 🔒 Rule **chỉ** thấy ngữ cảnh này: dữ liệu giỏ/kênh + khách + giờ + **tham số của candidate hiện tại**
/// (`MinOrderAmount`/`ChannelScope` — cột của promotion, để rule v1 đánh giá được mà vẫn THUẦN).
/// **KHÔNG** có bộ đếm ngân sách (`LeafVisits`/`Comparisons` thuộc engine).
/// </summary>
public sealed record PromotionRuleContext(
    IReadOnlyList<PromotionCartLine> Lines,
    string ChannelCode,
    string Currency,
    decimal Subtotal,
    Guid? CustomerId,
    Guid? CustomerGroupId,
    bool? IsFirstOrder,
    DateTime NowUtc,
    decimal? MinOrderAmount = null,
    string? ChannelScope = null);

/// <summary>
/// Rule lá — **THUẦN, đồng bộ, KHÔNG I/O** (`PRO-002` §3.4, `ADR-06`; `TC-29`).
/// 🚨 **CẤM** `async`, `IRepository`, `HttpClient`, `IServiceProvider` — nếu cho phép ⇒ rule gọi DB ⇒ mất guard I/O.
/// </summary>
public interface IPromotionConditionRule
{
    string SystemName { get; }

    bool IsSatisfied(PromotionRuleContext context);
}
