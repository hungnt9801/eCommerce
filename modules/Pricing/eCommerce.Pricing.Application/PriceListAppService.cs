using eCommerce.Kernel.Money;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// App service **quản lý bảng giá** (`PRC-001` §6.1).
///
/// 🛡️ **Lưới an toàn ở tầng này:**
///   • **append-only**: mọi thao tác ghi **phải** gọi `YeuCauChoPhepSuaNoiDung()` trước (BR-004).
///   • **số dòng đọc từ DB**, KHÔNG từ thuộc tính trên entity — xem <see cref="PublishAsync"/>.
/// </summary>
[Authorize(PricingPermissions.Default)]
public class PriceListAppService : ApplicationService, IPriceListAppService
{
    private readonly IRepository<PriceList, Guid> _bangGiaRepository;
    private readonly IRepository<PriceListItem, Guid> _dongGiaRepository;

    public PriceListAppService(
        IRepository<PriceList, Guid> bangGiaRepository,
        IRepository<PriceListItem, Guid> dongGiaRepository)
    {
        _bangGiaRepository = bangGiaRepository;
        _dongGiaRepository = dongGiaRepository;
    }

    public async Task<List<PriceListDto>> GetListAsync()
    {
        var danhSach = await _bangGiaRepository.GetListAsync();
        var tatCaDong = await _dongGiaRepository.GetListAsync();

        var demTheoBang = tatCaDong
            .GroupBy(x => x.PriceListId)
            .ToDictionary(g => g.Key, g => g.Count());

        return danhSach
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => ToDto(x, demTheoBang.TryGetValue(x.Id, out var n) ? n : 0))
            .ToList();
    }

    public async Task<PriceListDto> GetAsync(Guid id)
    {
        var bg = await _bangGiaRepository.GetAsync(id);
        var soDong = await _dongGiaRepository.CountAsync(x => x.PriceListId == id);

        return ToDto(bg, (int)soDong);
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<PriceListDto> CreateAsync(CreatePriceListDto input)
    {
        if (!Currency.TryFromCode(input.CurrencyCode, out var tien) || tien is null)
        {
            throw new BusinessException(PricingErrorCodes.LechLoaiTien)
                .WithData("ma", input.CurrencyCode)
                .WithData("lyDo", "Mã tiền tệ không được đăng ký.");
        }

        var bg = new PriceList(
            GuidGenerator.Create(), input.Code, input.Name, input.Scope, tien,
            input.EffectiveFrom, input.ChannelCode, input.ChannelShopId, input.EffectiveTo)
        {
            Priority = input.Priority
        };

        await _bangGiaRepository.InsertAsync(bg, autoSave: true);

        Logger.LogInformation("Đã tạo bảng giá {Code} ({Scope}) ở trạng thái {Status}.", bg.Code, bg.Scope, bg.Status);

        return ToDto(bg, 0);
    }

    public async Task<List<PriceListItemDto>> GetItemsAsync(Guid priceListId)
    {
        _ = await _bangGiaRepository.GetAsync(priceListId);

        var dong = await _dongGiaRepository.GetListAsync(x => x.PriceListId == priceListId);

        return dong
            .OrderBy(x => x.Sku, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<PriceListItemDto> AddItemAsync(Guid priceListId, AddPriceListItemDto input)
    {
        var bg = await _bangGiaRepository.GetAsync(priceListId);

        // 🚨 BR-004/append-only: bảng giá đã/đang hiệu lực **KHÔNG** sửa được — phải tạo bảng giá MỚI.
        bg.YeuCauChoPhepSuaNoiDung();

        var tien = Money.Cua(input.Amount, bg.Currency);

        var daCo = await _dongGiaRepository.FirstOrDefaultAsync(
            x => x.PriceListId == priceListId && x.Sku == input.Sku);

        if (daCo is not null)
        {
            // Idempotent theo **cùng SKU**: cập nhật giá trị (bảng giá còn `Draft` nên hợp lệ).
            daCo.DoiGia(tien, bg.Currency);
            await _dongGiaRepository.UpdateAsync(daCo, autoSave: true);

            return ToDto(daCo);
        }

        var dong = new PriceListItem(GuidGenerator.Create(), bg.Id, input.Sku, tien, bg.Currency);

        await _dongGiaRepository.InsertAsync(dong, autoSave: true);

        Logger.LogInformation("Đã thêm dòng giá {Sku} = {Amount} vào bảng giá {Code}.", dong.Sku, dong.AmountValue, bg.Code);

        return ToDto(dong);
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<PriceListDto> PublishAsync(Guid priceListId)
    {
        var bg = await _bangGiaRepository.GetAsync(priceListId);

        // 🚨 Số dòng đọc từ **DB** (nguồn sự thật) — KHÔNG từ thuộc tính trên entity.
        // Lỗi thật đã gặp: phiên bản đầu lưu `SoDong` trên entity nhưng **không map** ⇒ sau khi nạp lại từ DB
        // nó **luôn = 0** ⇒ mọi lần publish đều **thất bại** mà nhìn như "đúng luật".
        var soDong = (int)await _dongGiaRepository.CountAsync(x => x.PriceListId == priceListId);

        bg.PhatHanh(soDong);

        await _bangGiaRepository.UpdateAsync(bg, autoSave: true);

        Logger.LogInformation("Đã PHÁT HÀNH bảng giá {Code} ({SoDong} dòng).", bg.Code, soDong);

        return ToDto(bg, soDong);
    }

    [Authorize(PricingPermissions.Manage)]
    public async Task<PriceListDto> ActivateAsync(Guid priceListId, DateTime? at = null)
    {
        var bg = await _bangGiaRepository.GetAsync(priceListId);

        var thoiDiem = at ?? Clock.Now;

        // ADR-11: "chưa tới ngày" ⇒ trả `false`, **KHÔNG** ném.
        var daKichHoat = bg.KichHoatNeuToiNgay(thoiDiem);

        if (daKichHoat)
        {
            await _bangGiaRepository.UpdateAsync(bg, autoSave: true);
            Logger.LogInformation("Đã KÍCH HOẠT bảng giá {Code} tại {At}.", bg.Code, thoiDiem);
        }

        var soDong = (int)await _dongGiaRepository.CountAsync(x => x.PriceListId == priceListId);

        return ToDto(bg, soDong);
    }

    private static PriceListDto ToDto(PriceList bg, int soDong) => new()
    {
        Id = bg.Id,
        Code = bg.Code,
        Name = bg.Name,
        Scope = bg.Scope.ToString(),
        ChannelCode = bg.ChannelCode,
        ChannelShopId = bg.ChannelShopId,
        CurrencyCode = bg.Currency.Code,
        EffectiveFrom = bg.EffectiveFrom,
        EffectiveTo = bg.EffectiveTo,
        Priority = bg.Priority,
        Status = bg.Status.ToString(),
        ItemCount = soDong
    };

    private static PriceListItemDto ToDto(PriceListItem dong) => new()
    {
        Id = dong.Id,
        PriceListId = dong.PriceListId,
        Sku = dong.Sku,
        Amount = dong.AmountValue,
        CurrencyCode = dong.CurrencyCode
    };
}
