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
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
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
public class eCommerceDbContext : AbpDbContext<eCommerceDbContext>, IHasEventOutbox, IHasEventInbox
{
    // ══════════════════════════════════════════════════════════════════════════════════════════════════
    // 🚨 **OUTBOX / INBOX (P0 #3 — `XC-002` D1/D2, 24/10/2026).**
    //
    // 🔒 **BẮT BUỘC có 2 `DbSet` này** cùng 2 interface trên class: ABP chỉ ghi outbox/inbox khi DbContext
    // khai `IHasEventOutbox`/`IHasEventInbox` **và** có `DbSet` tương ứng (quy ước #5 — thiếu `DbSet` thì
    // `IRepository<T>` không đăng ký ⇒ lỗi Phase 0). 📌 Thiếu cấu hình này thì `AddToOutboxAsync` trả **false**
    // và ABP **rơi im lặng** về publish trực tiếp ⇒ *"có outbox"* trên giấy nhưng **không có** trên DB.
    // ══════════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Sự kiện **đã ghi cùng transaction** với dữ liệu nghiệp vụ, chờ relay phát (`AbpEventOutbox`).</summary>
    public DbSet<OutgoingEventRecord> OutgoingEvents { get; set; } = default!;

    /// <summary>Sự kiện **đến** đã ghi nhận để xử lý idempotent (`AbpEventInbox`).</summary>
    public DbSet<IncomingEventRecord> IncomingEvents { get; set; } = default!;

    // ---- Catalog ----
    public DbSet<Catalog.Product> Products { get; set; } = default!;
    public DbSet<Catalog.Brand> Brands { get; set; } = default!;

    // 🆕 REC-001 — lịch sử đối soát thanh toán (27/10/2026).
    public DbSet<eCommerce.Reconciliation.PaymentReconciliationRun> PaymentReconciliationRuns { get; set; } = default!;
    public DbSet<eCommerce.Reconciliation.PaymentReconciliationLine> PaymentReconciliationLines { get; set; } = default!;
    public DbSet<eCommerce.Reconciliation.PaymentDiscrepancy> PaymentDiscrepancies { get; set; } = default!;
    public DbSet<Catalog.ProductVariant> ProductVariants { get; set; } = default!;
    /// <summary>
    /// ẢNH SẢN PHẨM (`CAT-003`). 🚨 **BẮT BUỘC có `DbSet`** — thiếu ⇒ `IRepository&lt;ProductMedia, Guid&gt;`
    /// **không được đăng ký** (quy ước #3, lỗi Phase 0) ⇒ app service **không resolve được** ⇒ `HTTP 500`
    /// dù build/migration **đều xanh**. Lưới an toàn CI: `DbSetRegistrationTests`.
    /// </summary>
    public DbSet<Catalog.ProductMedia> ProductMedia { get; set; } = default!;
    /// <summary>
    /// HIỂN THỊ SẢN PHẨM THEO KÊNH (`CAT-003` `ADR-08`). 🚨 **BẮT BUỘC có `DbSet`** — thiếu ⇒
    /// `IRepository&lt;ProductChannelVisibility, Guid&gt;` **không được đăng ký** (quy ước #3) ⇒ app service
    /// **không resolve được** ⇒ **`HTTP 500`** dù build/migration **xanh**. Lưới an toàn CI: `DbSetRegistrationTests`.
    /// ⚠️ Và bài học `CAT-003` §20.3: `DbSet` **có** vẫn **chưa đủ** — module còn phải nằm trong đồ thị (`DependsOn`).
    /// </summary>
    public DbSet<Catalog.ProductChannelVisibility> ProductChannelVisibilities { get; set; } = default!;

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

    /// <summary>
    /// 🆕 `S12` — **GIÁ VỐN** (`PRC-001` §5.2, `PrcCostPrices`). 🚨 **Dữ liệu NỘI BỘ** (BR-013): chỉ đọc qua
    /// app service có quyền `eCommerce.Pricing.CostPrice.View`, **không** đường công khai.
    /// 📌 Thiếu `DbSet` ⇒ ABP không đăng ký `IRepository&lt;CostPrice, Guid&gt;` ⇒ DI nổ (`HTTP 500`) đúng lúc
    /// mở màn hình giá vốn — lưới an toàn: `DbSetRegistrationTests`.
    /// </summary>
    public DbSet<Pricing.CostPrice> CostPrices { get; set; } = default!;

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

