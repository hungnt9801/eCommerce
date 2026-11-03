namespace eCommerce.Catalog;

/// <summary>Nguồn của một dòng bán được khi đi qua <see cref="ProductLegacyAdapter"/>.</summary>
public enum NguonDongBan
{
    /// <summary>Dòng đọc từ **biến thể cũ** (`CatProductVariants`) — mô hình trước D-07.</summary>
    BienThe = 1,

    /// <summary>Dòng đọc từ **đơn vị tính gốc** (`CatProductUnits`) — mô hình Nhanh (D-07).</summary>
    DonVi = 2
}

/// <summary>
/// Một dòng **bán được** ở dạng mà các đường đọc CŨ hiểu được: *mã tra giá (SKU), barcode, giá*.
/// 📌 Đây **không phải** DTO công khai và **không** mang giá vốn/tồn kho — nó chỉ là hình dạng chung.
/// </summary>
/// <param name="ProductId">Sản phẩm chứa dòng này.</param>
/// <param name="MaTraGia">SKU (biến thể) hoặc mã sản phẩm (ĐVT gốc) — khoá để tra bảng giá cũ.</param>
/// <param name="Barcode">Mã vạch của dòng (nếu có).</param>
/// <param name="Gia">Giá niêm yết của dòng.</param>
/// <param name="Nguon">Dòng đến từ mô hình nào — quyết định *"giá này có chủ"* ở tầng hiển thị.</param>
public sealed record DongBanDuoc(
    Guid ProductId,
    string MaTraGia,
    string? Barcode,
    decimal Gia,
    NguonDongBan Nguon);

/// <summary>
/// **ADAPTER ĐỌC CŨ — "map ngược" mô hình Nhanh về hình dạng cũ** (WP-03 mục 7, D-07).
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VẤN ĐỀ:** trước WP-03, mọi đường đọc bán hàng hỏi *"sản phẩm này có dòng nào bán được"* bằng cách đọc
/// `Product.Variants`. Sau D-07, nơi giữ **giá** là `Product.Units` — nghĩa là một sản phẩm tạo theo mô hình
/// mới (**0 biến thể, có ĐVT**) sẽ **vô hình** với mọi đường đọc cũ: storefront không hiện, POS không quét ra.
/// Sửa từng chỗ đọc (view/controller) là hàng chục chỗ và mỗi chỗ có thể quên.
///
/// **CÁCH LÀM:** một hàm **thuần** duy nhất trả về danh sách dòng bán được theo hình dạng cũ, chọn nguồn theo
/// thứ tự **biến thể trước, ĐVT sau**. Nhờ vậy:
/// <list type="number">
///   <item>Dữ liệu cũ (chỉ có biến thể) cho ra **đúng như trước** — không đổi hành vi storefront/POS;</item>
///   <item>Sản phẩm mới (chỉ có ĐVT) **không còn vô hình** — nó có một dòng gốc với mã tra giá = mã sản phẩm;</item>
///   <item>Chỗ đọc **không phải biết** hai mô hình cùng tồn tại.</item>
/// </list>
///
/// ⚠️ **GIỚI HẠN ĐÃ BIẾT (ghi rõ, không giấu):**
/// <list type="bullet">
///   <item>Chỉ **ĐVT gốc** được đưa ra đường đọc cũ. Các ĐVT khác (thùng/kiện) **chưa có khoá tương thích**
///     (bảng giá cũ tra theo `Sku`, mà ĐVT không có SKU) ⇒ tự chế khoá `Code + …` sẽ tạo **mã giá không tồn tại**
///     trong bảng giá. Việc bán theo ĐVT thuộc POS (WP-06) — đọc **thẳng** `Product.Units`.</item>
///   <item>Một sản phẩm **vừa có biến thể cũ vừa có ĐVT** (trạng thái chuyển tiếp sau backfill) ⇒ **biến thể
///     thắng**. Đây là lựa chọn có chủ ý: giữ hành vi cũ cho tới khi đơn vị dữ liệu mới được xác nhận.</item>
///   <item>**Không** đọc giá từ bảng giá — hàm này chỉ trả **giá niêm yết của dòng**; ghép bảng giá vẫn là việc của
///     nơi gọi (storefront tra `PriceListItem` theo `MaTraGia`).</item>
/// </list>
/// </summary>
public static class ProductLegacyAdapter
{
    /// <summary>
    /// Các dòng bán được của **một** sản phẩm, theo hình dạng cũ. Trả mảng rỗng khi sản phẩm không có nguồn nào.
    /// 🔒 Chỉ dòng **đang bán** được trả về: biến thể `IsActive = false` (đã ngừng bán) **không** vào danh sách.
    /// </summary>
    /// <param name="productId">Id sản phẩm (chỉ để gắn vào từng dòng — không truy vấn gì).</param>
    /// <param name="productCode">Mã sản phẩm — dùng làm khoá tra giá cho dòng ĐVT gốc.</param>
    /// <param name="bienThe">Biến thể của sản phẩm (mô hình cũ).</param>
    /// <param name="donVi">Đơn vị tính của sản phẩm (mô hình Nhanh).</param>
    public static IReadOnlyList<DongBanDuoc> Doc(
        Guid productId,
        string productCode,
        IEnumerable<ProductVariant> bienThe,
        IEnumerable<ProductUnit> donVi)
    {
        var dangBan = bienThe.Where(x => x.IsActive).OrderBy(x => x.Sku, StringComparer.Ordinal).ToList();

        if (dangBan.Count > 0)
        {
            return dangBan
                .Select(x => new DongBanDuoc(productId, x.Sku, x.Barcode, x.Price, NguonDongBan.BienThe))
                .ToList();
        }

        var goc = donVi.FirstOrDefault(x => x.IsBase);

        if (goc is null)
        {
            return [];
        }

        return [new DongBanDuoc(productId, productCode, goc.Barcode, goc.SalePrice, NguonDongBan.DonVi)];
    }
}
