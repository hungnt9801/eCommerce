using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp08_BaoCao_Reporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RptAggDailySales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Nguon = table.Column<int>(type: "integer", nullable: false),
                    DocumentCount = table.Column<int>(type: "integer", nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LineAmountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DocumentDiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AggregatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptAggDailySales", x => x.Id);
                    table.CheckConstraint("CK_RptAggDailySales_Nguon_HopLe", "\"Nguon\" IN (0, 1)");
                    table.CheckConstraint("CK_RptAggDailySales_Tien_KhongAm", "\"LineAmountTotal\" >= 0 AND \"DocumentDiscountTotal\" >= 0 AND \"Revenue\" >= 0 AND \"VatTotal\" >= 0 AND \"Quantity\" >= 0 AND \"DocumentCount\" >= 0 AND \"LineCount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RptDimDates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsWeekend = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptDimDates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RptDimProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptDimProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RptDimStaffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptDimStaffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RptDimStores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptDimStores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RptFactInventorySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OnHand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Reserved = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Transferring = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Shipping = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Holding = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Damaged = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Warranty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    WarrantyHolding = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PreOrder = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    WaitingInbound = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    InWarehouse = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Available = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptFactInventorySnapshots", x => x.Id);
                    table.CheckConstraint("CK_RptFactInventorySnapshots_Chieu_KhongAm", "\"OnHand\" >= 0 AND \"Reserved\" >= 0 AND \"Transferring\" >= 0 AND \"Shipping\" >= 0 AND \"Holding\" >= 0 AND \"Damaged\" >= 0 AND \"Warranty\" >= 0 AND \"WarrantyHolding\" >= 0 AND \"PreOrder\" >= 0 AND \"WaitingInbound\" >= 0 AND \"Available\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RptFactSalesLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Nguon = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptFactSalesLines", x => x.Id);
                    table.CheckConstraint("CK_RptFactSalesLines_Nguon_HopLe", "\"Nguon\" IN (0, 1)");
                    table.CheckConstraint("CK_RptFactSalesLines_SoLuong_KhongAm", "\"Quantity\" >= 0");
                    table.CheckConstraint("CK_RptFactSalesLines_Tien_KhongAm", "\"UnitPrice\" >= 0 AND \"LineDiscount\" >= 0 AND \"LineAmount\" >= 0 AND \"VatAmount\" >= 0 AND (\"VatPercent\" IS NULL OR \"VatPercent\" >= 0)");
                });

            migrationBuilder.CreateTable(
                name: "RptWatermarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nguon = table.Column<int>(type: "integer", nullable: false),
                    LastAggregatedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRowCount = table.Column<long>(type: "bigint", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RptWatermarks", x => x.Id);
                    table.CheckConstraint("CK_RptWatermarks_SoDong_KhongAm", "\"LastRowCount\" >= 0");
                });

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 **`NULLS NOT DISTINCT` — VIẾT TAY, EF KHÔNG SINH ĐƯỢC** (`WP-08`, `XC-003` F1).
            //
            // 📌 PostgreSQL coi hai `NULL` là **KHÁC nhau** ⇒ với `TenantId = NULL` (đang chạy MỘT doanh
            // nghiệp) unique index **mất tác dụng IM LẶNG**. Với read-model báo cáo, hậu quả **không** phải
            // một lỗi nhỏ: job chạy lại (hoặc hai lượt chồng nhau) sẽ ghi **hai dòng** cho cùng một dòng
            // chứng từ ⇒ **doanh thu nhân đôi** đúng những dòng đó và **không có lỗi nào** để lần ra — đúng
            // ca mà DoD `WP-08` (*"job chạy lại 2 lần không nhân đôi"*) sinh ra để chặn.
            //
            // 🔒 **TÁM index dưới đây đều phải có NND:**
            //   • `RptFactSalesLines (TenantId, Nguon, SourceLineId)` — chống nhân đôi doanh thu (grain = dòng nguồn);
            //   • `RptAggDailySales (TenantId, BusinessDate, Nguon)` — chống nhân đôi ngày;
            //   • `RptFactInventorySnapshots (TenantId, SnapshotDate, WarehouseId, Sku)` — một ảnh / (ngày, kho, SKU);
            //   • `RptWatermarks (TenantId, Nguon)` — một mốc / nguồn (hai mốc ⇒ job chạy lại từ đầu mỗi lượt);
            //   • 4 chiều (`DimDates`, `DimProducts`, `DimStores`, `DimStaffs`) — một dòng / khoá tự nhiên.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptFactSalesLines_TenantId_Nguon_DongNguon\" "
                + "ON \"RptFactSalesLines\" (\"TenantId\", \"Nguon\", \"SourceLineId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptAggDailySales_TenantId_Ngay_Nguon\" "
                + "ON \"RptAggDailySales\" (\"TenantId\", \"BusinessDate\", \"Nguon\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptFactInventorySnapshots_TenantId_Ngay_Kho_Sku\" "
                + "ON \"RptFactInventorySnapshots\" (\"TenantId\", \"SnapshotDate\", \"WarehouseId\", \"Sku\") "
                + "NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptWatermarks_TenantId_Nguon\" "
                + "ON \"RptWatermarks\" (\"TenantId\", \"Nguon\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptDimDates_TenantId_Ngay\" "
                + "ON \"RptDimDates\" (\"TenantId\", \"BusinessDate\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptDimProducts_TenantId_Sku\" "
                + "ON \"RptDimProducts\" (\"TenantId\", \"Sku\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptDimStores_TenantId_Kho\" "
                + "ON \"RptDimStores\" (\"TenantId\", \"WarehouseId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RptDimStaffs_TenantId_NhanVien\" "
                + "ON \"RptDimStaffs\" (\"TenantId\", \"StaffId\") NULLS NOT DISTINCT;");

            // 📌 Ba index **KHÔNG unique** (truy vấn nóng) do EF sinh bình thường.
            migrationBuilder.CreateIndex(
                name: "IX_RptFactInventorySnapshots_TenantId_Ngay",
                table: "RptFactInventorySnapshots",
                columns: new[] { "TenantId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RptFactSalesLines_TenantId_Ngay_Nguon",
                table: "RptFactSalesLines",
                columns: new[] { "TenantId", "BusinessDate", "Nguon" });

            migrationBuilder.CreateIndex(
                name: "IX_RptFactSalesLines_TenantId_Sku",
                table: "RptFactSalesLines",
                columns: new[] { "TenantId", "Sku" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RptAggDailySales");

            migrationBuilder.DropTable(
                name: "RptDimDates");

            migrationBuilder.DropTable(
                name: "RptDimProducts");

            migrationBuilder.DropTable(
                name: "RptDimStaffs");

            migrationBuilder.DropTable(
                name: "RptDimStores");

            migrationBuilder.DropTable(
                name: "RptFactInventorySnapshots");

            migrationBuilder.DropTable(
                name: "RptFactSalesLines");

            migrationBuilder.DropTable(
                name: "RptWatermarks");
        }
    }
}
