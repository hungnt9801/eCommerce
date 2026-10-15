using eCommerce.ChannelHub.Channels;
using eCommerce.ChannelHub.EntityFrameworkCore;
using eCommerce.PaymentHub.EntityFrameworkCore;
using eCommerce.PaymentHub.Payments;
using eCommerce.Tax.Classifications;
using eCommerce.Tax.EntityFrameworkCore;
using eCommerce.Tax.RateSchedules;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;

namespace eCommerce.EntityFrameworkCore;

/// <summary>
/// MỘT DbContext duy nhất cho toàn bộ modular monolith.
///
/// Vì sao một DbContext chứ không phải mỗi module một DbContext:
///   • modular monolith = một database, một connection, một migration history;
///   • ranh giới module vẫn được giữ vì mỗi module tự cung cấp hàm mapping
///     (<c>ConfigureChannelHub()</c>, <c>ConfigurePaymentHub()</c>) và module KHÔNG
///     tham chiếu DbContext của app.
/// Khi tách microservice: mỗi module đổi sang DbContext riêng + migration riêng.
/// </summary>
[ConnectionStringName(eCommerceConsts.DefaultConnectionStringName)]
public class eCommerceDbContext : AbpDbContext<eCommerceDbContext>
{
    // ---- Catalog ----
    public DbSet<Catalog.Product> Products { get; set; } = default!;
    public DbSet<Catalog.ProductVariant> ProductVariants { get; set; } = default!;

    /// <summary>
    /// 🚨 **`DbSet` NÀY LÀ BẮT BUỘC, KHÔNG PHẢI "CHO ĐẸP"** (`CAT-002`, phát hiện 15/10/2026):
    /// ABP đăng ký `IRepository<T>` bằng cách **quét các thuộc tính `DbSet<>`** trên DbContext —
    /// **KHÔNG** quét model. Chỉ khai `builder.Entity<Category>(...)` trong `OnModelCreating` mà **quên**
    /// `DbSet` ⇒ **không có** `IRepository<Category, Guid>` ⇒ DI của app service **NỔ** khi có request đầu tiên:
    /// *"None of the constructors found … Cannot resolve parameter IRepository`2[eCommerce.Catalog.Category …]"*
    /// ⇒ **HTTP 500** (đã gặp thật, xem `cat-002` §17).
    /// 🛡️ Lưới an toàn: `DbSetRegistrationTests` quét model và khẳng định **mọi** entity `eCommerce.*` đều có `DbSet`.
    /// </summary>
    public DbSet<Catalog.Category> Categories { get; set; } = default!;

    /// <summary>Liên kết sản phẩm ↔ danh mục (`CAT-002`).</summary>
    public DbSet<Catalog.ProductCategory> ProductCategories { get; set; } = default!;

    // ── Thuộc tính + tổ hợp biến thể (`CAT-002` lát cắt 4) ──────────────────────────────────────
    // 🚨 NHỚ: thiếu `DbSet` ⇒ ABP **KHÔNG** đăng ký `IRepository<T>` ⇒ DI nổ (**HTTP 500**) dù build/migration
    // đều xanh — xem `cat-002` §17.3. Có lưới an toàn CI: `DbSetRegistrationTests`.
    public DbSet<Catalog.CatalogAttribute> CatalogAttributes { get; set; } = default!;
    public DbSet<Catalog.CatalogAttributeValue> CatalogAttributeValues { get; set; } = default!;
    public DbSet<Catalog.ProductAttributeSelection> ProductAttributeSelections { get; set; } = default!;
    public DbSet<Catalog.ProductVariantAttribute> ProductVariantAttributes { get; set; } = default!;

    // ── Pricing (`PRC-001` / `PRC-002`) ────────────────────────────────────────────────────────
    // 🚨 Thiếu `DbSet` ⇒ ABP **KHÔNG** đăng ký `IRepository<T>` ⇒ DI nổ (**HTTP 500**) dù build/migration xanh.
    // Lưới an toàn CI: `DbSetRegistrationTests` (mọi entity `eCommerce.*` trong model phải có `DbSet`).
    public DbSet<Pricing.PriceList> PriceLists { get; set; } = default!;
    public DbSet<Pricing.PriceListItem> PriceListItems { get; set; } = default!;
    public DbSet<Pricing.ChannelPrice> ChannelPrices { get; set; } = default!;

