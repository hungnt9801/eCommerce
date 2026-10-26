using eCommerce.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;

namespace eCommerce.Catalog;

/// <summary>
/// App service của **Catalog** (`CAT-001` §6.1 — **F11**).
///
/// 🔐 `[Authorize]` ở **cấp CLASS**: mọi method mặc định yêu cầu đăng nhập + quyền `Products`;
/// method nào cần quyền riêng thì ghi thêm (`Products.Create/Edit/Delete/Publish`).
///
/// 🚨 **BA ĐIỀU DỄ SAI NHẤT Ở ĐÂY — đã xử lý có chủ ý:**
///
/// 1. **`Sku`/`Code` KHÔNG sửa được qua đường cập nhật thường** (`CAT-001` **D2**): chúng là khoá đối chiếu
///    đã ra ngoài (`ChannelListings.InternalSku`, `OrdOrderLines.Sku`, `InvItems.Sku`). Sửa ⇒
///    **lịch sử bán và lịch sử kho trỏ sai**, và **không có lỗi nào**. DTO cập nhật **không có** trường này.
///
/// 2. **Chống trùng SKU phải kiểm tra theo TOÀN TENANT, không phải trong một sản phẩm**
///    (`CAT-001` **D3/F6**): `Product.AddVariant()` chỉ so trong bộ nhớ của **một** sản phẩm, còn ràng buộc
///    thật là `UNIQUE (TenantId, Sku)`. Kiểm tra trong bộ nhớ **yếu hơn DB** ⇒ va chạm thật sẽ nổi ở tầng DB
///    với thông điệp khó hiểu. Vì vậy: **khoá phân tán theo SKU** (`catalog:sku:{sku}`) + kiểm tra qua
///    repository ⇒ tuần tự hoá và cho **mã lỗi nghiệp vụ** thay vì lỗi DB.
///
/// 3. **Xoá là XOÁ MỀM** (`CAT-001` **D4**): xoá cứng `Product` sẽ làm treo tham chiếu từ lịch sử bán,
///    listing sàn, tồn kho và phân loại thuế. FK đã được đổi sang `Restrict` để **DB chặn** xoá cứng.
/// </summary>
[Authorize(eCommerceConsts.Permissions.Products.Default)]
public class ProductAppService : ApplicationService, IProductAppService
{
    /// <summary>Thời gian chờ giành khoá phân tán theo SKU (giây) — khớp mặc định `ChannelHubOptions.LockTimeoutSeconds`.</summary>
    private const int ChoKhoaToiDaGiay = 10;

    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<ProductVariant, Guid> _variantRepository;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedEventBus _eventBus;

    public ProductAppService(
        IRepository<Product, Guid> productRepository,
        IRepository<ProductVariant, Guid> variantRepository,
        IAbpDistributedLock distributedLock,
        IDistributedEventBus eventBus)
    {
        _productRepository = productRepository;
        _variantRepository = variantRepository;
        _distributedLock = distributedLock;
        _eventBus = eventBus;
    }

    // ── Đọc ───────────────────────────────────────────────────────────────────

    public async Task<PagedResultDto<ProductDto>> GetListAsync(GetProductsInput input)
    {
        var query = await _productRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var tuKhoa = input.Keyword.Trim();

            query = query.Where(x =>
                x.Code.Contains(tuKhoa) ||
                x.Name.Contains(tuKhoa) ||
                (x.Slug != null && x.Slug.Contains(tuKhoa)));
        }

        if (input.Status is not null)
        {
            query = query.Where(x => x.Status == input.Status);
        }

        var tong = await AsyncExecuter.CountAsync(query);

