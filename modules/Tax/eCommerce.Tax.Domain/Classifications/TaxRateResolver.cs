using eCommerce.Tax.RateSchedules;
using Volo.Abp;

namespace eCommerce.Tax.Classifications;

/// <summary>
/// **Hàm phân giải thuế thuần** (pure) — không chạm DB, không phụ thuộc thời gian hệ thống.
///
/// 🎯 Vì sao tách phần này ra thành hàm thuần:
///   • Test được **mốc chuyển mức thuế** (31/12/2026 → 01/01/2027) mà không cần DB, không cần chờ tới năm 2027
///     (truyền <paramref name="at"/> vào — NFR-XC-006-004).
///   • Không có I/O ⇒ không có race condition, dễ suy luận về tính đúng đắn.
///
/// Thứ tự ưu tiên (BR-003): **SKU → Danh mục (con trước cha) → Chưa phân loại**.
/// </summary>
public static class TaxRateResolver
{
    /// <summary>Mức cơ sở mặc định khi SKU chưa được phân loại (mức phổ thông 10%).</summary>
    public const string DefaultBaseRateCode = "VAT10";

    public static TaxRateResolution Resolve(
        Guid productVariantId,
        IReadOnlyList<Guid> categoryIdsMostSpecificFirst,
        IReadOnlyList<TaxClassification> usableClassifications,
        IReadOnlyList<TaxRateSchedule> effectiveSchedules,
        DateOnly at,
        string defaultBaseRateCode = DefaultBaseRateCode)
    {
        // 1) Ưu tiên cao nhất: phân loại ở cấp SKU (mới nhất theo EffectiveFrom).
        var classification = PickLatest(
            usableClassifications.Where(c =>
                c.TargetType == TaxClassificationTargetType.Sku && c.TargetId == productVariantId));

        var source = TaxClassificationSource.Sku;

        // 2) Nếu SKU không có, đi theo danh mục từ cụ thể nhất tới tổng quát nhất.
        if (classification is null)
        {
            foreach (var categoryId in categoryIdsMostSpecificFirst)
            {
                classification = PickLatest(
                    usableClassifications.Where(c =>
                        c.TargetType == TaxClassificationTargetType.Category && c.TargetId == categoryId));

                if (classification is not null)
                {
                    source = TaxClassificationSource.Category;
                    break;
                }
            }
        }

        // 3) Chưa phân loại ⇒ mức mặc định + cờ cần phân loại.
        //    ⚠️ D5 đã chốt: vẫn CHO BÁN nhưng CHẶN phát hành hoá đơn (eCommerce:Tax:0003).
        if (classification is null)
        {
            var fallback = effectiveSchedules.FirstOrDefault(s => s.Code == defaultBaseRateCode)
                           ?? throw new BusinessException(TaxErrorCodes.NoEffectiveTaxRate)
                               .WithData("baseRateCode", defaultBaseRateCode)
                               .WithData("at", at.ToString("yyyy-MM-dd"));

            return new TaxRateResolution(
                Rate: fallback.Rate,
                RateCode: fallback.Code,
                BaseRateCode: fallback.Code,
                ReductionApplicable: false,
                ClassificationId: null,
                Source: TaxClassificationSource.Unclassified,
                NeedsClassification: true);
        }

        // 4) Mức cơ sở theo quy định.
        var baseSchedule = effectiveSchedules.FirstOrDefault(s => s.Code == classification.BaseRateCode)
                           ?? throw new BusinessException(TaxErrorCodes.NoEffectiveTaxRate)
                               .WithData("baseRateCode", classification.BaseRateCode)
                               .WithData("at", at.ToString("yyyy-MM-dd"));

        var rate = baseSchedule.Rate;
        var rateCode = baseSchedule.Code;

        // 5) Nếu SKU thuộc diện được giảm VÀ đang có mức giảm hiệu lực ⇒ áp mức giảm.
        //
        //    🎯 ĐÂY CHÍNH LÀ CƠ CHẾ TỰ CHUYỂN 8% → 10%:
        //    sau 31/12/2026, không còn mức Reduced nào hiệu lực ⇒ hàm này tự trả về mức cơ sở 10%,
        //    KHÔNG cần sửa code, KHÔNG cần release (BR-XC-006-001).
        if (classification.ReductionApplicable)
        {
            var reduced = effectiveSchedules.FirstOrDefault(s =>
                s.Kind == TaxRateKind.Reduced && s.ReducedFromCode == baseSchedule.Code);

            if (reduced is not null)
            {
                rate = reduced.Rate;
                rateCode = reduced.Code;
            }
        }

        return new TaxRateResolution(
            Rate: rate,
            RateCode: rateCode,
            BaseRateCode: baseSchedule.Code,
            ReductionApplicable: classification.ReductionApplicable,
            ClassificationId: classification.Id,
            Source: source,
            NeedsClassification: false);
    }

    /// <summary>Chọn bản ghi mới nhất theo <c>EffectiveFrom</c> khi có nhiều bản ghi cùng đối tượng.</summary>
    private static TaxClassification? PickLatest(IEnumerable<TaxClassification> items)
        => items.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefault();
}
