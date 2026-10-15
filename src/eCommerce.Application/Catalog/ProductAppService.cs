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
            Slug = ChuanHoaSlug(input.Slug),
            Description = input.Description,
            BrandId = input.BrandId
        };

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
        sanPham.Slug = ChuanHoaSlug(input.Slug);
        sanPham.Description = input.Description;
        sanPham.BrandId = input.BrandId;

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

    private static string? ChuanHoaSlug(string? slug)
        => string.IsNullOrWhiteSpace(slug) ? null : slug.Trim().ToLowerInvariant();

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
