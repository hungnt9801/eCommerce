using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Catalog.EntityFrameworkCore;

/// <summary>
/// **MAPPING EF CỦA MODULE `Catalog`** (`P0 #1`, 24/10/2026 — lát cắt 2).
///
/// 🔒 **CHUYỂN NGUYÊN VĂN** từ `eCommerceDbContext.ConfigureCommerce` — không sửa một dòng cấu hình nào,
/// để `dotnet ef migrations add` cho ra migration **RỖNG** (điều kiện của việc tách module).
/// 📌 Host gọi hàm này từ `OnModelCreating`; module **không** tham chiếu host.
/// </summary>
public static class CatalogDbContextModelCreatingExtensions
{
    public static void ConfigureCatalog(this ModelBuilder builder)
    {        builder.Entity<Catalog.Product>(b =>
        {
            b.ToTable("CatProducts", t =>
            {
                // 🚨 VAT chỉ có nghĩa trong `-100..100` (`03 §3.1`: -1 không chịu thuế, -2 không kê khai,
                // -100 thuế 0%). Ngoài khoảng ⇒ hoá đơn sai thuế **mà không có lỗi nào**.
                t.HasCheckConstraint("CK_CatProducts_VatPercent_Khoang",
                    "\"VatPercent\" IS NULL OR (\"VatPercent\" >= -100 AND \"VatPercent\" <= 100)");
            });
            b.ConfigureByConvention();

            // 🔗 CatBrands (27/10/2026 — Q3): FK Restrict — xoá cứng thương hiệu KHÔNG kéo mất sản phẩm.
            b.HasOne<Catalog.Brand>()
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Property(x => x.Code).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(512);
            b.Property(x => x.Slug).HasMaxLength(512);
            b.Property(x => x.Description).HasColumnType("text");

            // ── WP-03 (02/11/2026) — cột mới của mô hình Nhanh ──────────────────────────────────────
            // 🔒 Lưu **SỐ** (enum) như mọi enum khác của repo: đổi tên enum là sửa MÃ, không phải sửa DỮ LIỆU.
            // 🚨 `HasDefaultValue` là **BẮT BUỘC**, không phải trang trí: cột `int NOT NULL` thêm vào bảng đã có
            // dữ liệu mà **không** có default sẽ nhận `0` — và `0` **không phải** giá trị hợp lệ của cả
            // `ProductType` (1..10) lẫn `VatMode` (1..2) ⇒ mọi sản phẩm cũ thành "loại không tồn tại".
            b.Property(x => x.Type).HasConversion<int>().HasDefaultValue(ProductType.Standard);
            b.Property(x => x.VatMode).HasConversion<int>().HasDefaultValue(VatMode.Excluded);
            // % VAT có số lẻ (5.5, 8.25) ⇒ `decimal`, KHÔNG `double` (quy ước #1).
            b.Property(x => x.VatPercent).HasPrecision(5, 2);
            b.Property(x => x.ExternalId).HasMaxLength(64);

            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            // Truy vấn "các con của một sản phẩm cha" (bảng sản phẩm nhóm theo cha).
            b.HasIndex(x => new { x.TenantId, x.ParentId })
                .HasDatabaseName("IX_CatProducts_TenantId_ParentId");

            // Đối chiếu dữ liệu nhập từ hệ thống ngoài (`ExternalId`). ⚠️ KHÔNG unique: một mã ngoài có thể
            // được ánh xạ cho nhiều bản ghi trong lúc nhập liệu dở, và chặn unique sẽ làm mất dữ liệu nguồn.
            b.HasIndex(x => new { x.TenantId, x.ExternalId })
                .HasDatabaseName("IX_CatProducts_TenantId_ExternalId");

            // 🔗 Cha–con (D-07): `Restrict` — xoá cứng một sản phẩm KHÔNG được kéo mất con và cũng không
            // được để con **mồ côi**. Đường xoá đúng là xoá mềm ở app service (CAT-001 D4).
            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🚨 **`CAT-003` ADR-03 — SLUG LÀ KHOÁ ĐỊNH DANH CÔNG KHAI TRÊN URL ⇒ PHẢI DUY NHẤT.**
            // (`WEB-002` §11 đã giao việc này cho `CAT-003`; `CAT-001` F5 chuyển sang đây.)
            // 📌 VÌ SAO KHÔNG CHỈ KIỂM Ở TẦNG APP: hai request đồng thời cùng thấy *"slug chưa ai dùng"*
            // ⇒ **cả hai ghi** ⇒ hai sản phẩm cùng một URL ⇒ **một trong hai không bao giờ truy cập được**,
            // và **máy tìm kiếm đánh chỉ mục SAI sản phẩm** — im lặng hoàn toàn (đúng họ lỗi `XC-003` F1).
            // ⚠️ Bộ lọc `Slug IS NOT NULL` là **BẮT BUỘC** (không thì index vô hiệu vì mọi sản phẩm chưa có
            // slug đều `NULL`), và **cần SQL thô** vì EF **không** sinh được `NULLS NOT DISTINCT` kèm bộ lọc
            // (cùng cách `Inv001_Fix_SoCai_Unique_ThemMovementType` đã làm).
            // 🔒 Tên đặt TƯỜNG MINH: tên EF tự sinh `IX_CatProducts_TenantId_Slug` là **33 ký tự** (an toàn),
            // nhưng đặt tay để `PostgreSqlSchemaTests` đối chiếu **đích danh** được.
            b.HasIndex(x => new { x.TenantId, x.Slug })
                .IsUnique()
                .HasFilter("\"Slug\" IS NOT NULL")
                .HasDatabaseName("IX_CatProducts_TenantId_Slug");

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

            // 🔗 ĐVT (WP-03). 🚨 PHẢI khai `HasMany` TƯỜNG MINH: nếu chỉ khai `HasOne` phía `ProductUnit`, EF coi
            // `Product.Units` là một quan hệ **thứ hai** ⇒ sinh cột bóng `ProductId1` + FK thừa, và dữ liệu ĐVT
            // sẽ được ghi qua cột bóng đó (không ai đọc) ⇒ **mất ĐVT im lặng**. Cùng lý do đã ghi ở `Variants`.
            b.HasMany(x => x.Units)
                .WithOne()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🚨 **`AutoInclude` CHO ĐVT — BẮT BUỘC từ WP-03 nửa sau** (02/11/2026).
            // VÌ SAO: `Product.Publish()` nay chấp nhận **ĐVT gốc** làm "có gì để bán" (D-07), mà repository
            // **không** tự nạp collection con ⇒ nếu thiếu dòng này thì `Product.Units` **luôn rỗng** khi đọc
            // từ DB ⇒ sản phẩm tạo qua `POST /api/v1/products` (3 ĐVT, 0 biến thể) **vẫn không publish được**
            // — đúng nợ `handover-n2` §2.8 tưởng đã sửa. 📌 Đây là loại lỗi "sửa nửa đường": guard đúng,
            // dữ liệu nạp sai ⇒ **không có lỗi biên dịch**, chỉ có 403 khi bấm publish.
            // ⚠️ Đánh đổi như `Variants`: mọi truy vấn `Product` join thêm bảng ĐVT. Quy mô GĐ1 chấp nhận.
            b.Navigation(x => x.Units).AutoInclude();
        });

        // 🚨 `CAT-003` — **ẢNH SẢN PHẨM**. Xem `ADR-02` (metadata ở DB, **bytes ở object storage**),
        // `ADR-12` (tối đa MỘT ảnh chính/sản phẩm), `ADR-13` (định dạng do MÁY CHỦ kiểm bằng magic bytes).
        // 🚨 `CAT-003` `ADR-08` — **HIỂN THỊ SẢN PHẨM THEO KÊNH**.
        builder.Entity<Catalog.ProductChannelVisibility>(b =>
        {
            b.ToTable("CatProductChannelVisibilities");
            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(Catalog.ProductChannelVisibility.DoDaiMaKenhToiDa);
            b.Property(x => x.LyDo).HasMaxLength(Catalog.ProductChannelVisibility.DoDaiLyDoToiDa);

            // 🔒 Lưu **SỐ** (enum) ⇒ chỉ có MỘT nguồn quyết định nghĩa của trạng thái.
            // 📌 Nếu lưu chuỗi thì đổi tên enum là sửa **dữ liệu**, không phải sửa **mã** — và dữ liệu cũ
            // sẽ có giá trị không ai còn kiểm được.
            b.Property(x => x.TrangThai).HasConversion<int>();

            // Truy vấn nóng: "kênh này đang HIỂN THỊ gì / ẩn gì".
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.TrangThai })
                .HasDatabaseName("IX_CatProductChannelVisibilities_TenantId_Kenh_TrangThai");

            // 🚨🚨 **HÀNG RÀO CUỐI CHO BẤT BIẾN `(TenantId, ProductId, ChannelCode)` LÀ DUY NHẤT.**
            //
            // **VÌ SAO PHẢI Ở DB (không chỉ ở app service):** đặt trạng thái là thao tác *"đọc bản ghi hiện tại
            // → quyết định → ghi"*. Hai request **ĐỒNG THỜI** (double-click, hai tab, hai nhân viên) **cùng**
            // đọc thấy *"chưa có bản ghi"* ⇒ **cả hai** `INSERT` ⇒ **HAI bản ghi** cho cùng cặp
            // `(sản phẩm, kênh)` ⇒ mọi truy vấn hiển thị trả **kết quả khác nhau tuỳ truy vấn** (kênh đọc cái
            // này, báo cáo đọc cái kia) — và **không có lỗi nào nổi lên**.
            // 📌 Đây **đúng** bài học `XC-003` D2: **"bỏ app đi, DB có chặn không?"**
            // ⚠️ Cần **SQL thô** trong migration: EF Core 10 **không** sinh được `NULLS NOT DISTINCT`.
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.ChannelCode })
                .IsUnique()
                .HasDatabaseName("IX_CatProductChannelVisibilities_TenantId_SanPham_Kenh");

            // 📌 FK `Restrict` (không `Cascade`): xoá cứng một sản phẩm **KHÔNG** được kéo mất cấu hình hiển thị
            // theo kênh một cách âm thầm — đó là **quyết định kinh doanh** đã ghi (kèm lý do cho ca ẩn).
            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ProductMedia>(b =>
        {
            b.ToTable("CatProductMedia");
            b.ConfigureByConvention();

            b.Property(x => x.KhoaLuuTru).IsRequired().HasMaxLength(Catalog.ProductMedia.DoDaiKhoaLuuTruToiDa);
            b.Property(x => x.Alt).HasMaxLength(Catalog.ProductMedia.DoDaiAltToiDa);

            // 🔒 `DinhDangDaKiem` lưu **SỐ** (enum) ⇒ chỉ có MỘT nguồn quyết định `Content-Type` khi phục vụ tệp.
            // 📌 Nếu lưu chuỗi MIME thô thì thêm/đổi định dạng là sửa **dữ liệu**, không phải sửa **mã** —
            // và dữ liệu cũ sẽ có giá trị không ai còn kiểm được.
            b.Property(x => x.DinhDangDaKiem).HasConversion<int>();

            // Truy vấn nóng: "ảnh của sản phẩm này, theo thứ tự hiển thị".
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.ThuTu })
                .HasDatabaseName("IX_CatProductMedia_TenantId_ProductId_ThuTu");

            // 🚨🚨 **HÀNG RÀO CUỐI CHO `ADR-12`: TỐI ĐA MỘT ẢNH CHÍNH MỖI SẢN PHẨM.**
            //
            // **VÌ SAO PHẢI Ở DB (không chỉ ở app service):** đặt ảnh chính là thao tác *"đọc ảnh chính hiện
            // tại → bỏ đánh dấu → đánh dấu ảnh mới"*. Hai request **đồng thời** (double-click, hai tab, hai
            // nhân viên) **cùng** đọc thấy *"chưa có ảnh chính"* ⇒ **cả hai** đánh dấu ⇒ **HAI ảnh chính**
            // ⇒ mọi chỗ đọc *"ảnh chính là ảnh nào"* trả **kết quả khác nhau tuỳ truy vấn** (thẻ chia sẻ dùng
            // một ảnh, danh sách dùng ảnh khác) — **không lỗi nào nổi lên**.
            // 📌 Đây đúng bài học `XC-003` D2: **"bỏ app đi, DB có chặn không?"**
            // ⚠️ Cần **SQL thô** trong migration: EF **không** sinh được unique index **có bộ lọc** này
            // (và càng không sinh được `NULLS NOT DISTINCT` — dù index này **không** cần vì `TenantId` có
            // `NULLS NOT DISTINCT` **không** áp dụng cho bộ lọc một phần tử… xem chú thích ở migration).
            b.HasIndex(x => new { x.TenantId, x.ProductId })
                .IsUnique()
                .HasFilter("\"LaAnhChinh\"")
                .HasDatabaseName("IX_CatProductMedia_MotAnhChinh");

            // 📌 FK `Restrict` (không `Cascade`): xoá cứng một sản phẩm **KHÔNG** được kéo mất ảnh một cách
            // âm thầm — ảnh là **tài sản** (đã tốn dung lượng lưu trữ và công nhập liệu).
            // ⚠️ Nhưng ảnh **cũng** phải dọn theo sản phẩm ⇒ việc đó thuộc **app service** (xoá mềm sản phẩm
            // + gọi `IMediaStorage.XoaAsync`), xem `CAT-003` §16.8.
            b.HasOne<Catalog.Product>()
                .WithMany()
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

        // ── WP-03 (02/11/2026) — MÔ HÌNH HÀNG HÓA THEO NHANH (`03 §3.1`, D-07) ─────────────────────
        //
        // 🚨 BA LUẬT ÁP CHO MỌI BẢNG DƯỚI ĐÂY:
        // 1. **Additive**: toàn bộ là bảng MỚI ⇒ dữ liệu thật hiện có không phải sửa (migration 51 có `Down`).
        // 2. **Unique index trên entity `IMultiTenant` PHẢI `NULLS NOT DISTINCT`**: hệ thống đang chạy 1 tenant
        //    ⇒ `TenantId` có thể `NULL`; PostgreSQL mặc định coi hai `NULL` là KHÁC nhau ⇒ index **mất tác dụng
        //    im lặng** (lỗi C4/F1 đã trả giá — `XC-004` Q6). EF không sinh được mệnh đề này ⇒ migration 51
        //    có bước SQL thô (cùng cách `Cat003_ProductSlug_Unique`).
        // 3. **FK `Restrict`** (không `Cascade`): xoá cứng một sản phẩm không được kéo mất đơn vị/IMEI/lô — đó là
        //    dữ liệu đã được lịch sử bán và sổ kho tham chiếu (`CAT-001` D4/F7).

        builder.Entity<Catalog.ProductUnit>(b =>
        {
            b.ToTable("CatProductUnits", t =>
            {
                t.HasCheckConstraint("CK_CatProdUnits_ConversionFactor_Duong", "\"ConversionFactor\" > 0");
                t.HasCheckConstraint("CK_CatProdUnits_SalePrice_KhongAm", "\"SalePrice\" >= 0");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(Catalog.ProductUnit.DoDaiTenToiDa);
            b.Property(x => x.Barcode).HasMaxLength(Catalog.ProductUnit.DoDaiBarcodeToiDa);
            b.Property(x => x.ConversionFactor).HasPrecision(18, 4);
            b.Property(x => x.SalePrice).HasPrecision(18, 4);
            b.Property(x => x.ImportPrice).HasPrecision(18, 4);
            b.Property(x => x.WholesalePrice).HasPrecision(18, 4);
            b.Property(x => x.OldPrice).HasPrecision(18, 4);

            // Tên ĐVT duy nhất trong một sản phẩm ("lon" không thể có hai dòng giá).
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.Name })
                .IsUnique()
                .HasDatabaseName("IX_CatProdUnits_TenantId_ProductId_Name");

            // 🚨 **BARCODE DUY NHẤT TOÀN TENANT** (`03 §3.1` bảng index). Quét mã là đường bán hàng nhanh
            // nhất ở POS: hai ĐVT cùng barcode ⇒ thu ngân bán **sai món** và không có lỗi nào nổi lên.
            // Filter `Barcode IS NOT NULL`: ĐVT không có mã vạch (`null`) không được chặn lẫn nhau.
            b.HasIndex(x => new { x.TenantId, x.Barcode })
                .IsUnique()
                .HasFilter("\"Barcode\" IS NOT NULL")
                .HasDatabaseName("IX_CatProdUnits_TenantId_Barcode");

            // 🚨 **ĐÚNG MỘT ĐVT GỐC MỖI SẢN PHẨM** — unique index có filter là cách DUY NHẤT diễn đạt được
            // (unique không filter sẽ chặn luôn sản phẩm có nhiều ĐVT — điều hợp lệ).
            b.HasIndex(x => new { x.TenantId, x.ProductId })
                .IsUnique()
                .HasFilter("\"IsBase\"")
                .HasDatabaseName("IX_CatProdUnits_MotDonViGoc");
        });

        builder.Entity<Catalog.ProductImei>(b =>
        {
            b.ToTable("CatProductImeis");
            b.ConfigureByConvention();

            b.Property(x => x.Serial).IsRequired().HasMaxLength(Catalog.ProductImei.DoDaiSerialToiDa);
            b.Property(x => x.Note).HasMaxLength(Catalog.ProductImei.DoDaiGhiChuToiDa);
            b.Property(x => x.Status).HasConversion<int>();

            // Serial duy nhất toàn tenant — hai máy cùng IMEI là dữ liệu sai (nhập trùng/hàng giả).
            b.HasIndex(x => new { x.TenantId, x.Serial })
                .IsUnique()
                .HasDatabaseName("IX_CatProdImeis_TenantId_Serial");

            // Truy vấn nóng: "serial còn tồn / đã bán của sản phẩm này".
            b.HasIndex(x => new { x.TenantId, x.ProductId, x.Status })
                .HasDatabaseName("IX_CatProdImeis_TenantId_ProductId_Status");

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.ProductBatch>()
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ProductBatch>(b =>
        {
            b.ToTable("CatProdBatches", t =>
            {
                // NSX sau HSD là dữ liệu vô nghĩa ⇒ cảnh báo hạn dùng sẽ sai.
                t.HasCheckConstraint("CK_CatProdBatches_Ngay_Khoang",
                    "\"ManufactureDate\" IS NULL OR \"ExpiredDate\" IS NULL OR \"ManufactureDate\" <= \"ExpiredDate\"");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(Catalog.ProductBatch.DoDaiCodeToiDa);

            b.HasIndex(x => new { x.TenantId, x.ProductId, x.Code })
                .IsUnique()
                .HasDatabaseName("IX_CatProdBatches_TenantId_ProductId_Code");

            // Truy vấn "hàng sắp hết hạn" — theo hạn dùng.
            b.HasIndex(x => new { x.TenantId, x.ExpiredDate })
                .HasDatabaseName("IX_CatProdBatches_TenantId_ExpiredDate");

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ComboComponent>(b =>
        {
            b.ToTable("CatComboComponents", t =>
                t.HasCheckConstraint("CK_CatComboComponents_Quantity_Duong", "\"Quantity\" > 0"));
            b.ConfigureByConvention();

            b.Property(x => x.Quantity).HasPrecision(18, 3);

            b.HasIndex(x => new { x.TenantId, x.ComboProductId, x.ComponentProductId })
                .IsUnique()
                .HasDatabaseName("IX_CatComboComponents_TenantId_Combo_Component");

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ComboProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.PackageComponent>(b =>
        {
            b.ToTable("CatPackageComponents", t =>
                t.HasCheckConstraint("CK_CatPackageComponents_Quantity_Duong", "\"Quantity\" > 0"));
            b.ConfigureByConvention();

            b.Property(x => x.Quantity).HasPrecision(18, 3);

            b.HasIndex(x => new { x.TenantId, x.PackageProductId, x.ComponentProductId })
                .IsUnique()
                .HasDatabaseName("IX_CatPackageComponents_TenantId_Package_Component");

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.PackageProductId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ComponentProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Catalog.ProductTag>(b =>
        {
            b.ToTable("CatProductTags");
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(Catalog.ProductTag.DoDaiTenToiDa);

            b.HasIndex(x => new { x.TenantId, x.ProductId, x.Name })
                .IsUnique()
                .HasDatabaseName("IX_CatProductTags_TenantId_ProductId_Name");

            // Truy vấn "mọi sản phẩm mang nhãn X" (bộ lọc 2 tầng của màn danh sách).
            b.HasIndex(x => new { x.TenantId, x.Name })
                .HasDatabaseName("IX_CatProductTags_TenantId_Name");

            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── WP-03 NỬA SAU (02/11/2026) — BẢNG ĐỐI CHIẾU CỦA BACKFILL (`handover-n3`) ─────────────
        // 🔑 Câu hỏi bảng này trả lời: *"biến thể cũ nào đã chuyển thành sản phẩm con nào"* — và trả lời
        // **bằng SQL**, không bằng log của một lần chạy. Nhờ nó lần chạy thứ hai **không tạo trùng**.
        builder.Entity<Catalog.ProductVariantMap>(b =>
        {
            b.ToTable("CatVariantMaps");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // 🚨 MỘT biến thể chỉ được chuyển MỘT lần — `NULLS NOT DISTINCT` (viết tay ở migration 52):
            // hệ thống đang chạy 1 tenant ⇒ `TenantId` có thể `NULL`, mà PostgreSQL mặc định coi hai `NULL`
            // là KHÁC nhau ⇒ index **mất tác dụng im lặng** (lỗi C4/F1). Thiếu ràng buộc này thì hai lần
            // chạy (hoặc chạy lại sau lỗi giữa đường) tạo **hai sản phẩm con** cho cùng một SKU.
            b.HasIndex(x => new { x.TenantId, x.VariantId })
                .IsUnique()
                .HasDatabaseName("IX_CatVariantMaps_TenantId_VariantId");

            // Đối soát theo SKU (mắt người + script) và truy vấn "sản phẩm con này sinh từ biến thể nào".
            b.HasIndex(x => new { x.TenantId, x.Sku })
                .HasDatabaseName("IX_CatVariantMaps_TenantId_Sku");

            b.HasIndex(x => new { x.TenantId, x.ProductId })
                .HasDatabaseName("IX_CatVariantMaps_TenantId_ProductId");

            // 🔒 FK `Restrict`: xoá cứng sản phẩm con KHÔNG được để lại dòng đối chiếu trỏ vào hư không
            // (khi đó lần backfill sau sẽ tưởng "đã chuyển" và **bỏ qua** biến thể đó — im lặng, sai).
            b.HasOne<Catalog.Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
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
        builder.Entity<Catalog.Brand>(b =>
        {
            b.ToTable("CatBrands");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(Catalog.Brand.DoDaiCodeToiDa);
            b.Property(x => x.Name).IsRequired().HasMaxLength(Catalog.Brand.DoDaiTenToiDa);
            b.Property(x => x.Slug).IsRequired().HasMaxLength(Catalog.Brand.DoDaiSlugToiDa);
            b.Property(x => x.Description).HasMaxLength(Catalog.Brand.DoDaiMoTaToiDa);
            b.Property(x => x.LogoUrl).HasMaxLength(Catalog.Brand.DoDaiLogoUrlToiDa);

            // 🔒 Unique theo TENANT. `NULLS NOT DISTINCT` bổ sung bằng SQL thô trong migration
            // (EF không khai báo được) — nếu thiếu, `TenantId = NULL` làm index mất tác dụng im lặng.
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Slug })
                .IsUnique()
                .HasDatabaseName("IX_CatBrands_TenantId_Slug");

            // Truy vấn danh sách admin: lọc đang hoạt động + sắp theo thứ tự.
            b.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder })
                .HasDatabaseName("IX_CatBrands_TenantId_HoatDong_ThuTu");
        });
    }
}