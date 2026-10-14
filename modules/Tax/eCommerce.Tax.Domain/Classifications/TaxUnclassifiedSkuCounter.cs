using eCommerce.Tax.RateSchedules;

namespace eCommerce.Tax.Classifications;

/// <summary>Một SKU cần xét khi đếm — chỉ cần Id và trạng thái đang bán.</summary>
public sealed record SkuCanDem(Guid ProductVariantId, bool IsActive);

/// <summary>Kết quả đếm SKU chưa phân loại thuế.</summary>
public sealed record KetQuaDemSku(
    int TongSoSkuDangBan,
    int SoChuaPhanLoai,
    IReadOnlyList<Guid> SkuChuaPhanLoai);

/// <summary>
/// **LÕI THUẦN đếm SKU chưa phân loại thuế** — nguồn số liệu cho gauge `tax_unclassified_skus`.
///
/// 🎯 VÌ SAO CẦN CON SỐ NÀY: quyết định **D5** đã chốt — SKU chưa phân loại **vẫn cho bán** nhưng
/// **CHẶN phát hành hoá đơn** (`eCommerce:Tax:0003`). Nghĩa là SKU chưa phân loại là **việc đang tồn
/// của kế toán**, và nó **chặn doanh thu hợp pháp** khi khách yêu cầu hoá đơn. Không đo được con số này
/// thì không ai biết đang tồn bao nhiêu, và **nó chỉ lộ ra khi có khách xin hoá đơn** — lúc đó đã muộn.
///
/// 🚨 GIỚI HẠN ĐÃ BIẾT (ghi rõ để KHÔNG hiểu nhầm con số):
///   Danh mục sản phẩm hiện **CHƯA có khái niệm `Category`** (`Product` chỉ có `BrandId`;
///   `ProductVariant` không có `CategoryId`). Vì vậy chuỗi danh mục truyền vào resolver là **rỗng**,
///   và phân loại hiện chỉ có thể ở **cấp SKU**. Hệ quả: khi danh mục được thêm vào, **con số này sẽ
///   GIẢM** vì nhiều SKU sẽ được phân loại theo danh mục — đó là điều đúng, không phải lỗi.
///   Resolver đã sẵn sàng cho cấp danh mục; chỉ cần nơi gọi truyền chuỗi danh mục vào.
/// </summary>
public static class TaxUnclassifiedSkuCounter
{
    /// <summary>
    /// Giá trị **"CHƯA XÁC ĐỊNH"** cho gauge.
    ///
    /// 🚨 VÌ SAO KHÔNG ĐỂ 0 KHI ĐẾM LỖI: gauge mặc định là 0, mà `0` mang nghĩa
    /// **"kế toán không còn việc gì"**. Nếu lần đếm **thất bại** mà gauge vẫn là 0 thì hệ thống
    /// **nói dối một cách im lặng** — đúng loại lỗi tệ nhất. Dùng `-1` để phân biệt
    /// *"không có SKU nào chưa phân loại"* với *"chưa đếm được"*.
    /// </summary>
    public const long ChuaXacDinh = -1;

    /// <summary>
    /// Đếm SKU **đang bán** chưa phân loại thuế tại ngày <paramref name="at"/>.
    ///
    /// 🔒 Chỉ xét SKU **đang bán** (`IsActive`): SKU đã ngừng bán **không phải việc của kế toán**
    /// — tính vào sẽ làm con số phình ra và **che mất** những SKU thật sự đang chặn hoá đơn.
    /// </summary>
    public static KetQuaDemSku Dem(
        IEnumerable<SkuCanDem> skus,
        IReadOnlyList<TaxClassification> usableClassifications,
        IReadOnlyList<TaxRateSchedule> effectiveSchedules,
        DateOnly at)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var dangBan = skus.Where(x => x.IsActive).ToList();
        var chuaPhanLoai = new List<Guid>();

        foreach (var sku in dangBan)
        {
            // Chuỗi danh mục RỖNG — xem "GIỚI HẠN ĐÃ BIẾT" ở đầu class.
            var ketQua = TaxRateResolver.Resolve(
                sku.ProductVariantId,
                Array.Empty<Guid>(),
                usableClassifications ?? Array.Empty<TaxClassification>(),
                effectiveSchedules ?? Array.Empty<TaxRateSchedule>(),
                at);

            if (ketQua.NeedsClassification)
            {
                chuaPhanLoai.Add(sku.ProductVariantId);
            }
        }

        return new KetQuaDemSku(dangBan.Count, chuaPhanLoai.Count, chuaPhanLoai);
    }
}
