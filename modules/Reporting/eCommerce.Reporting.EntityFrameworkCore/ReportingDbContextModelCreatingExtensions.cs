using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace eCommerce.Reporting.EntityFrameworkCore;

/// <summary>
/// **MAPPING EF CỦA MODULE `Reporting`** (`WP-08`) — 4 bảng chiều + 2 bảng sự kiện + 1 bảng tổng hợp + mốc.
///
/// 🚨 **UNIQUE INDEX Ở ĐÂY LÀ HÀNG RÀO CUỐI CỦA ĐiD (DoD) *"job chạy lại 2 lần không nhân đôi dữ liệu"*:**
/// tầng ứng dụng đã `DELETE` theo `(Ngày, Nguồn)` rồi `INSERT`, nhưng **hai lượt chạy đồng thời** hoặc một
/// đường ghi tắt (SQL tay/backfill viết vội) vẫn có thể chèn trùng ⇒ read-model có **hai dòng cho một dòng
/// chứng từ** ⇒ doanh thu nhân đôi **mà không có lỗi nào**. Index `(TenantId, Nguon, SourceLineId)` là thứ
/// duy nhất chặn được ca đó.
///
/// ⚠️ **EF KHÔNG sinh được `NULLS NOT DISTINCT`** ⇒ migration sửa TAY (bài học `XC-003` F1/C4: thiếu mệnh
/// đề thì `TenantId = NULL` làm index **mất tác dụng im lặng**).
/// 📌 Mỗi `CHECK`/unique index **phải** được ánh xạ trong `ViPhamRangBuocDb` **hoặc** khai báo có lý do ở
/// `Data/RangBuocChuaAnhXa.txt` — nếu không `RangBuocDbChuaAnhXaTests` **ĐỎ**.
/// </summary>
public static class ReportingDbContextModelCreatingExtensions
{
    public static void ConfigureReporting(this ModelBuilder builder)
    {
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // CHIỀU
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RptDimDate>(b =>
        {
            b.ToTable("RptDimDates");
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.TenantId, x.BusinessDate })
                .IsUnique()
                .HasDatabaseName("IX_RptDimDates_TenantId_Ngay");
        });

        builder.Entity<RptDimProduct>(b =>
        {
            b.ToTable("RptDimProducts");
            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.ProductName).HasMaxLength(256);

            b.HasIndex(x => new { x.TenantId, x.Sku })
                .IsUnique()
                .HasDatabaseName("IX_RptDimProducts_TenantId_Sku");
        });

        builder.Entity<RptDimStore>(b =>
        {
            b.ToTable("RptDimStores");
            b.ConfigureByConvention();

            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(256);

            b.HasIndex(x => new { x.TenantId, x.WarehouseId })
                .IsUnique()
                .HasDatabaseName("IX_RptDimStores_TenantId_Kho");
        });

        builder.Entity<RptDimStaff>(b =>
        {
            b.ToTable("RptDimStaffs");
            b.ConfigureByConvention();

            b.Property(x => x.DisplayName).HasMaxLength(256);

            b.HasIndex(x => new { x.TenantId, x.StaffId })
                .IsUnique()
                .HasDatabaseName("IX_RptDimStaffs_TenantId_NhanVien");
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // SỰ KIỆN BÁN HÀNG
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RptFactSalesLine>(b =>
        {
            b.ToTable("RptFactSalesLines", t =>
            {
                // 🚨 Tiền âm = "đã ghi sai dấu". Không có nghiệp vụ nào bán ra số âm ⇒ chặn ở DB.
                t.HasCheckConstraint(
                    "CK_RptFactSalesLines_Tien_KhongAm",
                    "\"UnitPrice\" >= 0 AND \"LineDiscount\" >= 0 AND \"LineAmount\" >= 0 " +
                    "AND \"VatAmount\" >= 0 AND (\"VatPercent\" IS NULL OR \"VatPercent\" >= 0)");

                t.HasCheckConstraint("CK_RptFactSalesLines_SoLuong_KhongAm", "\"Quantity\" >= 0");

                // 📌 Nguồn hợp lệ: 0 (bán lẻ) hoặc 1 (đơn hàng). `2` (tồn kho) **không** phải nguồn doanh thu.
                t.HasCheckConstraint(
                    "CK_RptFactSalesLines_Nguon_HopLe", "\"Nguon\" IN (0, 1)");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Nguon).HasConversion<int>();
            b.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
            b.Property(x => x.ProductName).HasMaxLength(256);

            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.LineDiscount).HasPrecision(18, 2);
            b.Property(x => x.LineAmount).HasPrecision(18, 2);
            b.Property(x => x.VatPercent).HasPrecision(18, 2);
            b.Property(x => x.VatAmount).HasPrecision(18, 2);

            // 🚨 **HÀNG RÀO CHỐNG NHÂN ĐÔI** — xem chú thích lớp.
            b.HasIndex(x => new { x.TenantId, x.Nguon, x.SourceLineId })
                .IsUnique()
                .HasDatabaseName("IX_RptFactSalesLines_TenantId_Nguon_DongNguon");

            // 📌 Truy vấn NÓNG: báo cáo theo SKU / theo kho / theo ngày.
            b.HasIndex(x => new { x.TenantId, x.BusinessDate, x.Nguon })
                .HasDatabaseName("IX_RptFactSalesLines_TenantId_Ngay_Nguon");

            b.HasIndex(x => new { x.TenantId, x.Sku })
                .HasDatabaseName("IX_RptFactSalesLines_TenantId_Sku");
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // ẢNH TỒN KHO
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RptFactInventorySnapshot>(b =>
        {
            b.ToTable("RptFactInventorySnapshots", t =>
            {
                t.HasCheckConstraint(
                    "CK_RptFactInventorySnapshots_Chieu_KhongAm",
                    "\"OnHand\" >= 0 AND \"Reserved\" >= 0 AND \"Transferring\" >= 0 AND \"Shipping\" >= 0 " +
                    "AND \"Holding\" >= 0 AND \"Damaged\" >= 0 AND \"Warranty\" >= 0 AND \"WarrantyHolding\" >= 0 " +
                    "AND \"PreOrder\" >= 0 AND \"WaitingInbound\" >= 0 AND \"Available\" >= 0");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Sku).IsRequired().HasMaxLength(64);

            // 📌 Viết TƯỜNG MINH từng cột (không dùng vòng lặp theo TÊN): `Property<T>(string)` tạo
            // **shadow property** nếu tên không khớp CLR ⇒ cột `decimal` mặc định (18,2) hoặc lỗi lúc chạy,
            // trong khi build/migration vẫn xanh.
            b.Property(x => x.OnHand).HasPrecision(18, 3);
            b.Property(x => x.Reserved).HasPrecision(18, 3);
            b.Property(x => x.Transferring).HasPrecision(18, 3);
            b.Property(x => x.Shipping).HasPrecision(18, 3);
            b.Property(x => x.Holding).HasPrecision(18, 3);
            b.Property(x => x.Damaged).HasPrecision(18, 3);
            b.Property(x => x.Warranty).HasPrecision(18, 3);
            b.Property(x => x.WarrantyHolding).HasPrecision(18, 3);
            b.Property(x => x.PreOrder).HasPrecision(18, 3);
            b.Property(x => x.WaitingInbound).HasPrecision(18, 3);
            b.Property(x => x.InWarehouse).HasPrecision(18, 3);
            b.Property(x => x.Available).HasPrecision(18, 3);

            // 🚨 **MỘT ẢNH / (NGÀY, KHO, SKU)** — chạy lại cùng ngày **thay thế**, không cộng thêm.
            b.HasIndex(x => new { x.TenantId, x.SnapshotDate, x.WarehouseId, x.Sku })
                .IsUnique()
                .HasDatabaseName("IX_RptFactInventorySnapshots_TenantId_Ngay_Kho_Sku");

            b.HasIndex(x => new { x.TenantId, x.SnapshotDate })
                .HasDatabaseName("IX_RptFactInventorySnapshots_TenantId_Ngay");
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // TỔNG HỢP THEO NGÀY
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RptAggDailySales>(b =>
        {
            b.ToTable("RptAggDailySales", t =>
            {
                t.HasCheckConstraint(
                    "CK_RptAggDailySales_Tien_KhongAm",
                    "\"LineAmountTotal\" >= 0 AND \"DocumentDiscountTotal\" >= 0 AND \"Revenue\" >= 0 " +
                    "AND \"VatTotal\" >= 0 AND \"Quantity\" >= 0 AND \"DocumentCount\" >= 0 AND \"LineCount\" >= 0");

                t.HasCheckConstraint("CK_RptAggDailySales_Nguon_HopLe", "\"Nguon\" IN (0, 1)");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Nguon).HasConversion<int>();
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.LineAmountTotal).HasPrecision(18, 2);
            b.Property(x => x.DocumentDiscountTotal).HasPrecision(18, 2);
            b.Property(x => x.Revenue).HasPrecision(18, 2);
            b.Property(x => x.VatTotal).HasPrecision(18, 2);

            // 🚨 **MỘT DÒNG / (NGÀY, NGUỒN)** — chạy lại thay thế.
            b.HasIndex(x => new { x.TenantId, x.BusinessDate, x.Nguon })
                .IsUnique()
                .HasDatabaseName("IX_RptAggDailySales_TenantId_Ngay_Nguon");
        });

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // MỐC TỔNG HỢP
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<RptWatermark>(b =>
        {
            b.ToTable("RptWatermarks", t =>
            {
                t.HasCheckConstraint("CK_RptWatermarks_SoDong_KhongAm", "\"LastRowCount\" >= 0");
            });

            b.ConfigureByConvention();

            b.Property(x => x.Nguon).HasConversion<int>();

            // 🚨 **MỘT MỐC / NGUỒN** — thiếu index này thì hai dòng mốc cho cùng nguồn LỌT ⇒ job đọc dòng nào
            // cũng "đúng", và nó có thể chạy lại từ đầu mỗi lượt (hoặc không bao giờ chạy tiếp).
            b.HasIndex(x => new { x.TenantId, x.Nguon })
                .IsUnique()
                .HasDatabaseName("IX_RptWatermarks_TenantId_Nguon");
        });
    }
}