    /// <summary>
    /// 🚨 **`INV-003` — HẠN MỨC PHÂN BỔ RIÊNG TỪNG KÊNH.** Không có `DbSet` ⇒ ABP **không** đăng ký
    /// `IRepository&lt;InvChannelLimit, Guid&gt;` ⇒ `StockReservationService` **không resolve được** ⇒ **HTTP 500**
    /// ngay lần giữ chỗ đầu tiên (đúng lỗi đã gặp ở `cat-002` §17, lưới an toàn: `DbSetRegistrationTests`).
    /// </summary>
    public DbSet<Inventory.InvChannelLimit> InvChannelLimits { get; set; } = default!;

    /// <summary>🆕 `S13` — **ngân sách tồn theo kênh** + **sổ cái append-only** (`INV-003`).</summary>
    public DbSet<Inventory.StockAllotment> StockAllotments { get; set; } = default!;

    public DbSet<Inventory.StockAllotmentLedger> StockAllotmentLedgers { get; set; } = default!;

    /// <summary>`REC-001` — **lịch sử lượt quét đối soát tồn kho** (+ dòng lệch). Xem chú thích `DbSet` phía trên.</summary>
    public DbSet<Inventory.ReconRun> ReconRuns { get; set; } = default!;

    /// <summary>`REC-001` — các cặp `(kho, SKU)` lệch của **một** lượt quét.</summary>
    public DbSet<Inventory.ReconRunMismatch> ReconRunMismatches { get; set; } = default!;

    // ── Customers (`CUS-001`) ───────────────────────────────────────────────────────────────────
    // 🚨 NHỚ: thiếu `DbSet` ⇒ ABP **KHÔNG** đăng ký `IRepository<T>` ⇒ DI nổ (**HTTP 500**) dù build/migration
    // đều xanh — xem `cat-002` §17.3. Lưới an toàn CI: `DbSetRegistrationTests`.
    public DbSet<Customers.Customer> Customers { get; set; } = default!;
    public DbSet<Customers.CustomerGroup> CustomerGroups { get; set; } = default!;

    /// <summary>
    /// 🚨 **NHAT KY KIEM TOAN XUAT DU LIEU CA NHAN** (`CUS-001` §22) — 🔑 tra loi *"AI / KHI NAO / BAO NHIEU LAN"*.
    ///
    /// ⚠️ **KHONG DUOC BO DONG NAY** — ABP dang ky `IRepository<T>` bang cach **QUET cac thuoc tinh
    /// `DbSet<>`** tren DbContext (xem chu thich o dau danh sach nay). 📌 Thieu no thi:
    /// entity **van co bang trong DB** va **van mapping duoc**, nhung 🔒 `IRepository<CustomerDataExportLog>`
    /// **khong duoc dang ky** ⇒ DI **nem luc chay** ⇒ 📌 `/KhachHang` tra **`500`** — ⚠️ va trieu chung
    /// **khong he** tro tới nhat ky (do la `GET` danh sach khach).
    /// ➕ Day la **loi DA GAP THAT** khi lam lat cat nay (`CUS-001` §22.5).
    /// </summary>
    public DbSet<Customers.CustomerDataExportLog> CustomerDataExportLogs { get; set; } = default!;

    // ---- Orders ----
    public DbSet<Orders.Order> Orders { get; set; } = default!;
    public DbSet<Orders.OrderLine> OrderLines { get; set; } = default!;

    /// <summary>
    /// 🚨 **NHẬT KÝ TRẠNG THÁI ĐƠN HÀNG** (`SAL-003` D7) — **BẮT BUỘC có `DbSet`** (quy ước #5):
    /// thiếu ⇒ `IRepository&lt;OrderStatusHistory, Guid&gt;` **không được đăng ký** ⇒ API đọc lịch sử
    /// trả **`HTTP 500`** dù build/migration xanh. Lưới an toàn CI: `DbSetRegistrationTests`.
    /// </summary>
    public DbSet<Orders.OrderStatusHistory> OrderStatusHistories { get; set; } = default!;