    // PRC-002 — **bậc thang giá**: bảng RIÊNG vì `PriceListItem` duy nhất theo `(TenantId, PriceListId, Sku)`
    // (`PRC-001` BR-008) ⇒ **không chứa được nhiều bậc** cho cùng một SKU (`PRC-002` ADR-01).
    public DbSet<Pricing.TierPriceSet> TierPriceSets { get; set; } = default!;
    public DbSet<Pricing.TierPrice> TierPrices { get; set; } = default!;

    // PRC-002 ADR-13 — **sổ giải trình tính giá** (append-only) + khoá idempotency.
    public DbSet<Pricing.PriceCalculationRecord> PriceCalculationRecords { get; set; } = default!;

    // ---- Inventory ----
    public DbSet<Inventory.InventoryItem> InventoryItems { get; set; } = default!;
    public DbSet<Inventory.StockLedgerEntry> StockLedgerEntries { get; set; } = default!;

    // INV-001 — **kho** + **chứng từ kho** (chứng từ và ghi sổ là HAI bước; ADR-01/ADR-02).
    public DbSet<Inventory.Warehouse> Warehouses { get; set; } = default!;
    public DbSet<Inventory.StockDocument> StockDocuments { get; set; } = default!;
    public DbSet<Inventory.StockDocumentLine> StockDocumentLines { get; set; } = default!;

    // INV-002 — **giữ chỗ** (reservation) có TTL; chống bán vượt tồn khi nhiều khách tranh nhau.
    public DbSet<Inventory.StockReservation> StockReservations { get; set; } = default!;

    // ---- Orders ----
    public DbSet<Orders.Order> Orders { get; set; } = default!;
    public DbSet<Orders.OrderLine> OrderLines { get; set; } = default!;

    /// <summary>
    /// Nhật ký nhận đơn từ sàn (`SAL-005`). 🚨 **BẮT BUỘC có `DbSet`** — thiếu ⇒
    /// `IRepository&lt;MarketplaceOrderIntake, Guid&gt;` **không được đăng ký** (quy ước #3, lỗi Phase 0)
    /// ⇒ handler nhận đơn **không resolve được** ⇒ đơn lại mất.
    /// </summary>
    public DbSet<Orders.MarketplaceOrderIntake> MarketplaceOrderIntakes { get; set; } = default!;

    // ---- ChannelHub (module) ----
    // 🚨 VÌ SAO PHẢI KHAI BÁO DbSet Ở ĐÂY:
    // AddDefaultRepositories(includeAllEntities: true) chỉ tạo IRepository<T> cho entity
    // CÓ DbSet trên DbContext. Nếu chỉ gọi builder.ConfigureChannelHub() (cấu hình model)
    // mà không khai báo DbSet thì runtime báo:
    //   "The requested service 'IRepository<ChannelShop, Guid>' has not been registered."
    public DbSet<Channel> Channels { get; set; } = default!;
    public DbSet<ChannelShop> ChannelShops { get; set; } = default!;
    public DbSet<ChannelListing> ChannelListings { get; set; } = default!;

    // ---- PaymentHub (module) ----
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = default!;

    // ---- Tax (module) — XC-006 ----
    // ⚠️ Phải khai báo DbSet, nếu không IRepository<TaxClassification, Guid> KHÔNG được đăng ký
    //    (xem ghi chú dài ở khối ChannelHub phía trên).
    public DbSet<TaxRateSchedule> TaxRateSchedules { get; set; } = default!;
    public DbSet<TaxClassification> TaxClassifications { get; set; } = default!;

    // ── Messaging (XC-002): bản ghi chống trùng ở biên vào ──────────────────
    // ⚠️ BẮT BUỘC có DbSet: thiếu DbSet thì `IRepository<IdempotencyRecord, Guid>` KHÔNG được
    // đăng ký và lỗi chỉ lộ ra lúc chạy ("has not been registered") — bài học Phase 0.
    public DbSet<Messaging.Idempotency.IdempotencyRecord> IdempotencyRecords { get; set; } = default!;

