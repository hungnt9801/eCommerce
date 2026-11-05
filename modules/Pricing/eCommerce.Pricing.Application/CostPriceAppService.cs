using eCommerce.Pricing.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`S12` — ĐỌC GIÁ VỐN** (`PRC-001` §5.2/§6.1, bảng `PrcCostPrices`). **CHỈ ĐỌC.**
///
/// 🚨 **BR-013 — BA ĐIỀU BẮT BUỘC, KHÔNG ĐƯỢC NỚI:**
///   1. **Quyền RIÊNG** `eCommerce.Pricing.CostPrice.View` ở **cấp CLASS** — không dùng chung `Pricing.Default`
///      (xem chú thích ở `PricingPermissions.CostPriceView`: xem giá bán ≠ xem biên lợi nhuận).
///   2. **Không** method nào trả giá vốn ra đường công khai/storefront. Test quét nguồn
///      (`StorefrontCongKhaiTests`) + E2E `S8-W3` (HTML công khai **không** chứa giá trị giá vốn) canh việc này.
///   3. **Không** có method ghi — nguồn giá vốn là **câu hỏi mở `Q2`** (nhập tay / import NCC / bình quân
///      gia quyền). 📌 Đường ghi "tạm" sẽ tạo **hai nguồn sự thật** cho cùng một con số.
///
/// 📌 **VÌ SAO TRẢ `Amount` + `CurrencyCode` (không trả DTO lồng `Money`):** `Money` là lớp của
/// `eCommerce.Kernel` với ctor riêng; serialize thẳng nó sẽ **lộ chi tiết lưu trữ** vào hợp đồng API. Cùng
/// lựa chọn với `PriceListItemDto`/`ChannelPriceDto` — client đọc cặp `(amount, currencyCode)`.
/// </summary>
[Authorize(PricingPermissions.CostPriceView)]
public class CostPriceAppService : ApplicationService, ICostPriceAppService
{
    private readonly IRepository<CostPrice, Guid> _giaVonRepository;

    public CostPriceAppService(IRepository<CostPrice, Guid> giaVonRepository)
    {
        _giaVonRepository = giaVonRepository;
    }

    public async Task<List<CostPriceDto>> GetListAsync(string? sku = null, DateOnly? at = null)
    {
        // 📌 Lọc SKU Ở TẦNG DB (index `(TenantId, Sku, EffectiveTo)`), lọc theo NGÀY ở bộ nhớ vì số dòng của
        // một SKU là nhỏ và `DateOnly` so sánh trong LINQ-to-Objects **không phụ thuộc provider**.
        // ⚠️ KHÔNG bỏ tham số ngày: `PRC-001` BR-003 **cấm** truy vấn giá "mới nhất" không kèm ngày —
        // *"mới nhất"* trả lời một câu hỏi khác câu người dùng đang hỏi.
        var danhSach = string.IsNullOrWhiteSpace(sku)
            ? await _giaVonRepository.GetListAsync()
            : await _giaVonRepository.GetListAsync(x => x.Sku == sku.Trim());

        if (at is { } ngay)
        {
            danhSach = danhSach.Where(x => x.CoHieuLucTai(ngay)).ToList();
        }

        if (danhSach.Count == 0)
        {
            // 📌 Log ở mức Information **không** chứa số tiền (NFR-009: không log giá vốn).
            Logger.LogInformation("Không có dòng giá vốn nào khớp bộ lọc (sku={Sku}, at={At}).", sku, at);
        }

        return danhSach
            .OrderBy(x => x.Sku, StringComparer.Ordinal)
            .ThenByDescending(x => x.EffectiveFrom)
            .Select(ToDto)
            .ToList();
    }

    public async Task<CostPriceDto> GetAsync(Guid id)
    {
        // 🚨 `GetAsync` **NÉM** khi không thấy (không trả `null` im lặng) — client phải phân biệt được
        // *"không có dòng này"* với *"trang hỏng"*.
        var giaVon = await _giaVonRepository.GetAsync(id);

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
