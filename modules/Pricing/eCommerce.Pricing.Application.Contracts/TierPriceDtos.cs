using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace eCommerce.Pricing.Dtos;

public class TierPriceSetDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Scope { get; set; } = default!;
    public string? ChannelCode { get; set; }
    public Guid? ChannelShopId { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int Priority { get; set; }
    public string Status { get; set; } = default!;

    /// <summary>Số bậc **đếm từ DB** (không phải thuộc tính trên entity — bài học `PRC-001` §17.2 **L1**).</summary>
    public int TierCount { get; set; }
}

public class TierPriceDto
{
    public Guid Id { get; set; }
    public Guid TierPriceSetId { get; set; }
    public string Sku { get; set; } = default!;
    public int MinQuantity { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
}

public class CreateTierPriceSetDto
{
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    public PriceListScope Scope { get; set; } = PriceListScope.All;

    [StringLength(64)]
    public string? ChannelCode { get; set; }

    /// <summary>🚨 **BẮT BUỘC** khi `Scope = ChannelShop` (nếu thiếu ⇒ NÉM: không xác định được shop ⇒ có thể áp bậc thang của shop A cho shop B).</summary>
    public Guid? ChannelShopId { get; set; }

    [StringLength(8)]
    public string CurrencyCode { get; set; } = "VND";

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public int Priority { get; set; }
}

public class AddTierPriceDto
{
    [Required]
    [StringLength(64)]
    public string Sku { get; set; } = default!;

    /// <summary>Mua từ số lượng này trở lên ⇒ đơn giá `Amount`.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "`MinQuantity` phải ≥ 1.")]
    public int MinQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
    public decimal Amount { get; set; }
}

/// <summary>
/// **Quản lý bộ bậc thang** (`PRC-002` §16.4 #2). 🔐 Quyền `Pricing.Manage` cho thao tác ghi.
/// </summary>
public interface ITierPriceSetAppService : IApplicationService
{
    Task<List<TierPriceSetDto>> GetListAsync();

    Task<TierPriceSetDto> GetAsync(Guid id);

    Task<List<TierPriceDto>> GetTiersAsync(Guid tierPriceSetId);

    Task<TierPriceSetDto> CreateAsync(CreateTierPriceSetDto input);

    /// <summary>
    /// Thêm bậc. 🚨 **NÉM `Pricing:0008`** nếu `(Sku, MinQuantity)` đã tồn tại (**ADR-02**: hai bậc cùng
    /// `MinQuantity` ⇒ không xác định được đơn giá). Hàng rào DB (unique index) là lớp **cuối**; lớp này để lỗi ra
    /// dưới **mã nghiệp vụ** thay vì lỗi unique khó hiểu.
    /// </summary>
    Task<TierPriceDto> AddTierAsync(Guid tierPriceSetId, AddTierPriceDto input);

    /// <summary>`Draft → Approved`; **NÉM** `Pricing:0002` nếu **chưa có bậc** nào.</summary>
    Task<TierPriceSetDto> PublishAsync(Guid tierPriceSetId);

    /// <summary>`Approved → Active` **nếu đã tới ngày** (chưa tới ⇒ **không** đổi, **không** ném — ADR-11).</summary>
    Task<TierPriceSetDto> ActivateAsync(Guid tierPriceSetId, DateTime? at = null);
}
