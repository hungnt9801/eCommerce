using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Inv002_StockReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvStockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_InvStockReservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockReservations_TenantId_Ref_Sku_Kho",
                table: "InvStockReservations",
                columns: new[] { "TenantId", "ReferenceId", "Sku", "WarehouseId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvStockReservations_TenantId_Status_ExpiresAtUtc",
                table: "InvStockReservations",
                columns: new[] { "TenantId", "Status", "ExpiresAtUtc" });

            // 🚨 **SỬA TAY — BẮT BUỘC**: EF không sinh được `NULLS NOT DISTINCT` **kèm** bộ lọc `WHERE`.
            // Thiếu nó thì khi `TenantId = NULL` (hệ thống **1 tenant**) PostgreSQL coi NULL là DISTINCT
            // ⇒ index **MẤT TÁC DỤNG** ⇒ **cùng một giỏ/đơn giữ chỗ HAI LẦN cho cùng SKU** (retry, double-click)
            // ⇒ `Reserved` **tăng gấp đôi** ⇒ `Available` **thấp giả** ⇒ **hết hàng oan**.
            migrationBuilder.Sql("DROP INDEX \"IX_InvStockReservations_TenantId_Ref_Sku_Kho\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockReservations_TenantId_Ref_Sku_Kho\" ON \"InvStockReservations\" " +
                "(\"TenantId\", \"ReferenceId\", \"Sku\", \"WarehouseId\") NULLS NOT DISTINCT " +
                "WHERE \"ReferenceId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvStockReservations");
        }
    }
}
