using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Reconciliation.EntityFrameworkCore;

/// <summary>
/// **MAPPING EF CỦA MODULE `Reconciliation`** (`P0 #1`, 24/10/2026).
/// 🔒 Chuyển **nguyên văn** từ `eCommerceDbContext.ConfigureCommerce` ⇒ migration **RỖNG**.
/// </summary>
public static class ReconciliationDbContextModelCreatingExtensions
{
    public static void ConfigureReconciliation(this ModelBuilder builder)
    {        builder.Entity<Inventory.ReconRun>(b =>
        {
            b.ToTable("ReconRuns");
            b.ConfigureByConvention();

            b.Property(x => x.DonViNguong).HasConversion<int>();
            b.Property(x => x.HanhDong).HasConversion<int>();
            b.Property(x => x.KetLuan).HasConversion<int>();
            b.Property(x => x.GiaTriNguong).HasPrecision(18, 4);

            // 🆕 `S7` — **XÁC NHẬN LỆCH** (`Q5` = A): ghi ai/khi nào/vì sao rồi ĐÓNG cảnh báo.
            // 🔒 KHÔNG sửa `SoLech*` khi xác nhận (xem chú thích `ReconRun.XacNhan`).
            b.Property(x => x.ConfirmNote).HasMaxLength(1024);

            // 📌 KHÔNG unique index: **cố ý** — hai lượt quét trong cùng một giây là **hợp lệ**
            // (job chạy `RunOnStart` ngay sau khởi động + nhịp quét đến hạn cùng lúc; hoặc API gọi tay).
            // Đặt unique ở đây sẽ **chặn oan** một lượt quét thật ⇒ mất lịch sử, đúng thứ bảng này chống.

            // Truy vấn vận hành hay dùng: *"các lượt quét gần đây của tenant"* và *"lượt nào có vượt ngưỡng"*.
            b.HasIndex(x => new { x.TenantId, x.BatDauUtc })
                .HasDatabaseName("IX_ReconRuns_TenantId_BatDauUtc");

            b.HasIndex(x => new { x.TenantId, x.CoVuotNguong, x.BatDauUtc })
                .HasDatabaseName("IX_ReconRuns_TenantId_CoVuotNguong_BatDauUtc");

            b.ToTable(t =>
            {
                // 🔒 Bất biến "số VƯỢT NGƯỠNG không lớn hơn tổng số LỆCH" — chặn ở DB vì đây là số liệu
                // người vận hành **dùng để quyết định**; một dòng vô lý làm hỏng cả báo cáo xu hướng.
                t.HasCheckConstraint("CK_ReconRuns_SoLechVuot_KhongVuotTong",
                    "\"SoLechVuotNguong\" >= 0 AND \"SoLech\" >= 0 AND \"SoLechVuotNguong\" <= \"SoLech\"");
                t.HasCheckConstraint("CK_ReconRuns_SoCapDaDo_KhongAm", "\"SoCapDaDo\" >= 0");
                t.HasCheckConstraint("CK_ReconRuns_MocThoiGian_HopLe", "\"KetThucUtc\" >= \"BatDauUtc\"");

                // 🆕 `S7` — **KHÔNG THỂ "XÁC NHẬN MỘT NỬA"**: hoặc cả ba trường có, hoặc cả ba `NULL`.
                // 📌 Vì sao chặn ở DB: một dòng `ConfirmedAtUtc` có mà `ConfirmedByUserId` rỗng là
                // **bằng chứng vô chủ** (ai đóng cảnh báo? không trả lời được) — và nó chỉ lọt vào qua
                // đường ghi tắt (SQL/hàng đợi import), tức đúng chỗ tầng domain không đi qua.
                t.HasCheckConstraint("CK_ReconRuns_XacNhan_DuBo",
                    "(\"ConfirmedAtUtc\" IS NULL) = (\"ConfirmedByUserId\" IS NULL) AND " +
                    "(\"ConfirmedAtUtc\" IS NULL OR \"ConfirmNote\" IS NOT NULL)");
            });
        });

        builder.Entity<Inventory.ReconRunMismatch>(b =>
        {
            b.ToTable("ReconRunMismatches");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // 🆕 `WP-04b` — lịch sử lệch phải giữ **cùng độ chính xác** với `InvItems`/`InvStockLedger`
            // (`numeric(18,3)`): nếu để `int` thì lệch 0,5 bị **ghi thành 0** ⇒ câu hỏi *"lệch này bao nhiêu"*
            // trả lời **sai** ngay trong bảng dùng để điều tra.
            b.Property(x => x.OnHand).HasPrecision(18, 3);
            b.Property(x => x.TongSoCai).HasPrecision(18, 3);
            b.Property(x => x.ChenhLech).HasPrecision(18, 3);

            // 🚨 `REC-001` — **TRẠNG THÁI XỬ LÝ LỆCH** (bổ sung 16/10/2026).
            //
            // **CẨN THẬN KHI SỬA KHỐI NÀY:** `b.Property(x => x.Sku).IsRequired().HasMaxLength(64);`
            // xuất hiện ở **7 entity** trong tệp này. Lần đầu tôi sửa bằng `-replace` (khớp dòng **ĐẦU TIÊN**)
            // ⇒ chèn nhầm vào `ProductVariant`/`InventoryItem` ⇒ **36 lỗi build**. 📌 Đúng cái bẫy đã ghi ở
            // bàn giao (round 4): *"`^\| <MÃ> \|` khớp dòng đầu tiên, không phải dòng bạn đang nghĩ tới"*.
            // 🔒 Sửa đúng cách: định vị **theo entity cha** (`builder.Entity<Inventory.ReconRunMismatch>`), không theo dòng.
            //
            // 📌 Trước lượt này bảng là **append-only, không trạng thái** ⇒ một dòng đã xử lý xong **trông
            // y hệt** một dòng chưa ai đụng tới ⇒ người trực phải đọc lại **toàn bộ** danh sách mỗi nhịp job,
            // và tới lúc danh sách dài thì **không ai đọc nữa**.
            b.Property(x => x.NguoiXuLy).HasMaxLength(256);
            b.Property(x => x.LyDoXuLy).HasMaxLength(1024);

            // 📌 Index **có điều kiện** cho truy vấn nóng nhất: *"lượt này còn dòng nào CHƯA xử lý?"*.
            // 🔒 Bộ lọc `WHERE NOT "DaXuLy"` giữ index **nhỏ**: sau vài tháng, **phần lớn** dòng đã xử lý
            // ⇒ index chỉ còn chứa **việc còn lại**. Không lọc thì index phình theo **toàn bộ lịch sử**
            // và chính truy vấn *"còn việc gì"* trở thành truy vấn nặng nhất — nghịch lý của báo cáo tồn đọng.
            b.HasIndex(x => new { x.TenantId, x.ReconRunId })
                .HasFilter("NOT \"DaXuLy\"")
                .HasDatabaseName("IX_ReconRunMismatches_TenantId_Run_ChuaXuLy");

            // Một cặp `(kho, SKU)` chỉ xuất hiện **MỘT** lần trong một lượt quét ⇒ index này chống ghi trùng
            // khi job retry / hai luồng cùng ghi. Có `TenantId` ⇒ **BẮT BUỘC** `NULLS NOT DISTINCT` (SQL thô trong migration).
            b.HasIndex(x => new { x.TenantId, x.ReconRunId, x.WarehouseId, x.Sku })
                .IsUnique()
                .HasDatabaseName("IX_ReconRunMismatches_TenantId_Run_Kho_Sku");

            // 🆕 `S7` — index cho truy vấn **RETENTION** (`Recon.Retention.Days`):
            // *"các dòng lệch ĐÃ XỬ LÝ và cũ hơn mốc"*. 📌 Không unique ⇒ **không** ảnh hưởng con số
            // `NULLS NOT DISTINCT` (53). Nếu không có index này thì mỗi lượt dọn là **quét cả bảng** —
            // và nó chạy hằng ngày, trên đúng cái bảng đang phình (lý do retention tồn tại).
            b.HasIndex(x => new { x.TenantId, x.DaXuLy, x.CreationTime })
                .HasDatabaseName("IX_ReconRunMismatches_TenantId_DaXuLy_CreationTime");

            // 📌 FK `Restrict` (không `Cascade`): lịch sử đối soát là **bằng chứng**; xoá cứng một lượt
            // **không** được phép kéo mất các dòng lệch của nó một cách âm thầm.
            b.HasOne<Inventory.ReconRun>()
                .WithMany()
                .HasForeignKey(x => x.ReconRunId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        // ══════════════════════════════════════════════════════════════════════════════════════════
        // 🆕 `REC-001` — ĐỐI SOÁT THANH TOÁN (27/10/2026): 3 bảng lịch sử lượt/dòng/lệch.
        // ══════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<eCommerce.Reconciliation.PaymentReconciliationRun>(b =>
        {
            b.ToTable("RecPaymentReconciliationRuns");
            b.ConfigureByConvention();

            b.Property(x => x.FailureCode).HasMaxLength(64);
            b.Property(x => x.CorrelationId).HasMaxLength(128);

            // Truy vấn Ops: lượt theo trạng thái trong khoảng thời gian.
            b.HasIndex(x => new { x.TenantId, x.Status, x.StartedAtUtc })
                .HasDatabaseName("IX_RecPayRuns_TenantId_Status_Started");
            b.HasIndex(x => new { x.TenantId, x.PeriodFromUtc })
                .HasDatabaseName("IX_RecPayRuns_TenantId_PeriodFrom");
        });

        builder.Entity<eCommerce.Reconciliation.PaymentReconciliationLine>(b =>
        {
            b.ToTable("RecPaymentReconciliationLines");
            b.ConfigureByConvention();

            b.Property(x => x.ProviderCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.MerchantReference).IsRequired().HasMaxLength(64);
            b.Property(x => x.RequestId).IsRequired().HasMaxLength(32);
            b.Property(x => x.SkipReason).HasMaxLength(64);
            b.Property(x => x.ProviderResponseCode).HasMaxLength(8);
            b.Property(x => x.ProviderTransactionStatus).HasMaxLength(8);
            b.Property(x => x.ProviderTransactionNo).HasMaxLength(32);
            b.Property(x => x.ProviderPayDateGmt7).HasMaxLength(14).IsFixedLength();
            b.Property(x => x.ProviderTransactionType).HasMaxLength(8);
            b.Property(x => x.FailureCode).HasMaxLength(64);
            b.Property(x => x.FailureMessage).HasMaxLength(1024);
            b.Property(x => x.CorrelationId).HasMaxLength(128);

            // 🔒 BR-05: mỗi giao dịch có ĐÚNG MỘT dòng trong một lượt (NND bổ sung ở migration).
            b.HasIndex(x => new { x.TenantId, x.RunId, x.PaymentTransactionId })
                .IsUnique()
                .HasDatabaseName("IX_RecPayLines_TenantId_Run_GiaoDich");

            b.HasIndex(x => new { x.TenantId, x.RunId, x.Status })
                .HasDatabaseName("IX_RecPayLines_TenantId_Run_Status");

            b.HasOne<eCommerce.Reconciliation.PaymentReconciliationRun>()
                .WithMany()
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<eCommerce.Reconciliation.PaymentDiscrepancy>(b =>
        {
            b.ToTable("RecPaymentDiscrepancies");
            b.ConfigureByConvention();

            b.Property(x => x.ProviderCode).IsRequired().HasMaxLength(32);
            b.Property(x => x.MerchantReference).IsRequired().HasMaxLength(64);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(x => x.ResolutionNote).HasMaxLength(2048);
            b.Property(x => x.IgnoreReason).HasMaxLength(64);
            b.Property(x => x.CorrelationId).HasMaxLength(128);

            // Hàng đợi Ops: lệch chưa xử lý, mức cao trước.
            b.HasIndex(x => new { x.TenantId, x.Status, x.Severity })
                .HasDatabaseName("IX_RecPayDiscrepancies_TenantId_Status_Severity");
            b.HasIndex(x => new { x.TenantId, x.RunId })
                .HasDatabaseName("IX_RecPayDiscrepancies_TenantId_Run");

            b.HasOne<eCommerce.Reconciliation.PaymentReconciliationRun>()
                .WithMany()
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });    }
}