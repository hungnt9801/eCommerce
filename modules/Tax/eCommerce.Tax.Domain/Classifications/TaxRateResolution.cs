namespace eCommerce.Tax.Classifications;

/// <summary>
/// Kết quả phân giải thuế suất — kèm **nguồn** đã dùng để có thể **giải trình với cơ quan thuế**.
/// </summary>
/// <param name="Rate">Thuế suất **phần trăm** thực tế (8.0000 hoặc 10.0000…).</param>
/// <param name="RateCode">Mã mức thực tế được áp (ví dụ <c>VAT8</c>).</param>
/// <param name="BaseRateCode">Mã mức cơ sở theo quy định (ví dụ <c>VAT10</c>).</param>
/// <param name="ReductionApplicable">SKU này có thuộc diện được giảm hay không.</param>
/// <param name="ClassificationId">Bản ghi phân loại đã dùng (<c>null</c> nếu chưa phân loại).</param>
/// <param name="Source">Đã lấy từ SKU / danh mục / mặc định / chưa phân loại.</param>
/// <param name="NeedsClassification">
/// <c>true</c> ⇒ SKU chưa được phân loại ⇒ theo **D5 đã chốt**: vẫn **cho bán** nhưng
/// **CHẶN phát hành hoá đơn** (mã lỗi <c>eCommerce:Tax:0003</c>).
/// </param>
public sealed record TaxRateResolution(
    decimal Rate,
    string RateCode,
    string BaseRateCode,
    bool ReductionApplicable,
    Guid? ClassificationId,
    TaxClassificationSource Source,
    bool NeedsClassification);

/// <summary>
/// Cổng vào duy nhất để mọi module khác hỏi "SKU này đang chịu thuế suất bao nhiêu?".
/// Dùng bởi <c>Sales</c> (tính thuế khi checkout/đặt hàng) và <c>EInvoice</c> (lập hoá đơn).
/// </summary>
public interface ITaxRateResolver
{
    /// <param name="productVariantId">SKU cần tính.</param>
    /// <param name="categoryIdsMostSpecificFirst">
    /// Danh mục của SKU, **sắp xếp từ cụ thể nhất tới tổng quát nhất** (con trước cha).
    /// Thứ tự này quyết định ưu tiên khi SKU không có phân loại riêng (BR-003).
    /// </param>
    /// <param name="at">Ngày cần tính (dùng <c>DateOnly</c> để khớp khung hiệu lực theo ngày).</param>
    Task<TaxRateResolution> ResolveEffectiveRateAsync(
        Guid productVariantId,
        IReadOnlyList<Guid> categoryIdsMostSpecificFirst,
        DateOnly at,
        CancellationToken cancellationToken = default);
}
