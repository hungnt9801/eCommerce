using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Inventory.EntityFrameworkCore;

/// <summary>
/// **MAPPING EF CỦA MODULE `Inventory`** (`P0 #1`, 24/10/2026 — lát cắt 2).
///
/// 🔒 **CHUYỂN NGUYÊN VĂN** từ `eCommerceDbContext.ConfigureCommerce` — không sửa cấu hình nào, để migration
/// **RỖNG**. 📌 Phần **đối soát kho** (`ReconRun`/`ReconRunMismatch`) **vẫn ở host** — sẽ chuyển sang module
/// `Reconciliation` (bounded context riêng), không gộp vào đây.
/// </summary>
public static class InventoryDbContextModelCreatingExtensions
{
    public static void ConfigureInventory(this ModelBuilder builder)
    {        builder.Entity<Inventory.InventoryItem>(b =>
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

        // ── INV-003: HẠN MỨC PHÂN BỔ **RIÊNG TỪNG KÊNH** ────────────────────────────────────────────
        // 📌 Thay cho "một giá trị dùng chung" trước đây: mỗi kênh một hạn mức, tuỳ chọn **theo kho**.
        // ⚠️ Khoá nghiệp vụ `(TenantId, ChannelCode, WarehouseId)` với `WarehouseId` **NULLABLE**
        // (`null` = hạn mức **cả kênh**) ⇒ unique index **BẮT BUỘC** `NULLS NOT DISTINCT`, mà EF **không**
        // sinh được ⇒ migration có **SQL thô** (đúng mẫu `Inv001_Fix_SoCai_Unique_ThemMovementType`).
        // 🚨 Thiếu `NULLS NOT DISTINCT`: PostgreSQL coi `NULL` là khác nhau ⇒ khai **hai** dòng
        // *"hạn mức cả kênh"* cho cùng kênh **LỌT** ⇒ hai hạn mức cho một kênh, không lỗi nào.
        builder.Entity<Inventory.InvChannelLimit>(b =>
        {
            b.ToTable("InvChannelLimits");
            b.ConfigureByConvention();

            b.Property(x => x.ChannelCode).IsRequired().HasMaxLength(Inventory.InvChannelLimit.DoDaiMaKenhToiDa);

            // Đọc bằng mắt trong DB là **số** — ghi rõ để không ai "sửa tay" sai (0=Block, 1=Warn).
            b.Property(x => x.HanhDong).HasConversion<int>();

            // 🚨 `INV-003` muc 5 viec 2 — **DON VI CUA RIENG DONG NAY** (no muc 16.8 #1 DA DONG).
            // 🔒 **NULLABLE la CO Y**: `null` = **khong override** => dung setting `Inv.ChannelLimit.Unit`.
            // 📌 Nho vay COT MOI **KHONG DOI HANH VI** cua du lieu cu => migration nay AN TOAN.
            // ⚠️ KHONG dung `0`/gia tri "mac dinh" lam dau "khong override": `Percent` = `0` la mot DON VI
            // HOP LE => dung no lam co se tao HAI cach dien dat cung mot y (dung loi ma `GiaTri` da tranh).
            b.Property(x => x.DonVi).HasConversion<int?>();

            b.Property(x => x.GiaTri).HasPrecision(18, 4);

            b.HasIndex(x => new { x.TenantId, x.ChannelCode, x.WarehouseId })
                .IsUnique()
                .HasDatabaseName("IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId");

            // 🔒 `GiaTri >= 0`: âm là **vô nghĩa** (quy ước `0` = TẮT) ⇒ chặn ở DB, không chỉ ở domain
            // (đường ghi tắt bằng SQL/hàng đợi import sẽ không đi qua hàm tạo).
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InvChannelLimits_GiaTri_KhongAm", "\"GiaTri\" >= 0");
            });
        });
    }
}