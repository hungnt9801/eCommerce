using eCommerce.Catalog;
using eCommerce.Pricing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace eCommerce.Web.Storefront;

/// <summary>
/// 🚨 **ĐƯỜNG ĐỌC CÔNG KHAI CỦA STOREFRONT** (`WEB-002` · `S8`) — chỉ đọc **repository**, **KHÔNG** gọi
/// app service có `[Authorize]`.
///
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
/// **VÌ SAO PHẢI CÓ LỚP NÀY (lỗi đã ĐO ĐƯỢC, không phải phòng xa):**
/// `/sitemap.xml` từng gọi `IPriceListAppService` — app service đó mang `[Authorize(Pricing.Default)]`
/// ⇒ khách **chưa đăng nhập** nhận `AbpAuthorizationException` ⇒ endpoint *"công khai"* trả **`HTTP 500`**
/// (ghi ở `SitemapController`, bài học `CAT-003`). 📌 Mở `[AllowAnonymous]` cho `/SanPham` mà vẫn gọi app
/// service có quyền thì **lặp lại đúng lỗi đó**: trang công khai **không bao giờ** phục vụ được khách.
///
/// 🔑 **Nguyên tắc:** *"mở cửa công khai"* và *"gọi vào chỗ cần quyền"* là **hai điều mâu thuẫn** —
/// cửa công khai phải tự đọc dữ liệu **công khai của chính nó** (đúng khuôn `CatMediaController`,
/// cũng `[AllowAnonymous]` và cũng đọc repository trực tiếp).
/// ══════════════════════════════════════════════════════════════════════════════════════════════════════
///
/// 🔒 **BA RÀNG BUỘC CỦA QUYẾT ĐỊNH `D10` (17/10/2026) — thi hành ngay ở tầng đọc, KHÔNG ở tầng view:**
/// <list type="number">
///   <item>**CHỈ hàng ĐANG BÁN** — lọc `Status == ProductStatus.Published` **trong truy vấn** (không lọc ở view:
///     view mà quên một nhánh là dữ liệu **đã ra khỏi DB** rồi, không cứu được).</item>
///   <item>**CẤM `CostPrice`** — DTO ở đây **không có** trường giá vốn, và lớp này **không** đọc cột đó.
///     📌 `ProductVariant.Price` là **giá niêm yết** (công khai), `CostPrice` **không** tồn tại trong DTO
///     ⇒ "quên ẩn" là **không thể**, vì **không có gì** để ẩn.</item>
///   <item>**CẤM tồn kho chi tiết** — lớp này **không** đọc `InvItems`/`InvStockLedger` ⇒ con số tồn
///     **không bao giờ** vào DTO. 📌 Mức *"còn/hết"* là việc của tầng hiển thị **khi nào có** đường đọc tồn
///     công khai (`INV-003`); hôm nay **không** hiển thị tồn — im lặng đúng hơn nói sai.</item>
/// </list>
///
/// ⚠️ **ĐIỀU LỚP NÀY CỐ Ý *KHÔNG* LÀM (ghi rõ để phiên sau không tưởng đã có):**
/// <list type="bullet">
///   <item>**Không** có read-model `WebSeoProducts` + ETO như `WEB-002` §5.2.2 thiết kế — dựng projection
///     là việc của GĐ2 khi Catalog tách module; đọc thẳng ở đây **chấp nhận** trả giá bằng NFR-001
///     (mỗi lần xem là một truy vấn). 📌 Ghi ở `web-002` §16.</item>
///   <item>**Không** cache Redis, **không** `ETag`/`304`, **không** 301 khi đổi slug — ba nợ đã ghi ở
///     `web-002` §16, không phải "đã xong".</item>
/// </list>
/// </summary>
public class DocSanPhamCongKhai : ITransientDependency
{
    private readonly IRepository<Product, Guid> _sanPham;
    private readonly IRepository<ProductVariant, Guid> _bienThe;
    private readonly IRepository<PriceList, Guid> _bangGia;
    private readonly IRepository<PriceListItem, Guid> _dongGia;