    // ── Address (ADDR-001): danh mục đơn vị hành chính theo PHIÊN BẢN schema ──
    public DbSet<Address.AdministrativeUnit> AdministrativeUnits { get; set; } = default!;

    // ── Address (ADDR-002): ánh xạ mã địa chỉ theo hãng vận chuyển ──
    public DbSet<Address.Carrier.CarrierAddressMapping> CarrierAddressMappings { get; set; } = default!;

    // ── EInvoice (EINV-001): hoá đơn điện tử ──
    public DbSet<EInvoice.EInvoice> Invoices { get; set; } = default!;

    // ── Signing (SIGN-001): yêu cầu ký 2 bước + chứng thư số ──
    public DbSet<Signing.SigningRequest> SigningRequests { get; set; } = default!;
    public DbSet<Signing.SigningCertificate> SigningCertificates { get; set; } = default!;

    public eCommerceDbContext(DbContextOptions<eCommerceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Cấu hình do từng module tự cung cấp.
        builder.ConfigureChannelHub();
        builder.ConfigurePaymentHub();
        builder.ConfigureTax();
        // Gọi qua tên đầy đủ để KHÔNG phải thêm using vào file này.
        Messaging.EntityFrameworkCore.MessagingDbContextModelCreatingExtensions.ConfigureMessaging(builder);
        Address.EntityFrameworkCore.AddressDbContextModelCreatingExtensions.ConfigureAddress(builder);
        EInvoice.EntityFrameworkCore.EInvoiceDbContextModelCreatingExtensions.ConfigureEInvoice(builder);
        Signing.EntityFrameworkCore.SigningDbContextModelCreatingExtensions.ConfigureSigning(builder);

        ConfigureCommerce(builder);

        // Module Pricing (`PRC-001`/`PRC-002`) — mapping nằm trong module, gọi từ DbContext hợp nhất.
        Pricing.EntityFrameworkCore.PricingDbContextModelCreatingExtensions.ConfigurePricing(builder);

        // Module ABP (thêm ở Phase 0) tự cung cấp cấu hình model của mình.
        // Mỗi hàm ConfigureXxx() đến từ package Volo.Abp.Xxx.EntityFrameworkCore tương ứng.
        builder.ConfigureIdentity();
        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureFeatureManagement();
        builder.ConfigureAuditLogging();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureOpenIddict();
    }

