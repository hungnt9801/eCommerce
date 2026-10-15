using eCommerce.Kernel.Money;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// App service **quản lý bộ bậc thang** (`PRC-002`).
///
/// 🛡️ **Lưới an toàn ở tầng này:**
///   • **append-only**: ghi **phải** gọi `YeuCauChoPhepSuaNoiDung()` trước;
///   • **số bậc đọc từ DB** khi phát hành (bài học `PRC-001` §17.2 **L1**);
///   • **chặn trùng `(Sku, MinQuantity)` bằng mã `0008`** (ADR-02) — hàng rào DB là lớp cuối, lớp này để
///     lỗi ra dưới **mã nghiệp vụ** thay vì lỗi unique khó hiểu.
/// </summary>
[Authorize(PricingPermissions.Default)]
public class TierPriceSetAppService : ApplicationService, ITierPriceSetAppService
{
    private readonly IRepository<TierPriceSet, Guid> _boBacRepository;
    private readonly IRepository<TierPrice, Guid> _bacRepository;

    public TierPriceSetAppService(
        IRepository<TierPriceSet, Guid> boBacRepository,
        IRepository<TierPrice, Guid> bacRepository)
    {
        _boBacRepository = boBacRepository;
        _bacRepository = bacRepository;
    }

    public async Task<List<TierPriceSetDto>> GetListAsync()
    {
        var ds = await _boBacRepository.GetListAsync();
        var tatCaBac = await _bacRepository.GetListAsync();

        var dem = tatCaBac.GroupBy(x => x.TierPriceSetId).ToDictionary(g => g.Key, g => g.Count());

        return ds.OrderByDescending(x => x.EffectiveFrom).ThenBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => ToDto(x, dem.TryGetValue(x.Id, out var n) ? n : 0))
            .ToList();
    }

    public async Task<TierPriceSetDto> GetAsync(Guid id)
    {
        var bo = await _boBacRepository.GetAsync(id);
        return ToDto(bo, (int)await _bacRepository.CountAsync(x => x.TierPriceSetId == id));
    }

    public async Task<List<TierPriceDto>> GetTiersAsync(Guid tierPriceSetId)
    {
        _ = await _boBacRepository.GetAsync(tierPriceSetId);

        var bac = await _bacRepository.GetListAsync(x => x.TierPriceSetId == tierPriceSetId);

        return bac.OrderBy(x => x.Sku, StringComparer.Ordinal).ThenBy(x => x.MinQuantity)
            .Select(ToDto).ToList();
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<TierPriceSetDto> CreateAsync(CreateTierPriceSetDto input)
    {
        if (!Currency.TryFromCode(input.CurrencyCode, out var tien) || tien is null)
        {
            throw new BusinessException(PricingErrorCodes.LechLoaiTien)
                .WithData("ma", input.CurrencyCode)
                .WithData("lyDo", "Mã tiền tệ không được đăng ký.");
        }

        var bo = new TierPriceSet(
            GuidGenerator.Create(), input.Code, input.Name, input.Scope, tien,
            input.EffectiveFrom, input.ChannelCode, input.ChannelShopId, input.EffectiveTo)
        {
            Priority = input.Priority
        };

        await _boBacRepository.InsertAsync(bo, autoSave: true);

        Logger.LogInformation("Đã tạo bộ bậc thang {Code} ({Scope}/{Kenh}/{Shop}) ở trạng thái {Status}.",
            bo.Code, bo.Scope, bo.ChannelCode ?? "(mọi kênh)", bo.ChannelShopId, bo.Status);

        return ToDto(bo, 0);
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<TierPriceDto> AddTierAsync(Guid tierPriceSetId, AddTierPriceDto input)
    {
        var bo = await _boBacRepository.GetAsync(tierPriceSetId);

        bo.YeuCauChoPhepSuaNoiDung();

        var sku = input.Sku.Trim();

        // 🚨 **ADR-02 — chặn TRÙNG `MinQuantity` bằng MÃ NGHIỆP VỤ** (không để lỗi unique `23505` lộ ra client):
        // hai bậc cùng `MinQuantity` ⇒ `ChonBacThang` **NÉM `0008`** ⇒ **checkout chết** cho SKU đó.
        var daCo = await _bacRepository.FirstOrDefaultAsync(
            x => x.TierPriceSetId == tierPriceSetId && x.Sku == sku && x.MinQuantity == input.MinQuantity);

        if (daCo is not null)
        {
            throw new BusinessException(PricingErrorCodes.BacThangTrungMinQuantity)
                .WithData("sku", sku)
                .WithData("minQuantity", input.MinQuantity)
                .WithData("tierPriceSetId", tierPriceSetId)
                .WithData("lyDo", "Đã có bậc với CÙNG `MinQuantity` cho SKU này trong bộ (PRC-002 ADR-02).");
        }

        var bac = new TierPrice(
            GuidGenerator.Create(), bo.Id, sku, input.MinQuantity,
            Money.Cua(input.Amount, bo.Currency), bo.Currency);

        await _bacRepository.InsertAsync(bac, autoSave: true);

        Logger.LogInformation("Đã thêm bậc {Sku} từ {MinQuantity} ⇒ {Amount} vào bộ {Code}.",
            bac.Sku, bac.MinQuantity, bac.AmountValue, bo.Code);

        return ToDto(bac);
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<TierPriceSetDto> PublishAsync(Guid tierPriceSetId)
    {
        var bo = await _boBacRepository.GetAsync(tierPriceSetId);

        // Số bậc đọc từ **DB** — bài học `PRC-001` §17.2 **L1**.
        var soBac = (int)await _bacRepository.CountAsync(x => x.TierPriceSetId == tierPriceSetId);

        bo.PhatHanh(soBac);

        await _boBacRepository.UpdateAsync(bo, autoSave: true);

        Logger.LogInformation("Đã PHÁT HÀNH bộ bậc thang {Code} ({SoBac} bậc).", bo.Code, soBac);

        return ToDto(bo, soBac);
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<TierPriceSetDto> ActivateAsync(Guid tierPriceSetId, DateTime? at = null)
    {
        var bo = await _boBacRepository.GetAsync(tierPriceSetId);

        var thoiDiem = at ?? Clock.Now;

        if (bo.KichHoatNeuToiNgay(thoiDiem))
        {
            await _boBacRepository.UpdateAsync(bo, autoSave: true);
            Logger.LogInformation("Đã KÍCH HOẠT bộ bậc thang {Code} tại {At}.", bo.Code, thoiDiem);
        }

        return ToDto(bo, (int)await _bacRepository.CountAsync(x => x.TierPriceSetId == tierPriceSetId));
    }

    private static TierPriceSetDto ToDto(TierPriceSet bo, int soBac) => new()
    {
        Id = bo.Id,
        Code = bo.Code,
        Name = bo.Name,
        Scope = bo.Scope.ToString(),
        ChannelCode = bo.ChannelCode,
        ChannelShopId = bo.ChannelShopId,
        CurrencyCode = bo.Currency.Code,
        EffectiveFrom = bo.EffectiveFrom,
        EffectiveTo = bo.EffectiveTo,
        Priority = bo.Priority,
        Status = bo.Status.ToString(),
        TierCount = soBac
    };

    private static TierPriceDto ToDto(TierPrice bac) => new()
    {
        Id = bac.Id,
        TierPriceSetId = bac.TierPriceSetId,
        Sku = bac.Sku,
        MinQuantity = bac.MinQuantity,
        Amount = bac.AmountValue,
        CurrencyCode = bac.CurrencyCode
    };
}