        var danhSach = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Code)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        // 🚨 Nạp biến thể theo **một truy vấn riêng** cho cả trang (không N+1 theo từng sản phẩm):
        // lấy toàn bộ biến thể của các sản phẩm trong trang rồi gom theo `ProductId`.
        var ids = danhSach.Select(x => x.Id).ToList();
        var bienThe = await _variantRepository.GetListAsync(x => ids.Contains(x.ProductId));
        var theoSanPham = bienThe.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var items = danhSach
            .Select(sp => ToDto(sp, theoSanPham.TryGetValue(sp.Id, out var ds) ? ds : []))
            .ToList();

        return new PagedResultDto<ProductDto>(tong, items);
    }

    public async Task<ProductDto> GetAsync(Guid id)
    {
        var sanPham = await _productRepository.GetAsync(id);
        var bienThe = await _variantRepository.GetListAsync(x => x.ProductId == id);
        return ToDto(sanPham, bienThe);
    }

    public async Task<ProductDto> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.ThieuThongTinBatBuoc)
                .WithData("truong", nameof(code));
        }

        var ma = code.Trim();

        var sanPham = await _productRepository.FirstOrDefaultAsync(x => x.Code == ma)
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThaySanPham)
                .WithData("code", ma);

        var bienThe = await _variantRepository.GetListAsync(x => x.ProductId == sanPham.Id);
        return ToDto(sanPham, bienThe);
    }

    // ── Ghi ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🚨 **TRA THEO SLUG** (`CAT-003`) — đường đọc cho **storefront**. Xem hợp đồng ở
    /// <see cref="IProductAppService.GetBySlugAsync"/>.
    ///
    /// 🔒 **Chuẩn hoá slug TRƯỚC khi tra** bằng **cùng** hàm `SinhSlug.Tu` dùng lúc ghi ⇒ người dùng gõ
    /// `/SanPham/ChiTiet/Áo-Thun-Nam` vẫn tìm thấy `ao-thun-nam` (URL không phân biệt dấu/hoa-thường ở tầng vào).
    /// 📌 Không có bước này thì **chỉ** URL đúng từng ký tự mới chạy ⇒ khách dán link từ nơi khác (bị mã hoá,
    /// hoặc viết hoa) nhận `404` — **không có lỗi nào**, chỉ là mất khách.
    /// ⚠️ `null` khi không tìm thấy (**không** ném): *"slug không tồn tại"* là ca **bình thường**.
    /// </summary>
    public async Task<ProductDto?> GetBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var chuanHoa = SinhSlug.Tu(slug);

        if (chuanHoa.Length == 0)
        {
            return null;
        }

        var sanPham = await _productRepository.FirstOrDefaultAsync(x => x.Slug == chuanHoa);

        if (sanPham is null)
        {
            return null;
        }

        var bienThe = await _variantRepository.GetListAsync(x => x.ProductId == sanPham.Id);

        return ToDto(sanPham, bienThe);
    }

    /// <summary>
    /// Tra sản phẩm theo **SKU biến thể** (`CAT-003`) — đường **TƯƠNG THÍCH NGƯỢC** cho URL cũ
    /// (xem hợp đồng ở <see cref="IProductAppService.GetByVariantSkuAsync"/>).
    /// 🔒 `null` khi không tìm thấy (**không** ném) — *"SKU không tồn tại"* là ca **bình thường**.
    /// </summary>
    public async Task<ProductDto?> GetByVariantSkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        var maSku = sku.Trim();

        var bienThe = await _variantRepository.FirstOrDefaultAsync(x => x.Sku == maSku);

        if (bienThe is null)
        {
            return null;
        }

        var sanPham = await _productRepository.FindAsync(bienThe.ProductId);

        if (sanPham is null)
        {
            // ⚠️ Biến thể mồ côi (sản phẩm bị xoá cứng?) — trả `null` thay vì ném: storefront chỉ cần `404`.
            // 📌 Đây là ca đã có guard riêng của `CAT-002` (`BR-012`/`0023`); ở đây **không** nhân bản guard.
            return null;
        }

        var tatCaBienThe = await _variantRepository.GetListAsync(x => x.ProductId == sanPham.Id);

        return ToDto(sanPham, tatCaBienThe);
    }

    [Authorize(eCommerceConsts.Permissions.Products.Create)]
    public async Task<ProductDto> CreateAsync(CreateProductDto input)
    {
        var ma = Product.ChuanHoaMa(input.Code);

        // Kiểm tra trước để trả **mã lỗi nghiệp vụ** thay vì lỗi ràng buộc DB khó hiểu.
        // ⚠️ Unique index `(TenantId, Code)` (`NULLS NOT DISTINCT`) vẫn là **hàng rào cuối** —
        // hai request đồng thời cùng mã vẫn có thể lọt qua bước kiểm tra này (xem ghi chú ở 16.3 của tài liệu).
        if (await _productRepository.AnyAsync(x => x.Code == ma))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.MaSanPhamDaTonTai).WithData("code", ma);
        }

        var sanPham = new Product(GuidGenerator.Create(), ma, input.Name)
        {
            Description = input.Description,
            BrandId = input.BrandId
        };

        // 🔑 `CAT-003` ADR-04: slug **chuẩn hoá qua CÙNG một hàm** với đường tự sinh (`SinhSlug.Tu`) —
        // lần đầu code này tự `Trim().ToLowerInvariant()` (KHÔNG bỏ dấu) ⇒ hai đường cho ra hai dạng slug
        // ⇒ URL của khách có dấu tiếng Việt và `canonical` khác nhau tuỳ đường tạo.
        // 📌 Không phát sự kiện ở đây: sản phẩm **vừa được tạo** ⇒ **chưa có** URL công khai nào để chuyển hướng.
        if (!string.IsNullOrWhiteSpace(input.Slug))
        {
            var slugMoi = SinhSlug.Tu(input.Slug);

            if (slugMoi.Length == 0)
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongSinhDuocSlug)
                    .WithData("slugNguoiNhap", input.Slug)
                    .WithData("lyDo", "Slug tự nhập không còn ký tự dùng được sau khi chuẩn hoá.");
            }

            // Kiểm trùng ngay khi tạo: DB vẫn là hàng rào cuối (`IX_CatProducts_TenantId_Slug` ⇒ `0026`).
            if (await CoSlugKhacDungAsync(slugMoi, sanPham.Id))
            {
                throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SlugDaTonTai)
                    .WithData("slug", slugMoi)
                    .WithData("lyDo", "Slug này đã được sản phẩm KHÁC dùng ⇒ URL công khai sẽ trùng.");
            }

            sanPham.DatSlug(slugMoi);
        }

        await _productRepository.InsertAsync(sanPham, autoSave: true);

        Logger.LogInformation("Đã tạo sản phẩm {Code} (trạng thái {Status}).", sanPham.Code, sanPham.Status);

        return ToDto(sanPham, []);
    }

    [Authorize(eCommerceConsts.Permissions.Products.Edit)]
    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input)
    {
        var sanPham = await _productRepository.GetAsync(id);

        // 🚨 KHÔNG chạm vào `Code` (D2) — `UpdateProductDto` cố ý không có trường đó.
        sanPham.Name = input.Name;
        sanPham.Description = input.Description;
        sanPham.BrandId = input.BrandId;

        // 🚨 `CAT-003` ADR-05: **ĐỔI `Name` KHÔNG tự đổi slug** (làm vậy sẽ phá URL đã được index ⇒ mất SEO
        // âm thầm). Nhưng nếu DTO **có** gửi slug thì đó là ý định **TƯỜNG MINH** của người dùng ⇒ áp dụng,
        // và **BẮT BUỘC** đi qua cùng đường với `DatSlugAsync` (khoá phân tán + kiểm trùng + **PHÁT SỰ KIỆN**).
        // 📌 Nếu đường này gán thẳng `sanPham.Slug = …` thì `ProductSlugChangedEto` **không** được phát
        // ⇒ `WEB-002` không dựng được 301 ⇒ **link cũ 404** mà **không có lỗi nào** — đúng họ lỗi im lặng.
        if (!string.IsNullOrWhiteSpace(input.Slug))
        {
            var slugCu = await ApDungSlugAsync(sanPham, input.Slug);

            if (slugCu is not null)
            {
                Logger.LogInformation("ĐÃ ĐỔI SLUG (qua `UpdateAsync`) sản phẩm {Code}: {SlugCu} → {SlugMoi}. [SLUG-DA-DOI]",
                    sanPham.Code, slugCu, sanPham.Slug);
            }
        }

        await _productRepository.UpdateAsync(sanPham, autoSave: true);

        var bienThe = await _variantRepository.GetListAsync(x => x.ProductId == id);
        return ToDto(sanPham, bienThe);
    }

    [Authorize(eCommerceConsts.Permissions.Products.Edit)]
    public async Task<ProductVariantDto> AddVariantAsync(Guid productId, CreateVariantDto input)
    {
        var sku = Product.ChuanHoaSku(input.Sku);

        // 🔒 Khoá theo SKU: tuần tự hoá "kiểm tra rồi ghi" cho **toàn tenant**.
        await using var handle = await _distributedLock.TryAcquireAsync(
            eCommerceConsts.LockKeys.CatalogSku(sku), TimeSpan.FromSeconds(ChoKhoaToiDaGiay));

        if (handle is null)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("sku", sku)
                .WithData("choGiay", ChoKhoaToiDaGiay);
        }

        // ✅ Kiểm tra TOÀN TENANT (mạnh hơn kiểm tra trong một sản phẩm của domain — D3/F6).
        if (await _variantRepository.AnyAsync(x => x.Sku == sku))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SkuDaTonTai).WithData("sku", sku);
        }

        var sanPham = await _productRepository.GetAsync(productId);

        var bienThe = sanPham.AddVariant(GuidGenerator.Create(), sku, input.Price, input.Barcode);
        bienThe.CompareAtPrice = input.CompareAtPrice;
        bienThe.WeightInGrams = input.WeightInGrams;

        await _productRepository.UpdateAsync(sanPham, autoSave: true);

        Logger.LogInformation("Đã thêm SKU {Sku} vào sản phẩm {Code}.", sku, sanPham.Code);

        return ToVariantDto(bienThe);
    }

    [Authorize(eCommerceConsts.Permissions.Products.Edit)]
    public async Task<bool> UpdateVariantPriceAsync(Guid productId, Guid variantId, UpdateVariantPriceDto input)
    {
        var bienThe = await _variantRepository.FirstOrDefaultAsync(x => x.Id == variantId && x.ProductId == productId)
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongTimThaySanPham)
                .WithData("variantId", variantId);

        // Giữ giá CŨ trước khi đổi — cần cho sự kiện (`OldPrice`) để bên nhận/đối soát biết
        // "giá nào đã áp dụng lúc nào". `DoiGia` chỉ trả về có đổi hay không, không trả giá cũ.
        var giaCu = bienThe.Price;

        var coDoi = bienThe.DoiGia(input.Price);

        if (coDoi)
        {
            await _variantRepository.UpdateAsync(bienThe, autoSave: true);
            Logger.LogInformation("Đã đổi giá SKU {Sku} thành {Gia}.", bienThe.Sku, bienThe.Price);

            // 📣 Sự kiện THÔNG BÁO (xem `VariantPriceChangedEto`) — CHỈ phát khi giá **thực sự** đổi
            // (`DoiGia` trả `true`) ⇒ không tạo sự kiện rác làm bên nhận đẩy giá lên sàn vô ích (tiêu quota).
            // ⚠️ Giá mới **đã được ghi** ở dòng trên ⇒ mất sự kiện này **không** mất dữ liệu.
            await _eventBus.PublishAsync(new Events.VariantPriceChangedEto
            {
                TenantId = bienThe.TenantId,
                ProductId = productId,
                VariantId = bienThe.Id,
                Sku = bienThe.Sku,
                OldPrice = giaCu,
                NewPrice = bienThe.Price,
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        return coDoi;
    }

    // ── Vòng đời (D5) ─────────────────────────────────────────────────────────

    /// <summary>
    /// 🚨 **SINH / ĐỔI SLUG SEO** (`CAT-003` **ADR-04/ADR-05**) — xem hợp đồng ở
    /// <see cref="IProductAppService.DatSlugAsync"/>.
    ///
    /// 🔒 **KHOÁ PHÂN TÁN theo slug** trước khi kiểm trùng: nếu chỉ kiểm bằng `SELECT` thì **hai request đồng
    /// thời** cùng thấy *"chưa trùng"* ⇒ cả hai ghi ⇒ hàng rào DB nổ (`0026`) và một người dùng nhận lỗi
    /// **đáng lẽ không cần**. 📌 Khoá này **không** thay thế unique index — nó chỉ làm ca **thường**
    /// (hai người cùng lúc) trả **kết quả đúng**; ca **đồng thời thật** vẫn được DB chặn (hai lớp, như `INV-002`).
    /// ⚠️ Tên khoá **KHÔNG** có tiền tố `ecommerce:` (`XC-003` §5.4 — `KeyPrefix` đã thêm).
    /// </summary>
    [Authorize(eCommerceConsts.Permissions.Products.Edit)]
    public async Task<bool> DatSlugAsync(Guid id, string? slugTuNhap)
    {
        var sanPham = await _productRepository.GetAsync(id);

        // 🔑 Người dùng TỰ NHẬP ⇒ dùng CHUNG đường với `UpdateAsync` (khoá + kiểm trùng + phát sự kiện).
        //    🚨 `ApDungSlugAsync` **tự giành khoá** nên ở đây KHÔNG giữ khoá nữa (tránh khoá lồng nhau
        //    trên cùng một tên khoá — hành vi của `TryAcquireAsync` lồng nhau là **không xác định**).
        if (!string.IsNullOrWhiteSpace(slugTuNhap))
        {
            var slugCuTruoc = await ApDungSlugAsync(sanPham, slugTuNhap);

            if (slugCuTruoc is null)
            {
                Logger.LogInformation("SLUG KHÔNG ĐỔI (idempotent): sản phẩm {Code} vẫn là {Slug}. [SLUG-KHONG-DOI]",
                    sanPham.Code, sanPham.Slug);
                return false;
            }

            await _productRepository.UpdateAsync(sanPham, autoSave: true);

            Logger.LogInformation("ĐÃ ĐỔI SLUG sản phẩm {Code}: {SlugCu} → {SlugMoi}. [SLUG-DA-DOI]",
                sanPham.Code, slugCuTruoc, sanPham.Slug);

            return true;
        }

        await using var khoa = await _distributedLock.TryAcquireAsync("catalog:slug", TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "catalog:slug").WithData("choGiay", ChoKhoaToiDaGiay);

        // 🔑 Sinh TẤT ĐỊNH từ `Name` + hậu tố số khi trùng (`ADR-04`), kiểm trùng **trong cùng transaction**.
        // 📌 Duyệt **tuần tự có `await`** — KHÔNG dùng `.Result`/`.GetAwaiter().GetResult()` (chặn luồng
        // trong đường async ⇒ dễ deadlock, và nhiều provider DB cấm gọi đồng bộ). Thường chỉ 1 vòng.
        var slugMoi = string.Empty;

        foreach (var ungVien in SinhSlug.CacUngVien(sanPham.Name))
        {
            if (!await CoSlugKhacDungAsync(ungVien, sanPham.Id))
            {
                slugMoi = ungVien;
                break;
            }
        }

        if (slugMoi.Length == 0)
        {
            // 🚨 KHÔNG bịa `slug-{guid}` (ADR-04: Guid không có giá trị SEO) — ném lỗi để người dùng nhập tay.
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongSinhDuocSlug)
                .WithData("productCode", sanPham.Code)
                .WithData("name", sanPham.Name)
                .WithData("lyDo", "Tên sản phẩm không sinh được slug (toàn ký tự đặc biệt) ⇒ cần nhập slug tay.");
        }

        // 🔑 `DatSlug` trả **slug CŨ** (hoặc `null` nếu KHÔNG đổi) — xem chú thích ở entity.
        var slugCu = sanPham.DatSlug(slugMoi);

        if (slugCu is null)
        {
            // Idempotent: gọi lặp ⇒ **không** ghi DB, **không** phát sự kiện (tránh dòng 301 "A → A" và vòng lặp 301).
            Logger.LogInformation("SLUG KHÔNG ĐỔI (idempotent): sản phẩm {Code} vẫn là {Slug}. [SLUG-KHONG-DOI]",
                sanPham.Code, slugMoi);
            return false;
        }

        await _productRepository.UpdateAsync(sanPham, autoSave: true);

        Logger.LogInformation("ĐÃ ĐỔI SLUG sản phẩm {Code}: {SlugCu} → {SlugMoi}. [SLUG-DA-DOI]",
            sanPham.Code, slugCu ?? "(chưa có)", slugMoi);

        await PhatSuKienDoiSlugAsync(sanPham, slugCu);

        return true;
    }

    /// <summary>Slug này đã có **sản phẩm KHÁC** dùng chưa? 🔒 Bỏ qua chính sản phẩm đang sửa.</summary>
    private async Task<bool> CoSlugKhacDungAsync(string slug, Guid boQuaId)
        => await _productRepository.AnyAsync(x => x.Slug == slug && x.Id != boQuaId);

    /// <summary>
    /// 🔑 **ĐƯỜNG DUY NHẤT áp slug do NGƯỜI DÙNG gửi** (dùng chung cho `DatSlugAsync` và `UpdateAsync`):
    /// giành khoá phân tán → chuẩn hoá → kiểm trùng **trong cùng transaction** → `DatSlug` → **phát sự kiện**.
    ///
    /// 📌 **VÌ SAO PHẢI GOM VỀ MỘT CHỖ:** nếu `UpdateAsync` tự gán `sanPham.Slug = …` thì sự kiện
    /// `ProductSlugChangedEto` **không** được phát ⇒ `WEB-002` **không** dựng được **301** ⇒ khách vào link cũ
    /// nhận **404**, và **không có lỗi nào** để lần ra (SEO mất âm thầm). Đây đúng loại "hai đường ghi cho cùng
    /// một đại lượng" mà dự án đã trả giá (`XC-003` F1).
    /// </summary>
    /// <returns>Slug **CŨ** nếu slug thực sự đổi; `null` nếu **không đổi** (idempotent).</returns>
    private async Task<string?> ApDungSlugAsync(Product sanPham, string slugNguoiNhap)
    {
        await using var khoa = await _distributedLock.TryAcquireAsync("catalog:slug", TimeSpan.FromSeconds(ChoKhoaToiDaGiay))
            ?? throw new BusinessException(eCommerceConsts.ErrorCodes.KhongGianhDuocKhoa)
                .WithData("khoa", "catalog:slug").WithData("choGiay", ChoKhoaToiDaGiay);

        var slugMoi = SinhSlug.Tu(slugNguoiNhap);

        if (slugMoi.Length == 0)
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.KhongSinhDuocSlug)
                .WithData("slugNguoiNhap", slugNguoiNhap)
                .WithData("lyDo", "Slug tự nhập không còn ký tự dùng được sau khi chuẩn hoá.");
        }

        if (await CoSlugKhacDungAsync(slugMoi, sanPham.Id))
        {
            throw new BusinessException(eCommerceConsts.ErrorCodes.Catalog.SlugDaTonTai)
                .WithData("slug", slugMoi)
                .WithData("lyDo", "Slug này đã được sản phẩm KHÁC dùng ⇒ URL công khai sẽ trùng.");
        }

        var slugCu = sanPham.DatSlug(slugMoi);

        if (slugCu is null)
        {
            return null;   // Idempotent — không phát sự kiện (tránh dòng 301 "A → A" và vòng lặp 301).
        }

        await PhatSuKienDoiSlugAsync(sanPham, slugCu);

        return slugCu;
    }

    /// <summary>
    /// Phát <see cref="Events.ProductSlugChangedEto"/> kèm **`OldSlug`** — trả lời **Q3 của `WEB-002`**.
    /// ⚠️ Đây là **sự kiện MANG DỮ LIỆU**: `OldSlug` **chỉ** tồn tại trong sự kiện (DB **không** giữ lịch sử slug;
    /// bảng `WebSlugRoutes` là của `WEB-002` — `CAT-003` ADR-06 cố ý không sở hữu).
    /// ⇒ Khi có message broker, sự kiện này **phải** đi qua **outbox** (`XC-002`); hiện in-process là **nợ đã ghi**.
    /// </summary>
    private async Task PhatSuKienDoiSlugAsync(Product sanPham, string? slugCu)
        => await _eventBus.PublishAsync(new Events.ProductSlugChangedEto
        {
            TenantId = sanPham.TenantId,
            ProductId = sanPham.Id,
            ProductCode = sanPham.Code,
            OldSlug = slugCu,
            NewSlug = sanPham.Slug,
            OccurredAtUtc = DateTime.UtcNow
        });

    [Authorize(eCommerceConsts.Permissions.Products.Publish)]
    public async Task PublishAsync(Guid id)
    {
        var sanPham = await _productRepository.GetAsync(id);

        // 🚨 Dùng ĐÚNG máy trạng thái của domain: không có biến thể đang hoạt động ⇒ **NÉM LỖI** (D9).
        // ⚠️ Guard đọc `sanPham.Variants`; collection này được nạp nhờ **`AutoInclude`** khai trong
        //    `eCommerceDbContext` (`CatProducts`). **Nếu ai bỏ `AutoInclude`** thì `Variants` sẽ rỗng
        //    ⇒ **mọi lần publish đều ném lỗi** — ồn ào, KHÔNG im lặng (xem ghi chú ở mapping).
        sanPham.Publish();

        await _productRepository.UpdateAsync(sanPham, autoSave: true);

        Logger.LogInformation("Đã publish sản phẩm {Code}.", sanPham.Code);

        // 📣 Sự kiện THÔNG BÁO (xem `ProductPublishedEto`): để `ChannelHub`/storefront biết mà hành động.
        // ⚠️ Trạng thái `Published` **đã ghi** ở trên ⇒ mất sự kiện này **không mất dữ liệu**, chỉ **chậm**
        //    cho tới lần đối chiếu sau. (Đó là lý do ETO này gắn `INotificationEvent`, không fail-closed.)
        var bienTheDangBan = await _variantRepository.GetListAsync(x => x.ProductId == sanPham.Id && x.IsActive);

        await _eventBus.PublishAsync(new Events.ProductPublishedEto
        {
            TenantId = sanPham.TenantId,
            ProductId = sanPham.Id,
            Code = sanPham.Code,
            Name = sanPham.Name,
            Slug = sanPham.Slug,
            ActiveSkus = bienTheDangBan.Select(x => x.Sku).ToList(),
            OccurredAtUtc = DateTime.UtcNow
        });
    }

    [Authorize(eCommerceConsts.Permissions.Products.Publish)]
    public async Task UnpublishAsync(Guid id)
    {
        var sanPham = await _productRepository.GetAsync(id);

        sanPham.Unpublish();

        await _productRepository.UpdateAsync(sanPham, autoSave: true);

        Logger.LogInformation("Đã ngừng bán sản phẩm {Code}.", sanPham.Code);
    }

    [Authorize(eCommerceConsts.Permissions.Products.Publish)]
    public async Task ArchiveAsync(Guid id)
    {
        var sanPham = await _productRepository.GetAsync(id);

        sanPham.Archive();

        await _productRepository.UpdateAsync(sanPham, autoSave: true);

        Logger.LogInformation("Đã lưu trữ sản phẩm {Code}.", sanPham.Code);
    }

    [Authorize(eCommerceConsts.Permissions.Products.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var sanPham = await _productRepository.GetAsync(id);

        // D4: KHÔNG xoá cứng. Ngừng bán trước (nếu đang bán) rồi xoá MỀM ⇒ lịch sử tra được.
        // `Unpublish()` ném lỗi khi đang ở `Draft` — nên chỉ gọi khi đang Published.
        if (sanPham.IsPublished)
        {
            sanPham.Unpublish();
        }

        // 🚨 FK `Restrict` ở DB sẽ **CHẶN** xoá cứng nếu còn biến thể ⇒ ta chủ động đi đường xoá mềm.
        await _productRepository.DeleteAsync(sanPham, autoSave: true);

        Logger.LogInformation("Đã xoá MỀM sản phẩm {Code} (biến thể giữ nguyên để tra lịch sử).", sanPham.Code);
    }

    // ── Nội bộ ────────────────────────────────────────────────────────────────

    // 🚨 ĐÃ XOÁ `ChuanHoaSlug(slug) => slug.Trim().ToLowerInvariant()` (16/10/2026, `CAT-003`).
    // VÌ SAO XOÁ (chứ không để lại): nó **KHÔNG bỏ dấu tiếng Việt** ⇒ đường "tạo/cập nhật sản phẩm" cho ra
    // slug `đường-đi` trong khi đường "tự sinh" (`SinhSlug.Tu`) cho ra `duong-di` ⇒ **HAI dạng URL** cho cùng
    // một sản phẩm tuỳ theo đường ghi, và `canonical`/`sitemap` sẽ trỏ một dạng còn khách vào dạng kia.
    // 📌 Đây đúng họ lỗi "hai đường ghi cho cùng một đại lượng": để lại hàm cũ thì người sau **sẽ** dùng nó.
    private static ProductDto ToDto(Product sp, IReadOnlyCollection<ProductVariant> bienThe) => new()
    {
        Id = sp.Id,
        Code = sp.Code,
        Name = sp.Name,
        Slug = sp.Slug,
        Description = sp.Description,
        BrandId = sp.BrandId,
        Status = sp.Status.ToString(),
        IsPublished = sp.IsPublished,
        Variants = bienThe.Select(ToVariantDto).ToList(),
        CreationTime = sp.CreationTime
    };

    private static ProductVariantDto ToVariantDto(ProductVariant bt) => new()
    {
        Id = bt.Id,
        ProductId = bt.ProductId,
        Sku = bt.Sku,
        Barcode = bt.Barcode,
        Price = bt.Price,
        CompareAtPrice = bt.CompareAtPrice,
        WeightInGrams = bt.WeightInGrams,
        IsActive = bt.IsActive
    };
}
