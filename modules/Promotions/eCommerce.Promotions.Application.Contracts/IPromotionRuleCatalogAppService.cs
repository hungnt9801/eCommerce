using Volo.Abp.Application.Services;

namespace eCommerce.Promotions.Contracts;

/// <summary>
/// **Service ĐỌC rule catalog** (`PRO-002` §6.2; `D-285` ①(ii)) — **interface riêng**, **KHÔNG** thêm vào
/// allowlist 10 method của <see cref="IPromotionAppService"/>.
///
/// 🎯 Cho màn dựng cây §10.1 dựng lá chọn: tên rule + **nhãn tiếng Việt** + `RequiresVerifiedCod` + chi phí.
/// 🔐 `[Authorize]` cấp **class** (`Promotions.Default`) trên impl; permission hẹp `Promotions.View` (luật A1).
/// 📌 Dữ liệu = `PromotionRuleRegistry.SystemNames` ⨝ `ProPromotionRuleCosts` + bảng nhãn **trong code**
/// (`PromotionRuleCostCatalog`, `D-290`) ⇒ mỗi rule **đúng một** dòng, tất định theo thứ tự ordinal.
/// </summary>
public interface IPromotionRuleCatalogAppService : IApplicationService
{
    Task<IReadOnlyList<PromotionRuleCatalogItemDto>> GetRulesAsync();
}

/// <summary>
/// Một dòng của rule catalog (`PRO-002` §6.2; `D-285`/`D-289`/`D-290`).
///
/// <list type="bullet">
///   <item><c>Label</c> — nhãn tiếng Việt **lấy từ code** (`PromotionRuleCostCatalog.Meta.Label`), **không**
///     từ DB; tên ngoài bảng ⇒ `Label = SystemName` (hiển thị thô — `D-290`).</item>
///   <item><c>IsSeeded</c> — `true` ⇔ **có** dòng `ProPromotionRuleCosts` (seeder đã chạy cho rule này);
///     `false` ⇒ chưa có dòng, đang dùng mặc định `PromotionRuleCostCatalog.Cua(name)` (**không** ném `0010`,
///     **không** bỏ khỏi danh sách — `D-289`).</item>
///   <item><c>RequiresVerifiedCod</c>/<c>DeclaredCostFactor</c>/<c>RetiredAtUtc</c> — **theo dòng** khi có,
///     mặc định `false`/`1`/`null` khi thiếu (`D-289`).</item>
/// </list>
/// </summary>
public class PromotionRuleCatalogItemDto
{
    public string SystemName { get; set; } = default!;

    /// <summary>Nhãn tiếng Việt từ code; tên ngoài bảng ⇒ trùng `SystemName` (`D-290`).</summary>
    public string Label { get; set; } = default!;

    public bool RequiresVerifiedCod { get; set; }

    public int DeclaredCostFactor { get; set; }

    public DateTime? RetiredAtUtc { get; set; }

    /// <summary>`true` ⇔ có dòng `ProPromotionRuleCosts` (`D-289`).</summary>
    public bool IsSeeded { get; set; }
}