    /// <summary>
    /// 🆕 `S14` — **PHIẾU TRẢ HÀNG (RMA)** (`SAL-006`). 🚨 Thiếu `DbSet` ⇒ `IRepository&lt;OrderReturn, Guid&gt;`
    /// không được đăng ký ⇒ API RMA **`HTTP 500`** dù build xanh (quy ước #5; lưới an toàn `DbSetRegistrationTests`).
    /// </summary>
    public DbSet<Orders.OrderReturn> OrderReturns { get; set; } = default!;

    public DbSet<Orders.OrderReturnLine> OrderReturnLines { get; set; } = default!;

    /// <summary>`S14` — nhật ký chuyển trạng thái phiếu trả (**append-only**).</summary>
    public DbSet<Orders.OrderReturnHistory> OrderReturnHistories { get; set; } = default!;

    /// <summary>
    /// Nhật ký nhận đơn từ sàn (`SAL-005`). 🚨 **BẮT BUỘC có `DbSet`** — thiếu ⇒
    /// `IRepository&lt;MarketplaceOrderIntake, Guid&gt;` **không được đăng ký** (quy ước #3, lỗi Phase 0)
    /// ⇒ handler nhận đơn **không resolve được** ⇒ đơn lại mất.
    /// </summary>
    public DbSet<Orders.MarketplaceOrderIntake> MarketplaceOrderIntakes { get; set; } = default!;

    /// <summary>
    /// 🚨 **NHẬT KÝ SỰ KIỆN ĐƠN SÀN** (`SAL-005` D7). **BẮT BUỘC có `DbSet`** — cùng lý do khối trên:
    /// thiếu `DbSet` ⇒ `IRepository&lt;MarketplaceEventLog, Guid&gt;` **không được đăng ký** ⇒ `HTTP 500`
    /// ngay sự kiện đầu tiên, ⚠️ dù build/migration đều xanh. Lưới an toàn CI: `DbSetRegistrationTests`
    /// + `MarketplaceEventLogBatBienTests`.
    /// </summary>
    public DbSet<Orders.MarketplaceEventLog> MarketplaceEventLogs { get; set; } = default!;

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

    /// <summary>
    /// 🚨 **DÒNG HOÀN TIỀN** (`S5` — `PAY-001` §5.6 #1). **BẮT BUỘC có `DbSet`** — cùng lý do khối ChannelHub
    /// ở trên: ABP đăng ký `IRepository<T>` bằng cách **quét `DbSet`**, nên thiếu nó thì
    /// `IRepository&lt;PaymentRefund, Guid&gt;` **không resolve được** ⇒ **`HTTP 500`** ngay lần hoàn tiền đầu
    /// tiên, ⚠️ dù build/migration **đều xanh**. Lưới an toàn CI: `DbSetRegistrationTests`.
    /// </summary>
    public DbSet<PaymentRefund> PaymentRefunds { get; set; } = default!;

    // ---- Tax (module) — XC-006 ----
    // ⚠️ Phải khai báo DbSet, nếu không IRepository<TaxClassification, Guid> KHÔNG được đăng ký
    //    (xem ghi chú dài ở khối ChannelHub phía trên).
    public DbSet<TaxRateSchedule> TaxRateSchedules { get; set; } = default!;
    public DbSet<TaxClassification> TaxClassifications { get; set; } = default!;