    /// <summary>
    /// Mapping của domain commerce (đang nằm trong app).
    /// Khi domain này lớn lên, tách thành <c>modules/Commerce/...</c> theo đúng khuôn ChannelHub.
    /// </summary>
    private static void ConfigureCommerce(ModelBuilder builder)
    {
        builder.Entity<Catalog.Product>(b =>
        {
            b.ToTable("CatProducts");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(512);
            b.Property(x => x.Slug).HasMaxLength(512);
            b.Property(x => x.Description).HasColumnType("text");

            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            // 🚨 `AutoInclude` — BẮT BUỘC để **máy trạng thái của domain nhìn thấy biến thể**.
            // VÌ SAO: repository của ABP **không** tự nạp collection con. Nếu thiếu dòng này thì
            // `Product.Variants` **luôn rỗng** khi đọc từ DB ⇒ `Publish()` (guard `CAT-001` D9) luôn thấy
            // "0 biến thể đang hoạt động" ⇒ **mọi lần publish đều ném lỗi** — mà đó lại là hành vi
            // **trông như hợp lý** ("chưa có biến thể thì không publish được") nên rất khó truy.
            // ⇒ Nạp kèm để guard đọc **đúng trạng thái thật của aggregate**.
            // ⚠️ Đánh đổi đã biết: mọi truy vấn `Product` đều join thêm biến thể. Với quy mô GĐ1 chấp nhận
            //    được; nếu danh mục lớn lên thì cân nhắc bỏ `AutoInclude` và nạp tường minh theo từng ca.
            b.Navigation(x => x.Variants).AutoInclude();

            // 🚨 `CAT-001` **F7/D4** — ĐỔI TỪ `Cascade` SANG `Restrict` (15/10/2026).
            // VÌ SAO: `Cascade` khiến xoá cứng một `Product` **XOÁ MỌI BIẾN THỂ** của nó. Biến thể là thứ
            // được tham chiếu bởi **lịch sử bán** (`OrdOrderLines.ProductVariantId`) và **listing sàn**
            // (`ChannelListings.ProductVariantId`) ⇒ xoá cascade **phá lịch sử** và để lại tham chiếu treo,
            // **không có lỗi nào**. Nay DB **CHẶN** xoá cứng khi còn biến thể ⇒ buộc đi qua **xoá mềm**
            // (`IsDeleted`, `CAT-001` D4) + unpublish ở tầng app service.
            b.HasMany(x => x.Variants)
                .WithOne()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ProductVariant>(b =>
        {
            b.ToTable("CatProductVariants");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.Barcode).HasMaxLength(64);

            // decimal(18,4) — tiền tệ KHÔNG bao giờ dùng double.
            b.Property(x => x.Price).HasPrecision(18, 4);
            b.Property(x => x.CompareAtPrice).HasPrecision(18, 4);

            b.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();

            b.Property(x => x.AttributeHash).HasMaxLength(128);

            // 🚨🚨 **HÀNG RÀO CUỐI CHO BẤT BIẾN "TỔ HỢP DUY NHẤT TRÊN MỖI SẢN PHẨM"** (`CAT-002` D7/BR-010).
            //
            // ⚠️ **LỆCH CÓ CHỦ Ý SO VỚI BẢNG INDEX CỦA TÀI LIỆU (§5.2)** — và đây là **sửa một lỗi trong tài liệu**:
            // tài liệu ghi `UNIQUE (TenantId, ProductVariantId, AttributeHash)` **trên chính bảng `CatProductVariants`**.
            // Nhưng trong bảng này `ProductVariantId` **chính là** khoá chính (`Id`) ⇒ index đó **luôn luôn đúng**
            // và **KHÔNG chặn được gì** (vô nghĩa). Ý định thật của bất biến là *"tổ hợp duy nhất trên mỗi
            // **SẢN PHẨM**"* ⇒ phải là `(TenantId, ProductId, AttributeHash)` — và bảng này **có** `ProductId`.
            //
            // 🎯 Vì sao phải có: `AttributeHash` là **khoá đối chiếu** khi sinh lại tổ hợp (BR-013). Không có
            // hàng rào này thì 2 request đồng thời (hoặc Redis chết) tạo **hai biến thể cùng tổ hợp** ⇒
            // storefront hiện 2 lựa chọn giống nhau và **không ai biết cái nào đúng**.
            // Chỉ áp cho biến thể CÓ hash (biến thể thủ công của sản phẩm đơn giản có hash `NULL`).
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.AttributeHash })
                .IsUnique()
                .HasFilter("\"AttributeHash\" IS NOT NULL")
                .HasDatabaseName("IX_CatProdVariants_TenantId_ProductId_AttributeHash");
        });