    /// <summary>
    /// 🔑 **MỘT NGUỒN SỰ THẬT cho câu hỏi *"hàng đang bán"*** — `Status == Published`.
    ///
    /// 📌 **VÌ SAO LÀ HẰNG BIỂU THỨC DÙNG CHUNG, KHÔNG VIẾT LẠI Ở TỪNG TRUY VẤN:** đã **đo được** ngay trong
    /// `S8` — bản đầu viết điều kiện này **ba lần** ở ba truy vấn; phép tiêm *"bỏ bộ lọc"* vào **một** chỗ
    /// **không** làm test đỏ, vì hai chỗ còn lại vẫn chứa chuỗi mà test tìm. 📌 Đó là *"phép đo sai, không phải
    /// sản phẩm sai"* ở dạng mới: test đang kiểm **sự tồn tại của chữ**, trong khi câu hỏi thật là
    /// *"mọi truy vấn đều lọc"*. ✅ Gom về **một** chỗ ⇒ test chỉ cần khẳng định **đếm = 1**, và mọi truy vấn
    /// dùng chung nó.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<Product, bool>> ChiHangDangBan =
        x => x.Status == ProductStatus.Published;

    public DocSanPhamCongKhai(
        IRepository<Product, Guid> sanPham,
        IRepository<ProductVariant, Guid> bienThe,
        IRepository<PriceList, Guid> bangGia,
        IRepository<PriceListItem, Guid> dongGia)
    {
        _sanPham = sanPham;
        _bienThe = bienThe;
        _bangGia = bangGia;
        _dongGia = dongGia;
    }

    /// <summary>
    /// Mọi sản phẩm **ĐANG BÁN** có **ít nhất một** dòng giá đang hiệu lực — dùng cho `/SanPham` và
    /// `/sitemap.xml`.
    /// </summary>
    /// <remarks>
    /// 🚨 **VÌ SAO LỌC "CÓ GIÁ" Ở ĐÂY CHỨ KHÔNG Ở VIEW:** một sản phẩm `Published` nhưng **chưa niêm yết giá**
    /// mà vẫn hiện là **link chết** — khách bấm vào nhận `404`, máy tìm kiếm học *"trang này không tồn tại"*
    /// ⇒ 📌 đúng lỗi `WEB-002` §12 cảnh báo (*"danh mục rỗng ⇒ 200 + thông báo, **không** phải trang lỗi"*).
    /// Sản phẩm **có** giá nhưng **không** muốn bán thì phải là `Unpublished` — đó là **ý nghĩa** của trạng thái.
    /// </remarks>
    public async Task<IReadOnlyList<SanPhamCongKhaiDto>> GetDangBanAsync(CancellationToken cancellationToken = default)
    {
        // 🔒 BƯỚC ①: chỉ sản phẩm ĐANG BÁN (`D10` ràng buộc ①).
        var sanPham = await _sanPham.GetListAsync(ChiHangDangBan, cancellationToken: cancellationToken);

        if (sanPham.Count == 0)
        {
            return [];
        }

        var tapId = sanPham.Select(x => x.Id).ToList();
        var bienThe = await _bienThe.GetListAsync(
            x => tapId.Contains(x.ProductId) && x.IsActive, cancellationToken: cancellationToken);

        var ketQua = await GhepGiaAsync(sanPham, bienThe, cancellationToken);

        // 📌 Sản phẩm chưa niêm yết giá bị **loại khỏi danh sách** (xem `<remarks>`), không trả về dòng rỗng.
        return ketQua.Where(x => x.Gia.Count > 0).OrderBy(x => x.Ten, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Tra **một** sản phẩm đang bán theo **slug** (URL chuẩn) hoặc **SKU** (URL cũ) — `null` nếu không có.
    /// </summary>
    /// <remarks>
    /// 🔒 **CHUẨN HOÁ SLUG bằng CHÍNH hàm của đường ghi** (`SinhSlug.Tu`): nếu tầng đọc tự chuẩn hoá một kiểu
    /// thì `/SanPham/ChiTiet/Áo-Thun-Nam` tìm thấy còn đường ghi lưu một kiểu khác ⇒ **hai nguồn sự thật**
    /// (đúng họ lỗi `CAT-003` ADR-04 đã trả giá). ⚠️ Tra **sản phẩm trước, giá sau** — tra giá trước thì một
    /// SKU "trông hợp lệ" vẫn cho `200` dù không thuộc sản phẩm nào (lỗi đã ghi ở controller cũ).
    /// 🚨 **SẢN PHẨM CHƯA BÁN ⇒ `null` NGAY, KHÔNG rơi xuống nhánh SKU:** nếu rơi xuống, một slug của hàng
    /// `Unpublished` mà **trùng chữ** với SKU của sản phẩm khác sẽ hiện **sai sản phẩm** — lỗi im lặng.
    /// </remarks>
    public async Task<SanPhamCongKhaiDto?> GetTheoSlugHoacSkuAsync(
        string slugHoacSku,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slugHoacSku))
        {
            return null;
        }

        var dauVao = slugHoacSku.Trim();

        Product? sanPham = null;

        var slugChuan = SinhSlug.Tu(dauVao);

        if (slugChuan.Length > 0)
        {
            sanPham = await _sanPham.FirstOrDefaultAsync(x => x.Slug == slugChuan, cancellationToken: cancellationToken);

            if (sanPham is not null && !sanPham.IsPublished)
            {
                // 🔑 Hàng **ngừng bán**: trả `null` (⇒ `404`) — KHÔNG nói *"có nhưng bị ẩn"* (khách không cần biết)
                // và KHÔNG rơi xuống nhánh SKU (xem `<remarks>`).
                return null;
            }
        }

        if (sanPham is null)
        {
            // 🔑 URL cũ theo **SKU**: vẫn trả `200` nhưng `canonical` (do controller dựng) trỏ về slug —
            // bỏ đột ngột thì mọi link đã chia sẻ/index thành `404` và mất tín hiệu SEO đã tích luỹ.
            var bienThe = await _bienThe.FirstOrDefaultAsync(x => x.Sku == dauVao, cancellationToken: cancellationToken);

            if (bienThe is not null)
            {
                sanPham = await _sanPham.FirstOrDefaultAsync(x => x.Id == bienThe.ProductId, cancellationToken: cancellationToken);

                if (sanPham is not null && !sanPham.IsPublished)
                {
                    return null;
                }
            }
        }

        if (sanPham is null)
        {
            return null;
        }

        var tatCaBienThe = await _bienThe.GetListAsync(
            x => x.ProductId == sanPham.Id && x.IsActive, cancellationToken: cancellationToken);

        var dong = await GhepGiaAsync([sanPham], tatCaBienThe, cancellationToken);

        return dong.Count == 0 ? null : dong[0];
    }

