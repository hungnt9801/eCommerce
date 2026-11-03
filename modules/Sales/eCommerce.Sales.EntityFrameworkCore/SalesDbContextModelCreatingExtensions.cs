using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Sales.EntityFrameworkCore;

/// <summary>
/// **MAPPING EF CỦA MODULE `Sales`** (`P0 #1`, 24/10/2026 — lát cắt 2).
/// 🔒 Chuyển **nguyên văn** từ `eCommerceDbContext.ConfigureCommerce` ⇒ migration **RỖNG**.
/// </summary>
public static class SalesDbContextModelCreatingExtensions
{
    public static void ConfigureSales(this ModelBuilder builder)
    {        builder.Entity<Orders.Order>(b =>
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

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 **`SAL-003` — 4 TRỤC TRẠNG THÁI.** `Status` nay là **giá trị SUY RA**; 3 cột dưới đây là
            // **nguồn sự thật** (cột thứ tư là trục tài chính — `PaymentStatus` đã có, giữ nguyên tên).
            // 📌 Index theo `(TenantId, <trục>, CreationTime)` phục vụ ĐÚNG câu hỏi vận hành:
            //    • `FulfillmentStatus` ⇒ "đơn cần đóng gói/giao"
            //    • `PaymentStatus`     ⇒ "đơn chưa thu tiền" (ca COD — lý do tồn tại của 4 trục)
            //    • `ReconciliationStatus` ⇒ job đối soát quét đơn chờ
            //    • `ReturnStatus`      ⇒ màn hình RMA (`SAL-006`)
            // ══════════════════════════════════════════════════════════════════════════════════════════
            b.HasIndex(x => new { x.TenantId, x.FulfillmentStatus, x.CreationTime })
                .HasDatabaseName("IX_OrdOrders_TenantId_FulfillmentStatus_CreationTime");

            b.HasIndex(x => new { x.TenantId, x.PaymentStatus, x.CreationTime })
                .HasDatabaseName("IX_OrdOrders_TenantId_PaymentStatus_CreationTime");

            b.HasIndex(x => new { x.TenantId, x.ReconciliationStatus, x.CreationTime })
                .HasDatabaseName("IX_OrdOrders_TenantId_ReconciliationStatus_CreationTime");

            b.HasIndex(x => new { x.TenantId, x.ReturnStatus, x.CreationTime })
                .HasDatabaseName("IX_OrdOrders_TenantId_ReturnStatus_CreationTime");

            // 🚨 Bộ đếm version **không được âm** — âm thì mọi so sánh `VersionAfter` vô nghĩa và
            // lịch sử không còn dùng được để phát hiện ghi trùng.
            b.ToTable(tb => tb.HasCheckConstraint(
                "CK_OrdOrders_StatusVersions_KhongAm",
                "\"FulfillmentVersion\" >= 0 AND \"PaymentVersion\" >= 0 " +
                "AND \"ReconciliationVersion\" >= 0 AND \"ClosureVersion\" >= 0 " +
                "AND \"RowVersion\" >= 0"));

            b.HasMany(x => x.StatusHistory)
                .WithOne()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 **OPTIMISTIC CONCURRENCY (D5/BR-010, 24/10/2026) — `RowVersion` do APP quản lý.**
            //
            // 🔑 **VÌ SAO KHÔNG DÙNG `xid`/`xmin`:** Npgsql 10 **không còn** helper `UseXminAsConcurrencyToken`;
            // khai shadow property `xmin` khiến migration sinh **cột thật** tên `xmin` (đụng cột hệ thống).
            // 📌 Cột `bigint` do entity tăng trong `Chuyen` là **tất định**, **kiểm được**, và EF dùng **giá trị
            // GỐC** trong `WHERE` ⇒ hai request cùng sửa một đơn thì request sau nhận `DbUpdateConcurrencyException`
            // (app service map `eCommerce:Order:0002`). Ghi ở `sal-003` **L8**.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            b.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // 🚨 **NHẬT KÝ TRẠNG THÁI (`OrdStatusHistory`) — APPEND-ONLY.**
        //
        // 🔒 UNIQUE `(TenantId, OrderId, Axis, VersionAfter)` là **hàng rào CUỐI** cho bất biến
        // *"mỗi phiên bản của một trục chỉ có đúng một lịch sử"*: nếu tầng ứng dụng sai (race, retry),
        // DB **chặn** — không tạo lịch sử trùng. ⚠️ Index này **BẮT BUỘC** `NULLS NOT DISTINCT`
        // (TenantId NULL ở chế độ 1 tenant) ⇒ viết SQL thô trong migration, y như 53 index trước.
        //
        // 📌 CHECK `FromStatus <> ToStatus`: "chuyển dịch" mà không đổi giá trị không phải chuyển dịch —
        // cho phép nó là mở đường cho lịch sử rác và làm `VersionAfter` nhảy vô nghĩa.
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<Orders.OrderStatusHistory>(b =>
        {
            b.ToTable("OrdStatusHistory");
            b.ConfigureByConvention();

            b.Property(x => x.Axis).IsRequired().HasMaxLength(24);
            b.Property(x => x.ActorType).IsRequired().HasMaxLength(24);
            b.Property(x => x.ReasonCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.ReasonText).HasMaxLength(512);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);

            b.HasIndex(x => new { x.TenantId, x.OrderId, x.OccurredAtUtc })
                .HasDatabaseName("IX_OrdStatusHistory_TenantId_OrderId_OccurredAtUtc");

            b.HasIndex(x => new { x.TenantId, x.Axis, x.OccurredAtUtc })
                .HasDatabaseName("IX_OrdStatusHistory_TenantId_Axis_OccurredAtUtc");

            b.HasIndex(x => new { x.TenantId, x.ActorType, x.OccurredAtUtc })
                .HasDatabaseName("IX_OrdStatusHistory_TenantId_ActorType_OccurredAtUtc");

            b.HasIndex(x => new { x.TenantId, x.OrderId, x.Axis, x.VersionAfter })
                .IsUnique()
                .HasDatabaseName("IX_OrdStatusHistory_TenantId_OrderId_Axis_VersionAfter");

            b.ToTable(tb => tb.HasCheckConstraint(
                "CK_OrdStatusHistory_From_Khac_To",
                "\"FromStatus\" <> \"ToStatus\""));
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

            // 🆕 WP-03 nửa sau — `ProductId` (sản phẩm theo mô hình Nhanh). Truy vấn nóng: *"doanh số theo
            // sản phẩm"* (báo cáo `WP-08`) mà không phải join chuỗi SKU. ⚠️ KHÔNG unique: một sản phẩm nằm
            // trên nhiều dòng đơn là chuyện bình thường.
            b.HasIndex(x => new { x.TenantId, x.ProductId })
                .HasDatabaseName("IX_OrdOrderLines_TenantId_ProductId");

            // 🆕 `S14` — **HÀNG RÀO CUỐI chống trả vượt số đã mua** (`SAL-006` ADR-11).
            // 📌 Kiểm ở domain (`OrderLine.TangTra`) là lớp **chính**; `CHECK` này chặn đường ghi tắt
            // (SQL tay / job) — thiếu nó thì `ReturnedQuantity` có thể vượt `Quantity` mà **không lỗi nào**.
            b.ToTable(t => t.HasCheckConstraint(
                "CK_OrdOrderLines_ReturnedQuantity_HopLe",
                "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\""));
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // 🆕 `S14` — PHIẾU TRẢ HÀNG (`SAL-006`): `OrdReturns` · `OrdReturnLines` · `OrdReturnHistory`
        // ⚠️ **`OrdRefunds` CHƯA có** — `Q4` = *"chỉ ghi nhận"*: chưa có đường gọi cổng tự động.
        // 📌 Vì vậy `RefundedAmount`/`RefundMethod` là **bản ghi tay** có chứng từ, không phải kết quả cổng.
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<Orders.OrderReturn>(b =>
        {
            b.ToTable("OrdReturns", t =>
            {
                t.HasCheckConstraint("CK_OrdReturns_RefundedAmount_KhongAm", "\"RefundedAmount\" >= 0");
                t.HasCheckConstraint("CK_OrdReturns_NhanHang_CoMoc", "\"Status\" <> 4 OR \"ReceivedAtUtc\" IS NOT NULL");
                t.HasCheckConstraint("CK_OrdReturns_Dong_CoMoc", "\"Status\" <> 5 OR \"ClosedAtUtc\" IS NOT NULL");
            });
            b.ConfigureByConvention();

            b.Property(x => x.ReturnNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.ReasonCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.ReasonText).HasMaxLength(512);
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.GoodsStatus).HasConversion<int>();
            b.Property(x => x.RefundMethod).HasConversion<int>();
            b.Property(x => x.RefundedAmount).HasPrecision(18, 4);
            b.Property(x => x.RefundCurrency).IsRequired().HasMaxLength(8);
            b.Property(x => x.RequestedByActorType).IsRequired().HasMaxLength(24);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
            b.Ignore(x => x.ConMo);

            // `SAL-006` §5.1 — **hai** unique NND; migration sửa tay.
            b.HasIndex(x => new { x.TenantId, x.ReturnNumber })
                .IsUnique()
                .HasDatabaseName("IX_OrdReturns_TenantId_ReturnNumber");
            b.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("IX_OrdReturns_TenantId_IdempotencyKey");

            b.HasIndex(x => new { x.TenantId, x.OrderId, x.CreationTime })
                .HasDatabaseName("IX_OrdReturns_TenantId_OrderId_CreationTime");
            b.HasIndex(x => new { x.TenantId, x.Status, x.GoodsStatus })
                .HasDatabaseName("IX_OrdReturns_TenantId_Status_GoodsStatus");
        });

        builder.Entity<Orders.OrderReturnLine>(b =>
        {
            b.ToTable("OrdReturnLines", t =>
            {
                t.HasCheckConstraint("CK_OrdReturnLines_SoLuong_HopLe", "\"Quantity\" > 0");
                t.HasCheckConstraint(
                    "CK_OrdReturnLines_Nhan_HopLe",
                    "\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"Quantity\"");
                t.HasCheckConstraint(
                    "CK_OrdReturnLines_Disposition_ChiSauKhiNhan",
                    "\"Disposition\" = 0 OR \"ReceivedQuantity\" > 0");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.Disposition).HasConversion<int>();
            b.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 4);
            b.Property(x => x.DiscountAmountSnapshot).HasPrecision(18, 4);
            b.Property(x => x.TaxAmountSnapshot).HasPrecision(18, 4);
            b.Property(x => x.RefundAmount).HasPrecision(18, 4);

            // Một dòng đơn chỉ xuất hiện **MỘT** lần trong một phiếu (chống trả trùng cùng dòng).
            b.HasIndex(x => new { x.TenantId, x.ReturnId, x.OrderLineId })
                .IsUnique()
                .HasDatabaseName("IX_OrdReturnLines_TenantId_ReturnId_OrderLineId");

            b.HasOne<Orders.OrderReturn>()
                .WithMany()
                .HasForeignKey(x => x.ReturnId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Orders.OrderReturnHistory>(b =>
        {
            b.ToTable("OrdReturnHistory", t =>
            {
                t.HasCheckConstraint("CK_OrdReturnHistory_PhaiDoiTrangThai", "\"FromStatus\" <> \"ToStatus\"");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Axis).IsRequired().HasMaxLength(24);
            b.Property(x => x.ActorType).IsRequired().HasMaxLength(24);
            b.Property(x => x.ReasonCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.ReasonText).HasMaxLength(512);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);

            b.HasIndex(x => new { x.TenantId, x.ReturnId, x.OccurredAtUtc })
                .HasDatabaseName("IX_OrdReturnHistory_TenantId_ReturnId_OccurredAtUtc");

            // 🚨 *mỗi phiên bản của một trục chỉ có ĐÚNG MỘT dòng lịch sử* (`sal-003` cùng khuôn).
            b.HasIndex(x => new { x.TenantId, x.ReturnId, x.Axis, x.VersionAfter })
                .IsUnique()
                .HasDatabaseName("IX_OrdReturnHistory_TenantId_ReturnId_Axis_VersionAfter");

            b.HasOne<Orders.OrderReturn>()
                .WithMany()
                .HasForeignKey(x => x.ReturnId)
                .OnDelete(DeleteBehavior.Restrict);
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

            // 🚨 `SAL-005` **BR-SAL-005-009 / D12** — **INDEX CHO CỜ "NGHI NGỜ THIẾU BẢN TRUNG GIAN"**.
            //
            // **VÌ SAO BẮT BUỘC (đây là ĐIỀU KIỆN CHẠY của job đối soát, KHÔNG phải tối ưu):**
            // job đối soát hằng ngày quét **đúng** tập `GapSuspected = true` để tự đi lấy lại đơn từ sàn
            // (`D12`: *"job đối soát chủ động là BẮT BUỘC, không phải tuỳ chọn"* — vì sàn **không** đảm bảo
            // giao webhook). Bảng intake **chỉ tăng** (mỗi đơn sàn một dòng, append-only theo thiết kế):
            // không có index thì mỗi nhịp job quét **toàn bộ** bảng.
            // 📌 Hệ quả nếu thiếu: job chậm dần ⇒ tới một lúc **vượt cửa sổ chạy** ⇒ **không hoàn thành**
            // ⇒ cờ `GapSuspected` **tồn đọng** ⇒ đơn sai trạng thái **không ai phát hiện** — mà **không có
            // lỗi nào** để lần ra (job chỉ *"chạy lâu hơn"*).
            b.HasIndex(x => new { x.TenantId, x.GapSuspected })
                .HasDatabaseName("IX_SalMktIntakes_TenantId_GapSuspected");
        });

        // ---- Nhật ký SỰ KIỆN đơn sàn (`SAL-005` D7) — APPEND-ONLY ----
        //
        // 📌 `SalMarketplaceOrderIntakes` = *"đơn này đang ở đâu"* (MỘT dòng/đơn).
        //    Bảng này = *"sàn gửi gì, lúc nào, ta đã làm gì"* (MỘT dòng/SỰ KIỆN) — hai câu hỏi KHÁC nhau.
        // 🔒 Không FK tới `OrdOrders`/`SalMarketplaceOrderIntakes`: nhật ký phải sống được kể cả khi đơn/intake
        //    chưa tồn tại (sự kiện `Failed` đầu tiên) — FK ở đây sẽ chặn đúng lúc cần dấu vết nhất.
        builder.Entity<Orders.MarketplaceEventLog>(b =>
        {
            b.ToTable("SalMarketplaceEventLogs", t =>
            {
                // 📌 `SourceVersion` = epoch giây của `update_time` sàn ⇒ **luôn dương** khi CÓ.
                //    `NULL` được phép (sự kiện thiếu watermark — `Outcome = ThieuWatermark`), nhưng `0`/âm
                //    là dấu hiệu **map sai** (ví dụ map `PlacedAtUtc` hoặc số dòng vào watermark).
                t.HasCheckConstraint(
                    "CK_SalMktEventLogs_SourceVersion",
                    "\"SourceVersion\" IS NULL OR \"SourceVersion\" > 0");

                // 📌 Bắt buộc là SHA-256 hex **chữ thường** — cùng chuẩn với `SalMarketplaceOrderIntakes`.
                //    Hash HOA/base64 vẫn "trông như hash" nhưng làm nhánh C/D (trùng/xung đột) **so sai**.
                t.HasCheckConstraint(
                    "CK_SalMktEventLogs_PayloadHash_Hex",
                    "\"PayloadHash\" ~ '^[0-9a-f]{64}$'");
            });

            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiChannelCode);
            b.Property(x => x.ExternalOrderId).IsRequired().HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiExternalOrderId);
            b.Property(x => x.PayloadHash).IsRequired().HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiPayloadHash);
            b.Property(x => x.EventKind).HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiEventKind);
            b.Property(x => x.MarketplaceStatusRaw).HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiTrangThaiTho);
            b.Property(x => x.FailureCode).HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiFailureCode);
            b.Property(x => x.FailureMessage).HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiFailureMessage);
            b.Property(x => x.CorrelationId).HasMaxLength(global::eCommerce.Orders.MarketplaceEventLog.DoDaiCorrelationId);