    // ── Messaging (XC-002): bản ghi chống trùng ở biên vào ──────────────────
    // ⚠️ BẮT BUỘC có DbSet: thiếu DbSet thì `IRepository<IdempotencyRecord, Guid>` KHÔNG được
    // đăng ký và lỗi chỉ lộ ra lúc chạy ("has not been registered") — bài học Phase 0.
    public DbSet<Messaging.Idempotency.IdempotencyRecord> IdempotencyRecords { get; set; } = default!;
    /// <summary>
    /// 🚨 **HÀNG ĐỢI THƯ CHẾT** (`XC-002`). **BẮT BUỘC có `DbSet`** — thiếu ⇒
    /// `IRepository&lt;DeadLetterMessage, Guid&gt;` **không được đăng ký** và lỗi chỉ lộ ra **lúc chạy**
    /// (*"has not been registered"*, bài học Phase 0). Lưới an toàn CI: `DbSetRegistrationTests`.
    /// </summary>
    public DbSet<Messaging.DeadLetter.DeadLetterMessage> DeadLetterMessages { get; set; } = default!;

    // ── Address (ADDR-001): danh mục đơn vị hành chính theo PHIÊN BẢN schema ──
    public DbSet<Address.AdministrativeUnit> AdministrativeUnits { get; set; } = default!;

    // ── Address (ADDR-002): ánh xạ mã địa chỉ theo hãng vận chuyển ──
    public DbSet<Address.Carrier.CarrierAddressMapping> CarrierAddressMappings { get; set; } = default!;

    // ── Address (ADDR-004): SỔ ĐỊA CHỈ — địa chỉ VẬT LÝ dùng chung trong tenant ──
    // 🚨 BẮT BUỘC có DbSet cho MỌI entity: ABP đăng ký `IRepository<T>` bằng cách **quét DbSet**,
    //    KHÔNG quét model. Thiếu ⇒ DI nổ lúc chạy ⇒ HTTP 500, dù build và migration đều XANH.
    //    Lưới an toàn: `test/eCommerce.Tests/EfCoreRegistration/DbSetRegistrationTests.cs`.
    public DbSet<Address.Address> Addresses { get; set; } = default!;

    // ── Customers (ADDR-004 phần B): SỔ ĐỊA CHỈ CỦA KHÁCH — liên kết khách ↔ địa chỉ ──
    // 🔒 `AddressId` là **tham chiếu MỀM** (KHÔNG FK): `Address` thuộc module `Address` (`ADR-01`).
    public DbSet<Customers.CustomerAddressLink> CustomerAddressLinks { get; set; } = default!;

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

        // `CUS-001` — hồ sơ khách + nhóm khách. 🔒 Hai bất biến DB ở đây: `IdempotencyKey` duy nhất theo tenant
        // và **một tài khoản ↔ tối đa một khách** (`ADR-07`) — cả hai cần SQL thô `NULLS NOT DISTINCT` + bộ lọc.
        global::eCommerce.Customers.EntityFrameworkCore.CustomersDbContextModelCreatingExtensions.ConfigureCustomers(builder);

        // Module ABP (thêm ở Phase 0) tự cung cấp cấu hình model của mình.
        // Mỗi hàm ConfigureXxx() đến từ package Volo.Abp.Xxx.EntityFrameworkCore tương ứng.
        builder.ConfigureIdentity();
        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureFeatureManagement();
        builder.ConfigureAuditLogging();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureOpenIddict();