    /// <summary>
    /// Ghép **sản phẩm × biến thể × dòng giá** của các bảng giá **đang hiệu lực**.
    /// 🔒 Chỉ đọc `PriceList`/`PriceListItem` — **không** đọc `ChannelPrice` (giá theo kênh là của pipeline
    /// `PRC-002`; `WEB-002` BR-008 cho phép hiển thị **giá niêm yết kèm nhãn nguồn**, và trộn hai nguồn
    /// trong một trang là **điều BR-008 cấm**).
    /// </summary>
    private async Task<List<SanPhamCongKhaiDto>> GhepGiaAsync(
        IReadOnlyCollection<Product> sanPham,
        IReadOnlyCollection<ProductVariant> bienThe,
        CancellationToken cancellationToken)
    {
        var bayGio = DateTime.UtcNow;

        // 🔒 Bảng giá "đang hiệu lực" = `Active` **VÀ** trong khoảng `EffectiveFrom..EffectiveTo`.
        // 📌 Chỉ dựa vào `Status` là **thiếu**: một bảng `Active` đã quá `EffectiveTo` vẫn còn `Active` cho tới
        // khi job chuyển trạng thái chạy ⇒ hiển thị **giá hết hạn** cho khách. Nhưng cũng **không** chỉ dựa
        // vào ngày: bảng `Draft` có ngày đúng vẫn **chưa được duyệt**. ⇒ **cả hai** điều kiện.
        var bangGia = await _bangGia.GetListAsync(
            x => x.Status == PriceListStatus.Active
                 && x.EffectiveFrom <= bayGio
                 && (x.EffectiveTo == null || x.EffectiveTo >= bayGio),
            cancellationToken: cancellationToken);

        if (bangGia.Count == 0)
        {
            return [];
        }

        var idBangGia = bangGia.Select(x => x.Id).ToList();
        var cacSku = bienThe.Select(x => x.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dongGia = await _dongGia.GetListAsync(
            x => idBangGia.Contains(x.PriceListId) && cacSku.Contains(x.Sku), cancellationToken: cancellationToken);

        if (dongGia.Count == 0)
        {
            return [];
        }

        // Thứ tự bảng giá: `Priority` GIẢM dần rồi tới `Code` — **tất định** (cùng `Priority` thì không phụ
        // thuộc thứ tự DB trả về; đây cùng nguyên tắc với `GiaCoSoResolver`: cấm "cái nào gặp trước thắng").
        var thuTuBangGia = bangGia
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        var ketQua = new List<SanPhamCongKhaiDto>(sanPham.Count);

        foreach (var sp in sanPham)
        {
            var bienTheCuaSp = bienThe.Where(x => x.ProductId == sp.Id).ToList();

            if (bienTheCuaSp.Count == 0)
            {
                continue;
            }

            var skuCuaSp = bienTheCuaSp.Select(x => x.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var gia = new List<DongGiaCongKhaiDto>();

            foreach (var bg in thuTuBangGia)
            {
                foreach (var d in dongGia
                             .Where(x => x.PriceListId == bg.Id && skuCuaSp.Contains(x.Sku))
                             .OrderBy(x => x.Sku, StringComparer.Ordinal))
                {
                    // 🔒 Chống trùng khi HAI bảng giá cùng hiệu lực cùng SKU: giữ dòng của bảng ưu tiên cao hơn
                    // và ghi rõ nguồn — nếu không, khách thấy **hai giá** cho cùng một SKU và không biết giá nào.
                    if (gia.Any(x => string.Equals(x.Sku, d.Sku, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    gia.Add(new DongGiaCongKhaiDto
                    {
                        Sku = d.Sku,
                        MaBangGia = bg.Code,
                        TenBangGia = bg.Name,
                        DonGia = d.AmountValue,
                        TienTe = d.CurrencyCode
                    });
                }
            }

            if (gia.Count == 0)
            {
                continue;
            }

            ketQua.Add(new SanPhamCongKhaiDto
            {
                Ten = sp.Name,
                // 🔒 KHÔNG trả `sp.Code` (mã nội bộ) và KHÔNG trả `sp.Id` (GUID): URL công khai dùng **slug**
                // (`WEB-002` ADR-03). Slug `null` vẫn được controller dựng từ tên — cùng hàm, cùng kết quả.
                Slug = sp.Slug,
                MoTa = sp.Description,
                Gia = gia.OrderBy(x => x.DonGia).ToList()
            });
        }

        return ketQua;
    }
}

/// <summary>
/// **DỮ LIỆU CÔNG KHAI** của một sản phẩm — 🔒 danh sách trường ở đây **là** ranh giới chống rò rỉ.
///
/// 🚨 **CẤM thêm vào đây:** `CostPrice` (giá vốn), `OnHand`/`Reserved` (tồn chi tiết), `Code` nội bộ,
/// `Id` (GUID), `BrandId`, `CreatorId`… 📌 Mỗi trường thêm vào là một trường **hiện ra Internet** — và
/// không có bước review nào khác chặn việc đó.
/// ✅ Có `Slug` vì đó là **URL công khai** (`ADR-03`), và `Sku` vì URL cũ + sitemap dùng nó.
/// </summary>
public class SanPhamCongKhaiDto
{
    public string Ten { get; set; } = default!;

    public string? Slug { get; set; }

    /// <summary>Mô tả do admin nhập — ⚠️ **KHÔNG** render thô (`WEB-002` ADR-12: cột `text` tự do ⇒ stored XSS).
    /// Tầng view **bắt buộc** escape (Razor `@`), đó là lý do DTO giữ nguyên văn và view không dùng `Html.Raw`.</summary>
    public string? MoTa { get; set; }

    public List<DongGiaCongKhaiDto> Gia { get; set; } = new();
}

/// <summary>Một dòng giá **công khai**: giá niêm yết của một SKU trong một bảng giá đang hiệu lực.</summary>
public class DongGiaCongKhaiDto
{
    public string Sku { get; set; } = default!;

    public string MaBangGia { get; set; } = default!;

    public string TenBangGia { get; set; } = default!;

    public decimal DonGia { get; set; }

    public string TienTe { get; set; } = "VND";
}
