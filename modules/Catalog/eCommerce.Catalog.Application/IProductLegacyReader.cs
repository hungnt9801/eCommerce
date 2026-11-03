namespace eCommerce.Catalog;

/// <summary>
/// **ADAPTER ĐỌC CŨ** (WP-03 mục 7, D-07) — trả lời câu hỏi *"sản phẩm này có dòng nào bán được"*
/// theo hình dạng mà các đường đọc **trước** mô hình Nhanh hiểu được (mã tra giá + barcode + giá).
///
/// 🚨 **VÌ SAO LÀ MỘT HỢP ĐỒNG RIÊNG, KHÔNG PHẢI `IProductAppService`:** app service mang
/// `[Authorize(Products.Default)]` ở **cấp class** (`XC-004` D3) ⇒ gọi nó từ trang **công khai** (storefront)
/// làm khách ẩn danh nhận `AbpAuthorizationException` ⇒ **HTTP 500** (lỗi đã xảy ra thật với `/sitemap.xml`,
/// xem `StorefrontCongKhaiTests`). Lớp này **cố ý không** mang `[Authorize]` và **chỉ đọc** — đúng khuôn
/// `DocSanPhamCongKhai` (đọc repository, không quyền).
///
/// 🔒 **BA RÀNG BUỘC:** (1) chỉ trả dòng **đang bán**; (2) **không** đọc/trả giá vốn; (3) **không** đọc tồn kho.
/// </summary>
public interface IProductLegacyReader
{
    /// <summary>
    /// Các dòng bán được (hình dạng cũ) của những sản phẩm được hỏi — **một truy vấn cho cả trang**.
    /// 📌 Thứ tự nguồn: **biến thể cũ trước, ĐVT gốc sau** (xem `ProductLegacyAdapter`).
    /// ⚠️ Sản phẩm không có nguồn nào **không** xuất hiện trong kết quả (không có dòng rỗng).
    /// </summary>
    Task<IReadOnlyList<LegacySellableLineDto>> GetSellableLinesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra **một** sản phẩm theo **mã cũ**: SKU của biến thể **hoặc** mã sản phẩm (`Product.Code`).
    ///
    /// 📌 **VÌ SAO CẦN CẢ HAI:** URL cũ `/SanPham/ChiTiet/{sku}` đã ra ngoài (link đã chia sẻ/index) ⇒ phải
    /// giữ tra theo **SKU biến thể**; sau backfill, sản phẩm **con** không có biến thể mà mang `Code = SKU cũ`
    /// ⇒ tra theo `Code` là đường duy nhất tìm được nó. Trả `null` khi không thấy (**không** ném).
    /// </summary>
    Task<Guid?> FindProductIdByCodeOrSkuAsync(string ma, CancellationToken cancellationToken = default);
}

/// <summary>
/// Một dòng bán được ở **hình dạng cũ**: `Sku` + `Barcode` + `Price`.
/// 📌 Không có giá vốn, không có tồn kho, không có `TenantId` — đây là DTO dùng cho đường đọc công khai.
/// </summary>
public class LegacySellableLineDto
{
    public Guid ProductId { get; set; }

    /// <summary>Khoá tra bảng giá cũ: SKU của biến thể, hoặc **mã sản phẩm** khi dòng đến từ ĐVT gốc.</summary>
    public string Sku { get; set; } = default!;

    public string? Barcode { get; set; }

    public decimal Price { get; set; }

    /// <summary>`Variant` (mô hình cũ) hoặc `Unit` (mô hình Nhanh) — để tầng hiển thị biết nguồn của giá.</summary>
    public string Source { get; set; } = default!;
}