        // 🚨 **OUTBOX/INBOX** (`XC-002` §6.3) — hai hàm này map bảng `AbpEventOutbox`/`AbpEventInbox` từ
        // `IHasEventOutbox`/`IHasEventInbox`. 📌 Thiếu chúng thì bảng **không tồn tại** và outbox **im lặng
        // không ghi** (không có lỗi build/migration).
        builder.ConfigureEventOutbox();
        builder.ConfigureEventInbox();
    }

    /// <summary>
    /// Mapping của domain commerce (đang nằm trong app).
    /// Khi domain này lớn lên, tách thành <c>modules/Commerce/...</c> theo đúng khuôn ChannelHub.
    /// </summary>
    private static void ConfigureCommerce(ModelBuilder builder)
    {
        // 🆕 `P0 #1` (24/10/2026) — mapping Catalog đã chuyển sang MODULE (`Catalog.EntityFrameworkCore`).
        // 🔒 Gọi **một chỗ**; migration phải **RỖNG** sau khi chuyển (đã kiểm).
        Catalog.EntityFrameworkCore.CatalogDbContextModelCreatingExtensions.ConfigureCatalog(builder);


        // 🆕 `P0 #1` (24/10/2026) — mapping tồn kho đã chuyển sang MODULE `Inventory.EntityFrameworkCore`.
        // 🔒 Recon (`ReconRun`…) **vẫn ở host** cho tới khi tách `Reconciliation`.
        Inventory.EntityFrameworkCore.InventoryDbContextModelCreatingExtensions.ConfigureInventory(builder);


        // ── REC-001: LỊCH SỬ LƯỢT QUÉT ĐỐI SOÁT TỒN KHO ────────────────────────────────────────────
        // 🆕 `P0 #1` (24/10/2026) — mapping đối soát kho đã chuyển sang MODULE `Reconciliation.EntityFrameworkCore`.
        global::eCommerce.Reconciliation.EntityFrameworkCore.ReconciliationDbContextModelCreatingExtensions.ConfigureReconciliation(builder);


        // 🚨 `XC-002` — **HÀNG ĐỢI THƯ CHẾT**. Xem chú thích entity để biết vì sao "chỉ ghi log" là KHÔNG ĐỦ
        // (log bị xoay vòng, không tra cứu theo khoá, không biết còn tồn bao nhiêu ⇒ **mất dữ liệu nhưng có vẻ
        // như đã xử lý**).
        builder.Entity<Messaging.DeadLetter.DeadLetterMessage>(b =>
        {
            b.ToTable("MsgDeadLetters");
            b.ConfigureByConvention();

            b.Property(x => x.TenSuKien).IsRequired().HasMaxLength(Messaging.DeadLetter.DeadLetterMessage.DoDaiTenSuKienToiDa);
            b.Property(x => x.KhoaNghiepVu).IsRequired().HasMaxLength(Messaging.DeadLetter.DeadLetterMessage.DoDaiKhoaNghiepVuToiDa);
            b.Property(x => x.MoTaLyDo).IsRequired().HasMaxLength(Messaging.DeadLetter.DeadLetterMessage.DoDaiLyDoToiDa);
            b.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.NguoiXuLy).HasMaxLength(256);
            b.Property(x => x.LyDoXuLy).HasMaxLength(Messaging.DeadLetter.DeadLetterMessage.DoDaiLyDoToiDa);
            b.Property(x => x.CorrelationId).HasMaxLength(128);

            // 📌 `PayloadJson` là `text` (không giới hạn độ dài): 🔒 **CẮT payload là không phát lại được** —
            // đúng bất biến (1) ở chú thích entity.
            b.Property(x => x.PayloadJson).IsRequired();

            b.Property(x => x.LyDo).HasConversion<int>();

            // 📌 Truy vấn NÓNG NHẤT: *"còn gì CHƯA xử lý?"* — index **có bộ lọc** để luôn **nhỏ**.
            // 🔑 Nghịch lý của mọi hàng đợi lỗi: sau một thời gian, **phần lớn** dòng đã xử lý ⇒ không lọc thì
            // chính truy vấn *"còn việc gì"* trở thành truy vấn **nặng nhất** ⇒ chậm dần cho tới khi **không ai đọc**.
            // 📌 Cùng khuôn `IX_ReconRunMismatches_TenantId_Run_ChuaXuLy` (`REC-001` §17.4).
            b.HasIndex(x => new { x.TenantId, x.VaoDlqLucUtc })
                .HasFilter("NOT \"DaXuLy\"")
                .HasDatabaseName("IX_MsgDeadLetters_TenantId_ChuaXuLy");

            // 📌 Tra theo **khoá nghiệp vụ** — câu hỏi *"đơn/sự kiện này có vấn đề gì?"*.
            b.HasIndex(x => new { x.TenantId, x.KhoaNghiepVu })
                .HasDatabaseName("IX_MsgDeadLetters_TenantId_Khoa");
        });
        // 🆕 `P0 #1` (24/10/2026) — mapping đơn hàng/sàn đã chuyển sang MODULE `Sales.EntityFrameworkCore`.
        global::eCommerce.Sales.EntityFrameworkCore.SalesDbContextModelCreatingExtensions.ConfigureSales(builder);

    }
}