        // ── Cây danh mục (`CAT-002` D1/D2/D3/D9) ────────────────────────────────────────────────
        builder.Entity<Catalog.Category>(b =>
        {
            b.ToTable("CatCategories");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Slug).HasMaxLength(256);

            // `Path` = `/guid/guid/` — vật chất hoá để lấy cả cây bằng MỘT truy vấn tiền tố (D1).
            b.Property(x => x.Path).IsRequired().HasMaxLength(Catalog.Category.DoDaiPathToiDa);

            // `Code` duy nhất trong tenant. ⚠️ Unique index này **PHẢI** là `NULLS NOT DISTINCT`
            // (hệ thống hiện 1 tenant ⇒ `TenantId` có thể NULL) — EF **không** sinh được, nên migration
            // có bước SQL thô sửa lại (xem `Fix_NullableTenantId_UniqueIndexes` làm mẫu).
            // Tên index đặt TƯỜNG MINH: tên tự sinh dài quá 63 ký tự sẽ bị PostgreSQL **cắt cụt**
            // (đúng lỗi đã gặp ở `SalMarketplaceOrderIntakes`).
            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_CatCategories_TenantId_Code");

            // Truy vấn "các con của một nút" (dựng menu từng cấp) và truy vấn cây con theo tiền tố `Path`.
            b.HasIndex(x => new { x.TenantId, x.ParentId })
                .HasDatabaseName("IX_CatCategories_TenantId_ParentId");

            b.HasIndex(x => new { x.TenantId, x.Path })
                .HasDatabaseName("IX_CatCategories_TenantId_Path");

            // 🚨 `Restrict` (`CAT-002` D9): xoá cứng một nút KHÔNG được kéo theo con (Cascade) và cũng
            // không được để con **mồ côi**. DB chặn ở đây; app service đi đường **xoá mềm**.
            b.HasOne<Catalog.Category>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🚨 Hàng rào cuối cho 2 bất biến mà tầng domain cũng kiểm tra (CAT-002 §8.1 #2 và #3).
            // ⚠️ Đây là CHECK **định dạng/khoảng**, KHÔNG phải ràng buộc ngữ nghĩa: nó **không** phát hiện
            // được `Path` không khớp cây thật, và **không** phát hiện được chu trình (§8.1 #1) —
            // phần ngữ nghĩa phải do `CategoryTreeService` + test lo.
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_CatCategories_Path_DinhDang", "\"Path\" LIKE '/%/'");
                t.HasCheckConstraint("CK_CatCategories_Level_Khoang", "\"Level\" >= 0 AND \"Level\" <= 5");
            });
        });

        // ── Liên kết sản phẩm ↔ danh mục (`CAT-002` BR-006) ──────────────────────────────────────
        builder.Entity<Catalog.ProductCategory>(b =>
        {
            b.ToTable("CatProductCategories");
            b.ConfigureByConvention();

            // Chống GÁN TRÙNG cùng một cặp (sản phẩm, danh mục).
            // ⚠️ Tên TƯỜNG MINH: tên EF tự sinh dài > 63 ký tự ⇒ PostgreSQL **cắt cụt** (lỗi đã gặp thật).
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.CategoryId })
                .IsUnique()
                .HasDatabaseName("IX_CatProdCats_TenantId_ProductId_CategoryId");

            // Truy vấn "sản phẩm trong một danh mục" (storefront).
            b.HasIndex(x => new { x.TenantId, x.CategoryId })
                .HasDatabaseName("IX_CatProdCats_TenantId_CategoryId");

            // 🚨 **TỐI ĐA MỘT danh mục CHÍNH cho mỗi sản phẩm** (BR-006) — unique index **CÓ FILTER**.
            // Đây là hàng rào DUY NHẤT diễn đạt được bất biến này: unique index KHÔNG có điều kiện sẽ chặn
            // luôn việc một sản phẩm nằm trong NHIỀU danh mục (điều hoàn toàn hợp lệ).
            // ⚠️ EF không sinh được `NULLS NOT DISTINCT` cho index có filter ⇒ migration có bước SQL thô.
            b.HasIndex(x => new { x.TenantId, x.ProductId })
                .IsUnique()
                .HasFilter("\"IsPrimary\"")
                .HasDatabaseName("IX_CatProdCats_TenantId_ProductId_ChiMotChinh");

            // FK `Restrict`: xoá cứng sản phẩm/danh mục KHÔNG được kéo mất liên kết ở đây.
            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.Category>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Thuộc tính (`CAT-002` D4/BR-008) ──────────────────────────────────────────────────────
        builder.Entity<Catalog.CatalogAttribute>(b =>
        {
            b.ToTable("CatAttributes");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);

            // `Usage` lưu dạng `int` (enum) — đọc bằng mắt trong DB là số, ghi rõ để không ai "sửa tay" sai.
            b.Property(x => x.Usage).HasConversion<int>();

            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_CatAttributes_TenantId_Code");
        });

        builder.Entity<Catalog.CatalogAttributeValue>(b =>
        {
            b.ToTable("CatAttributeValues");
            b.ConfigureByConvention();

            b.Property(x => x.Value).IsRequired().HasMaxLength(256);

            b.HasIndex(x => new { x.TenantId, x.AttributeId, x.Value })
                .IsUnique()
                .HasDatabaseName("IX_CatAttrValues_TenantId_AttributeId_Value");

            b.HasOne<Catalog.CatalogAttribute>()
                .WithMany()
                .HasForeignKey(x => x.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ProductAttributeSelection>(b =>
        {
            b.ToTable("CatProductAttributeSelections");
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.TenantId, x.ProductId, x.AttributeId, x.AttributeValueId })
                .IsUnique()
                .HasDatabaseName("IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId");

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.CatalogAttribute>()
                .WithMany()
                .HasForeignKey(x => x.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.CatalogAttributeValue>()
                .WithMany()
                .HasForeignKey(x => x.AttributeValueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ProductVariantAttribute>(b =>
        {
            b.ToTable("CatProductVariantAttributes");
            b.ConfigureByConvention();

            // Mỗi biến thể có **ĐÚNG MỘT** giá trị cho mỗi thuộc tính (BR §8.1 #6).
            b.HasIndex(x => new { x.TenantId, x.ProductVariantId, x.AttributeId })
                .IsUnique()
                .HasDatabaseName("IX_CatProdVarAttr_TenantId_VariantId_AttributeId");

            b.HasOne<Catalog.ProductVariant>()
                .WithMany()
                .HasForeignKey(x => x.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.CatalogAttribute>()
                .WithMany()
                .HasForeignKey(x => x.AttributeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.CatalogAttributeValue>()
                .WithMany()
                .HasForeignKey(x => x.AttributeValueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Inventory.InventoryItem>(b =>
        {
            b.ToTable("InvItems");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // Một SKU chỉ có một bản ghi tồn kho cho mỗi kho trong mỗi tenant.
            // Index này là hàng rào cuối cùng chống ghi trùng khi có race condition.
            b.HasIndex(x => new { x.TenantId, x.WarehouseId, x.Sku }).IsUnique();
        });

        // ── INV-001 ADR-01: KHO ──────────────────────────────────────────────────────────────────
        builder.Entity<Inventory.Warehouse>(b =>
        {
            b.ToTable("InvWarehouses");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Address).HasMaxLength(512);

            // Mã kho duy nhất trong tenant — hàng rào DB (sửa tay NULLS NOT DISTINCT trong migration).
            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_InvWarehouses_TenantId_Code");
        });

        // ── INV-001 ADR-02: CHỨNG TỪ KHO + DÒNG CHỨNG TỪ ─────────────────────────────────────────
        builder.Entity<Inventory.StockDocument>(b =>
        {
            b.ToTable("InvStockDocuments");
            b.ConfigureByConvention();

            b.Property(x => x.Number).IsRequired().HasMaxLength(64);
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.Note).HasMaxLength(2048);
            b.Property(x => x.IdempotencyKey).HasMaxLength(128);

            // Số chứng từ duy nhất trong tenant — chống tạo trùng khi retry/import.
            b.HasIndex(x => new { x.TenantId, x.Number })
                .IsUnique()
                .HasDatabaseName("IX_InvStockDocuments_TenantId_Number");

            b.HasIndex(x => new { x.TenantId, x.Status, x.Type, x.OccurredAtUtc })
                .HasDatabaseName("IX_InvStockDocuments_TenantId_Status_Type_OccurredAtUtc");
        });

        builder.Entity<Inventory.StockDocumentLine>(b =>
        {
            b.ToTable("InvStockDocumentLines");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // Một SKU chỉ xuất hiện MỘT lần trong một chứng từ: hai dòng cùng SKU trong cùng phiếu nhập
            // ⇒ người đọc phiếu thấy "nhập 5" nhưng sổ cái ghi hai bút toán ⇒ **cộng sai** mà không lỗi nào.
            b.HasIndex(x => new { x.TenantId, x.StockDocumentId, x.Sku })
                .IsUnique()
                .HasDatabaseName("IX_InvStockDocumentLines_TenantId_Doc_Sku");

            b.HasOne<Inventory.StockDocument>()
                .WithMany()
                .HasForeignKey(x => x.StockDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── INV-002: GIỮ CHỖ ─────────────────────────────────────────────────────────────────────
        builder.Entity<Inventory.StockReservation>(b =>
        {
            b.ToTable("InvStockReservations");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.ReferenceType).HasMaxLength(64);
            b.Property(x => x.ReferenceId).HasMaxLength(128);

            // Truy vấn của JOB nhả chỗ: các lượt `Held` đã quá hạn.
            b.HasIndex(x => new { x.TenantId, x.Status, x.ExpiresAtUtc })
                .HasDatabaseName("IX_InvStockReservations_TenantId_Status_ExpiresAtUtc");

            // Một nguồn (giỏ/đơn) + một SKU chỉ giữ chỗ **MỘT** lần ⇒ chống tạo trùng khi retry.
            // (Sửa tay `NULLS NOT DISTINCT` trong migration — nếu thiếu thì `TenantId = NULL` làm index vô hiệu.)
            b.HasIndex(x => new { x.TenantId, x.ReferenceId, x.Sku, x.WarehouseId })
                .IsUnique()
                .HasFilter("\"ReferenceId\" IS NOT NULL")
                .HasDatabaseName("IX_InvStockReservations_TenantId_Ref_Sku_Kho");
        });

        builder.Entity<Inventory.StockLedgerEntry>(b =>
        {
            b.ToTable("InvStockLedger");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.ReferenceType).HasMaxLength(64);
            b.Property(x => x.ReferenceId).HasMaxLength(128);
            b.Property(x => x.ChannelCode).HasMaxLength(32);
            b.Property(x => x.Note).HasMaxLength(2048);

            // Truy vấn thường gặp: lịch sử biến động của một SKU trong một kho, mới nhất trước.
            b.HasIndex(x => new { x.TenantId, x.WarehouseId, x.Sku, x.OccurredAtUtc });

            // Đối soát theo kênh.
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.OccurredAtUtc });

            // 🚨 **`INV-001` ADR-05 — HÀNG RÀO CHỐNG GHI TRÙNG SỔ CÁI.**
            // Ghi sổ một dòng chứng từ HAI lần (retry · job chạy lại · người bấm hai lần) ⇒ lần hai **bị chặn**.
            // 📌 Kiểm ở tầng app ("đã ghi chưa?") **KHÔNG đủ**: hai request **đồng thời** cùng thấy "chưa ghi"
            // ⇒ ghi hai lần ⇒ **tồn kho tăng gấp đôi mà không có lỗi nào**.
            // ⚠️ Bộ lọc `ReferenceLineId IS NOT NULL` là **BẮT BUỘC** (sửa tay trong migration —
            // EF không sinh được `NULLS NOT DISTINCT` lẫn bộ lọc kèm nhau):
            // bút toán điều chỉnh tay KHÔNG có dòng chứng từ; nếu unique **không** bộ lọc thì toàn hệ thống
            // chỉ được phép có **MỘT** bút toán không tham chiếu.
            // 🚨 **SỬA 15/10/2026 — LỖI THẬT DO KIỂM CHỨNG HTTP BẮT ĐƯỢC:** phiên bản đầu chỉ unique
            // `(TenantId, ReferenceLineId)` ⇒ **CHẶN OAN** vế thứ hai của **phiếu chuyển kho** (một dòng chứng từ
            // chuyển kho sinh **HAI** bút toán: `TransferOut` ở nguồn + `TransferIn` ở đích) ⇒ ghi sổ chuyển kho
            // **luôn 500** (`23505`), tức là **tính năng chuyển kho không dùng được**.
            // ✅ Nay unique theo **`(TenantId, ReferenceLineId, MovementType)`**: hai vế có **loại khác nhau**
            // ⇒ qua được; còn **ghi sổ lại cùng một dòng cùng một loại** (retry / hai request đồng thời)
            // vẫn **BỊ CHẶN** — giữ nguyên mục đích chống ghi trùng (ADR-05).
            b.HasIndex(x => new { x.TenantId, x.ReferenceLineId, x.MovementType })
                .IsUnique()
                .HasFilter("\"ReferenceLineId\" IS NOT NULL")
                .HasDatabaseName("IX_InvStockLedger_TenantId_ReferenceLineId");
        });

        builder.Entity<Orders.Order>(b =>
        {
            b.ToTable("OrdOrders");
            b.ConfigureByConvention();

            b.Property(x => x.Number).IsRequired().HasMaxLength(64);
            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.ExternalOrderId).HasMaxLength(64);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(8);
            b.Property(x => x.Note).HasMaxLength(2048);
            b.Property(x => x.CheckoutIdempotencyKey).HasMaxLength(128);

            // 🚨 **MỘT checkout chỉ được sinh MỘT đơn.** Không có index này thì khách bấm hai lần / client retry
            // ⇒ **HAI ĐƠN** cho cùng một lần mua ⇒ giao hàng hai lần hoặc thu tiền hai lần.
            // (Sửa tay `NULLS NOT DISTINCT` trong migration — nếu thiếu thì `TenantId = NULL` làm index VÔ HIỆU.)
            b.HasIndex(x => new { x.TenantId, x.CheckoutIdempotencyKey })
                .IsUnique()
                .HasFilter("\"CheckoutIdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("IX_OrdOrders_TenantId_CheckoutIdempotencyKey");

            b.Property(x => x.Subtotal).HasPrecision(18, 4);
            b.Property(x => x.DiscountTotal).HasPrecision(18, 4);
            b.Property(x => x.TaxTotal).HasPrecision(18, 4);
            b.Property(x => x.ShippingTotal).HasPrecision(18, 4);
            b.Property(x => x.GrandTotal).HasPrecision(18, 4);
            b.Property(x => x.ChannelFeeAmount).HasPrecision(18, 4);

            b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();

            // 🎯 IDEMPOTENCY: webhook của sàn là at-least-once. Index unique này bảo đảm
            // cùng một đơn của sàn không thể tạo hai lần trong cùng tenant.
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.ExternalOrderId })
                .IsUnique()
                .HasFilter("\"ExternalOrderId\" IS NOT NULL");

            b.HasMany(x => x.Lines)
                .WithOne()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Orders.OrderLine>(b =>
        {
            b.ToTable("OrdOrderLines");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.ExternalLineId).HasMaxLength(64);
            b.Property(x => x.UnitPrice).HasPrecision(18, 4);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 4);
            b.Property(x => x.TaxAmount).HasPrecision(18, 4);

            // Chống trùng dòng đơn khi webhook gửi lại.
            b.HasIndex(x => new { x.OrderId, x.ExternalLineId })
                .IsUnique()
                .HasFilter("\"ExternalLineId\" IS NOT NULL");
        });

        // ---- Nhật ký nhận đơn từ sàn (`SAL-005`) ----
        builder.Entity<Orders.MarketplaceOrderIntake>(b =>
        {
            b.ToTable("SalMarketplaceOrderIntakes");
            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.ExternalShopId).IsRequired().HasMaxLength(64);
            b.Property(x => x.ExternalOrderId).IsRequired().HasMaxLength(64);
            b.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.CorrelationId).HasMaxLength(64);
            b.Property(x => x.SkipReason).HasMaxLength(500);

            // 🎯 Hàng rào chống trùng ở mức ĐƠN (khoá nghiệp vụ giống `OrdOrders`).
            // ⚠️ EF **không** mô hình hoá được `NULLS NOT DISTINCT` ⇒ migration sinh ra phải được
            //    bổ sung mệnh đề đó (xem migration `Add_Sal005_MarketplaceOrderIntake`), nếu không
            //    index **mất tác dụng im lặng** khi `TenantId` NULL (lỗi C4/F1).
            // ⚠️ ĐẶT TÊN TƯỜNG MINH: tên EF tự sinh là
            //    `IX_SalMarketplaceOrderIntakes_TenantId_ChannelCode_ExternalOrderId` = **67 ký tự**,
            //    vượt giới hạn **63** của PostgreSQL ⇒ bị **cắt** (kèm dấu `~`) ⇒ test bất biến index
            //    (đối chiếu **đích danh tên**) sẽ không tìm thấy. Tên dưới đây = **52 ký tự**.
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.ExternalOrderId })
                .IsUnique()
                .HasDatabaseName("IX_SalMktIntakes_TenantId_ChannelCode_ExternalOrderId");
        });
    }
}
