using eCommerce.Pricing.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Pricing;

/// <summary>
/// 🆕 **`W17-2` — HIỆN THỰC PORT ĐỌC GIÁ VỐN** (`PRC-001-arch` §3.1).
///
/// 📌 `ITransientDependency` để ABP tự đăng ký; **KHÔNG** là `IApplicationService` ⇒ **không** lộ endpoint,
/// **không** kiểm quyền (quyền ở app service/HTTP). Đọc `IRepository<CostPrice>` dưới tenant filter hiện tại.
/// 🔒 **CHỈ** dòng `DaDuyet` (`ApprovedBy != null && ApprovedAtUtc != null`) + `CoHieuLucTai(at)` (`D-177` ④).
/// 📌 Khi **không** có dòng ⇒ trả `null` để bên gọi đặt `CostMissing = true` (**CẤM ghi `0`** — `D-177` ⑥).
/// </summary>
public class CostPriceLookupAdapter : ICostPriceLookup, ITransientDependency
{
    private readonly IRepository<CostPrice, Guid> _giaVonRepository;

    public CostPriceLookupAdapter(IRepository<CostPrice, Guid> giaVonRepository)
    {
        _giaVonRepository = giaVonRepository;
    }

    /// <inheritdoc />
    public async Task<CostPriceLookupDto?> GetApprovedCostAsync(string sku, DateOnly at)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        var maSku = sku.Trim();

        var daDuyet = await _giaVonRepository.GetListAsync(
            x => x.Sku == maSku && x.ApprovedBy != null && x.ApprovedAtUtc != null);

        var dong = ChonHieuLuc(daDuyet, at);

        return dong is null ? null : ToDto(dong, maSku, at);
    }

    /// <inheritdoc />
    public async Task<List<CostPriceLookupDto>> GetApprovedCostManyAsync(GetApprovedCostManyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Items.Count == 0)
        {
            return new List<CostPriceLookupDto>();
        }

        var cacSku = input.Items
            .Select(x => x.Sku?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var tatCa = await _giaVonRepository.GetListAsync(
            x => cacSku.Contains(x.Sku) && x.ApprovedBy != null && x.ApprovedAtUtc != null);

        var theoDauSku = tatCa
            .GroupBy(x => x.Sku, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var ketQua = new List<CostPriceLookupDto>(input.Items.Count);

        foreach (var item in input.Items)
        {
            var maSku = item.Sku?.Trim() ?? string.Empty;

            var dong = maSku.Length > 0 && theoDauSku.TryGetValue(maSku, out var ungVien)
                ? ChonHieuLuc(ungVien, item.At)
                : null;

            // 📌 Ở đường LÔ vẫn trả MỘT DTO cho mỗi đầu vào (giữ thứ tự) — thiếu ⇒ `AmountValue = null`
            //    (`CoGiaVon = false`) để bên gọi đặt `CostMissing`, KHÔNG bỏ dòng (bỏ dòng = lặng lẽ mất SKU).
            ketQua.Add(dong is null
                ? new CostPriceLookupDto { Sku = maSku, At = item.At, AmountValue = null, CurrencyCode = null }
                : ToDto(dong, maSku, item.At));
        }

        return ketQua;
    }

    /// <summary>Chọn dòng hiệu lực tại `at`; nếu (dữ liệu bẩn) có nhiều dòng ⇒ lấy `EffectiveFrom` **mới nhất**.</summary>
    private static CostPrice? ChonHieuLuc(IEnumerable<CostPrice> ungVien, DateOnly at)
        => ungVien
            .Where(x => x.CoHieuLucTai(at))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault();

    private static CostPriceLookupDto ToDto(CostPrice giaVon, string sku, DateOnly at) => new()
    {
        Sku = sku,
        At = at,
        AmountValue = giaVon.AmountValue,
        CurrencyCode = giaVon.CurrencyCode
    };
}
