using eCommerce.Kernel.Money;
using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — ĐƯỜNG GHI + DUYỆT GIÁ VỐN** (`PRC-001-arch` §3.2/§5.1).
///
/// 🔐 `[Authorize]` ở **cấp CLASS** (bắt buộc — `BatBienPhanQuyenTests.TC01`); hai method mang policy riêng
/// `.CostPrice.Manage` / `.CostPrice.Approve` (gắn ở method nhưng **kèm** `[Authorize]` cấp class ⇒ vẫn khoá).
///
/// 🔒 **GIỮ 2 BƯỚC** (`Q4`/`PRC-001-AQ1` **còn `Open`**): `CreateAsync` tạo `DaDuyet = false`; `ApproveAsync`
/// gọi `CostPrice.Duyet`. Nếu kế toán chốt "không 2 bước" ⇒ **chỉ** thêm `Duyet(...)` trong `CreateAsync` (0 schema).
///
/// 📌 **Idempotency tạo** (`XC-002`/§5.6): `IdempotencyKey` **BẮT BUỘC**; cùng khoá ⇒ **trả dòng cũ**
/// (pre-check) và **unique filtered** `(TenantId, IdempotencyKey)` là **hàng rào cuối** ở DB.
/// </summary>
[Authorize]
public class CostPriceAdminAppService : ApplicationService, ICostPriceAdminAppService
{
    private readonly IRepository<CostPrice, Guid> _giaVonRepository;

    public CostPriceAdminAppService(IRepository<CostPrice, Guid> giaVonRepository)
    {
        _giaVonRepository = giaVonRepository;
    }

    /// <inheritdoc />
    [Authorize(PricingPermissions.CostPriceManage)]
    public async Task<CostPriceDto> CreateAsync(CreateCostPriceDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.IdempotencyKey))
        {
            // 🚨 Thiếu khoá ⇒ mỗi lần retry sinh một dòng trùng ⇒ **NÉM** (không tạo "tạm").
            throw new BusinessException(PricingErrorCodes.ThieuKhoaIdempotency)
                .WithData("sku", input.Sku)
                .WithData("lyDo", "Đường ghi giá vốn BẮT BUỘC kèm `IdempotencyKey` (XC-002).");
        }

        var khoa = input.IdempotencyKey.Trim();

        // 📌 Pre-check: cùng khoá ⇒ trả dòng đã tạo (không tạo thứ hai). Hàng rào CUỐI vẫn là unique DB.
        var daCo = await _giaVonRepository.FirstOrDefaultAsync(x => x.IdempotencyKey == khoa);

        if (daCo is not null)
        {
            Logger.LogInformation("Giá vốn đã tồn tại cho khoá idempotency ⇒ trả dòng cũ (sku={Sku}).", daCo.Sku);

            return ToDto(daCo);
        }

        var tien = Money.Cua(input.AmountValue, Currency.FromCode(input.CurrencyCode));

        // `CostPrice..ctor` NÉM `0009` nếu SKU rỗng / tiền âm / kỳ ngược (giá vốn `0` HỢP LỆ).
        var giaVon = new CostPrice(
            GuidGenerator.Create(), input.Sku, tien, input.EffectiveFrom, input.EffectiveTo);

        giaVon.GanKhoaIdempotency(khoa);

        await _giaVonRepository.InsertAsync(giaVon, autoSave: false);

        return ToDto(giaVon);
    }

    /// <inheritdoc />
    [Authorize(PricingPermissions.CostPriceApprove)]
    public async Task<CostPriceDto> ApproveAsync(Guid id)
    {
        var giaVon = await _giaVonRepository.GetAsync(id);

        // 🚨 `Duyet` NÉM `0009` khi thiếu người duyệt / **ĐÃ duyệt** (`D12` — không no-op im lặng).
        giaVon.Duyet(CurrentUser.Id ?? Guid.Empty, Clock.Now);

        await _giaVonRepository.UpdateAsync(giaVon, autoSave: false);

        return ToDto(giaVon);
    }

    private static CostPriceDto ToDto(CostPrice giaVon) => new()
    {
        Id = giaVon.Id,
        Sku = giaVon.Sku,
        Amount = giaVon.AmountValue,
        CurrencyCode = giaVon.CurrencyCode,
        EffectiveFrom = giaVon.EffectiveFrom,
        EffectiveTo = giaVon.EffectiveTo,
        ApprovedBy = giaVon.ApprovedBy,
        ApprovedAtUtc = giaVon.ApprovedAtUtc,
        DaDuyet = giaVon.DaDuyet
    };
}