            b.Property(x => x.Outcome).HasConversion<int>();

            // 🚨 **HÀNG RÀO CHỐNG GHI HAI DÒNG CHO CÙNG MỘT KẾT CỤC CỦA CÙNG MỘT SỰ KIỆN.**
            // 📌 Đây là câu trả lời cho `XC-003` D2 (*"bỏ lock đi, DB có chặn không?"*) ⇒ **CÓ**.
            //
            // 🚨 **VÌ SAO KHOÁ CÓ CẢ `Outcome` (phát hiện ĐO ĐƯỢC ở `S6` — `sal-005` §20.5 #1):**
            // bản đầu của index chỉ có 5 cột `(TenantId, ChannelCode, ExternalOrderId, SourceVersion, PayloadHash)`
            // ⇒ một sự kiện **giao lại** (cùng phiên bản + cùng hash) trùng **toàn bộ** khoá với dòng đã có
            // ⇒ dòng `Duplicate` **KHÔNG BAO GIỜ** ghi được (INSERT vi phạm index ⇒ writer coi là trùng ⇒ bỏ).
            // 🔑 Hệ quả: `Outcome = Duplicate` là **giá trị CHẾT**, và câu hỏi vận hành *"sàn có gửi lại không?"*
            // **không trả lời được** — đúng thứ `D7` sinh ra để trả lời. 📌 Thêm `Outcome` vào khoá ⇒ ghi được
            // **một** dòng đánh dấu *"lần giao lại đầu tiên"*, mà vẫn chặn ca *"cùng kết cục ghi hai lần do
            // bug/hai luồng"* (mục đích thật của hàng rào).
            //
            // ⚠️ Tên đặt TƯỜNG MINH vì tên EF tự sinh (94+ ký tự) vượt giới hạn **63** của PostgreSQL ⇒ bị **cắt**
            // ⇒ test đối chiếu **đích danh tên index** không tìm thấy (bẫy đã gặp với bảng intake).
            // ⚠️ `NULLS NOT DISTINCT` phải bổ sung bằng **SQL thô** trong migration (EF không khai báo được),
            // nếu không thì với `TenantId = NULL` (đang chạy 1 tenant) index **mất tác dụng im lặng**.
            b.HasIndex(x => new
                {
                    x.TenantId,
                    x.ChannelCode,
                    x.ExternalOrderId,
                    x.SourceVersion,
                    x.PayloadHash,
                    x.Outcome
                })
                .IsUnique()
                .HasDatabaseName("IX_SalMktEventLogs_TenantId_Channel_Order_Ver_Hash_Outcome");

            // 📌 Truy vấn NÓNG NHẤT của Ops: *"lịch sử nhận của MỘT đơn sàn"* (mới nhất trước).
            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.ExternalOrderId, x.ReceivedAtUtc })
                .HasDatabaseName("IX_SalMktEventLogs_TenantId_Channel_Order_Received");

            // 📌 Dashboard vận hành: tỉ lệ `Late`/`Conflict` theo thời gian — *"hệ thống có đang nhận sự kiện
            //    lộn xộn không"*. 🔑 Không có index này thì bảng **chỉ tăng** (append-only) làm câu đó chậm dần
            //    ⇒ tới lúc **không ai xem** — đúng cái chết của mọi bảng nhật ký.
            b.HasIndex(x => new { x.TenantId, x.Outcome, x.ReceivedAtUtc })
                .HasDatabaseName("IX_SalMktEventLogs_TenantId_Outcome_Received");
        });
    }
}