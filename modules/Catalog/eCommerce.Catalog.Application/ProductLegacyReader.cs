using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Catalog;

/// <summary>
/// Hiện thực <see cref="IProductLegacyReader"/> — **CHỈ ĐỌC**, không quyền, không phụ thuộc Host
/// (WP-03 mục 7: adapter để các đường đọc cũ không phải biết mô hình Nhanh tồn tại).
///
/// 🔒 **BA truy vấn cố định cho cả trang** (không N+1): sản phẩm (chỉ `Id`+`Code`) → biến thể → ĐVT.
/// 📌 Đánh đổi đã biết: storefront đã nạp `Product` trước đó, nên `Id`+`Code` được nạp **lần hai** — chấp nhận
/// để hợp đồng này **không** lộ entity ra ngoài module (tầng Web không được cầm `Product`). Khi cần tối ưu thì
/// truyền thẳng danh sách sản phẩm đã nạp — nhưng khi đó `IProductLegacyReader` phải nằm trong `Domain` và
/// Web phải tham chiếu entity Catalog (đánh đổi ngược lại).
/// </summary>
public class ProductLegacyReader : IProductLegacyReader, ITransientDependency
{
    private readonly IRepository<Product, Guid> _sanPham;
    private readonly IRepository<ProductVariant, Guid> _bienThe;
    private readonly IRepository<ProductUnit, Guid> _donVi;

    public ProductLegacyReader(
        IRepository<Product, Guid> sanPham,
        IRepository<ProductVariant, Guid> bienThe,
        IRepository<ProductUnit, Guid> donVi)
    {
        _sanPham = sanPham;
        _bienThe = bienThe;
        _donVi = donVi;
    }

    public async Task<IReadOnlyList<LegacySellableLineDto>> GetSellableLinesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var ids = productIds.ToList();

        // 🔎 Chỉ nạp sản phẩm **trong trang** (≤ 50 với danh sách) — cần `Code` cho khoá tra giá của ĐVT gốc.
        // 📌 Dùng `GetListAsync` (không chiếu ẩn danh) vì tầng `Application` **không** tham chiếu EF Core; đánh đổi
        // là mỗi sản phẩm kéo theo `AutoInclude` (`Variants`/`Units`) — chấp nhận được ở trang danh sách, và
        // chính hai collection đó là thứ ta cần đọc ngay sau đây.
        var dsSanPham = await _sanPham.GetListAsync(x => ids.Contains(x.Id), cancellationToken: cancellationToken);

        if (dsSanPham.Count == 0)
        {
            return [];
        }

        var bienThe = await _bienThe.GetListAsync(x => ids.Contains(x.ProductId), cancellationToken: cancellationToken);
        var donVi = await _donVi.GetListAsync(x => ids.Contains(x.ProductId), cancellationToken: cancellationToken);

        var bienTheTheoSanPham = bienThe.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var donViTheoSanPham = donVi.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var ketQua = new List<LegacySellableLineDto>();

        foreach (var sp in dsSanPham)
        {
            var dong = ProductLegacyAdapter.Doc(
                sp.Id,
                sp.Code,
                bienTheTheoSanPham.TryGetValue(sp.Id, out var bt) ? bt : [],
                donViTheoSanPham.TryGetValue(sp.Id, out var dv) ? dv : []);

            foreach (var d in dong)
            {
                ketQua.Add(new LegacySellableLineDto
                {
                    ProductId = d.ProductId,
                    Sku = d.MaTraGia,
                    Barcode = d.Barcode,
                    Price = d.Gia,
                    Source = d.Nguon == NguonDongBan.BienThe ? "Variant" : "Unit"
                });
            }
        }

        return ketQua;
    }

    /// <summary>
    /// Tra sản phẩm theo **SKU biến thể** trước, rồi theo **mã sản phẩm** — xem hợp đồng ở interface.
    /// 🔒 Hai truy vấn **tuần tự, dừng ngay khi thấy** (không nạp gì thêm): đường công khai chạy mỗi lần khách
    /// mở một URL chi tiết.
    /// </summary>
    public async Task<Guid?> FindProductIdByCodeOrSkuAsync(
        string ma,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ma))
        {
            return null;
        }

        var khoa = ma.Trim();

        var bienThe = await _bienThe.FirstOrDefaultAsync(x => x.Sku == khoa, cancellationToken: cancellationToken);

        if (bienThe is not null)
        {
            return bienThe.ProductId;
        }

        var sanPham = await _sanPham.FirstOrDefaultAsync(x => x.Code == khoa, cancellationToken: cancellationToken);

        return sanPham?.Id;
    }
}
