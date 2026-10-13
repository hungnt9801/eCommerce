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

    // ---- Inventory ----
    public DbSet<Inventory.InventoryItem> InventoryItems { get; set; } = default!;
    public DbSet<Inventory.StockLedgerEntry> StockLedgerEntries { get; set; } = default!;

    // ---- Orders ----
    public DbSet<Orders.Order> Orders { get; set; } = default!;
    public DbSet<Orders.OrderLine> OrderLines { get; set; } = default!;

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

    // ── EInvoice (EINV-001): hoá đơn điện tử ──
    public DbSet<EInvoice.EInvoice> Invoices { get; set; } = default!;

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

        ConfigureCommerce(builder);

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

            b.HasMany(x => x.Variants)
                .WithOne()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
