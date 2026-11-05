using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Gd1S13_StockAllotment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelCode",
                table: "InvStockReservations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvStockAllotmentLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockAllotmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityDelta = table.Column<int>(type: "integer", nullable: false),
                    AllottedAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_InvStockAllotmentLedgers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvStockAllotments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AllottedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FrozenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FrozenByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_InvStockAllotments", x => x.Id);
                    table.CheckConstraint("CK_InvStockAllotments_DongBang_CoDauVet", "\"Status\" <> 1 OR (\"FrozenAtUtc\" IS NOT NULL AND \"FrozenByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CK_InvStockAllotments_Nha_CoLyDo", "\"Status\" <> 2 OR \"ReleaseReason\" IS NOT NULL");
                    table.CheckConstraint("CK_InvStockAllotments_Reserved_KhongVuotAllotted", "\"ReservedQuantity\" <= \"AllottedQuantity\"");
                    table.CheckConstraint("CK_InvStockAllotments_SoLuong_KhongAm", "\"AllottedQuantity\" >= 0 AND \"ReservedQuantity\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockAllotmentLedgers_TenantId_IdempotencyKey",
                table: "InvStockAllotmentLedgers",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvStockAllotmentLedgers_TenantId_Kenh_OccurredAtUtc",
                table: "InvStockAllotmentLedgers",
                columns: new[] { "TenantId", "ChannelCode", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockAllotmentLedgers_TenantId_Kho_Sku_OccurredAtUtc",
                table: "InvStockAllotmentLedgers",
                columns: new[] { "TenantId", "WarehouseId", "Sku", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockAllotments_TenantId_Kenh_Status",
                table: "InvStockAllotments",
                columns: new[] { "TenantId", "ChannelCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockAllotments_TenantId_Kho_Sku",
                table: "InvStockAllotments",
                columns: new[] { "TenantId", "WarehouseId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockAllotments_TenantId_Kho_Sku_Kenh",
                table: "InvStockAllotments",
                columns: new[] { "TenantId", "WarehouseId", "Sku", "ChannelCode" },
                unique: true);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`INV-003` §5.3 + `XC-004` BR-009).
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`. PostgreSQL coi **NULL là DISTINCT**
            // ⇒ khi `TenantId = NULL` (hiện chạy **1 tenant**) cả hai index dưới đây **mất tác dụng IM LẶNG**:
            //   • `InvStockAllotments`: hai ngân sách cho **cùng (kho, SKU, kênh)** đều lọt ⇒ câu hỏi *"kênh này
            //     còn bán được bao nhiêu"* có **hai đáp án** ⇒ ATP **không tất định**, và 📌 **không lỗi nào**;
            //   • `InvStockAllotmentLedgers`: cùng **khoá idempotency** ghi được **hai** dòng ⇒ gọi lại một biên vào
            //     (import/retry) **nhân đôi ngân sách** — đúng lý do `XC-002` BR-005 tồn tại.
            migrationBuilder.Sql("DROP INDEX \"IX_InvStockAllotments_TenantId_Kho_Sku_Kenh\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockAllotments_TenantId_Kho_Sku_Kenh\" ON \"InvStockAllotments\" " +
                "(\"TenantId\", \"WarehouseId\", \"Sku\", \"ChannelCode\") NULLS NOT DISTINCT;");

            // ⚠️ Index **có bộ lọc** (`IdempotencyKey IS NOT NULL`): bút toán do hệ thống/job sinh **không** có
            // khoá; nếu unique không lọc thì toàn hệ thống chỉ được phép có **MỘT** dòng không khoá.
            migrationBuilder.Sql("DROP INDEX \"IX_InvStockAllotmentLedgers_TenantId_IdempotencyKey\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockAllotmentLedgers_TenantId_IdempotencyKey\" ON " +
                "\"InvStockAllotmentLedgers\" (\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT " +
                "WHERE \"IdempotencyKey\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvStockAllotmentLedgers");

            migrationBuilder.DropTable(
                name: "InvStockAllotments");

            migrationBuilder.DropColumn(
                name: "ChannelCode",
                table: "InvStockReservations");
        }
    }
}
