using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp04_Inventory_Buckets_Mode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CounterpartyId",
                table: "InvStockDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyType",
                table: "InvStockDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                table: "InvStockDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "InvStockDocuments",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "InvStockDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "InvStockDocuments",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefId",
                table: "InvStockDocuments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefType",
                table: "InvStockDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "InvStockDocumentLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImeiSerials",
                table: "InvStockDocumentLines",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "InvStockDocumentLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                table: "InvStockDocumentLines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Damaged",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Holding",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreOrder",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Shipping",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Transferring",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WaitingInbound",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Warranty",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyHolding",
                table: "InvItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_InvStockDocuments_TenantId_Huong_Status_OccurredAtUtc",
                table: "InvStockDocuments",
                columns: new[] { "TenantId", "Direction", "Status", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockDocuments_TenantId_IdempotencyKey",
                table: "InvStockDocuments",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            // 🚨 **`WP-04` — `NULLS NOT DISTINCT` (viết TAY, EF KHÔNG sinh được).**
            // 📌 Với `TenantId = NULL` (đúng tình trạng 1 tenant hiện nay), PostgreSQL coi hai `NULL` là **KHÁC
            // nhau** ⇒ unique index **MẤT TÁC DỤNG** ⇒ hai phiếu cùng `IdempotencyKey` **LỌT** ⇒ nhập kho hai lần
            // (tồn tăng gấp đôi) mà **không có lỗi nào** — đúng lỗi C4/F1 mà `PostgreSqlSchemaTests` sinh ra để chặn.
            // 📌 Đúng mẫu đã dùng ở `Inv001_Fix_SoCai_Unique_ThemMovementType` và migration 51/52 của WP-03.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_InvStockDocuments_TenantId_IdempotencyKey\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockDocuments_TenantId_IdempotencyKey\" "
                + "ON \"InvStockDocuments\" (\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT "
                + "WHERE \"IdempotencyKey\" IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvStockDocuments_Direction_HopLe",
                table: "InvStockDocuments",
                sql: "\"Direction\" IS NULL OR \"Direction\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvStockDocuments_Mode_HopLe",
                table: "InvStockDocuments",
                sql: "\"Mode\" IS NULL OR \"Mode\" IN (1, 2, 3, 4, 5, 6, 8, 10, 13, 14, 15, 16, 17, 18, 19)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvStockDocuments_Tien_KhongAm",
                table: "InvStockDocuments",
                sql: "(\"DiscountAmount\" IS NULL OR \"DiscountAmount\" >= 0) AND (\"PaidAmount\" IS NULL OR \"PaidAmount\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvStockDocumentLines_VatPercent_HopLe",
                table: "InvStockDocumentLines",
                sql: "\"VatPercent\" IS NULL OR (\"VatPercent\" >= 0 AND \"VatPercent\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvItems_Chieu_KhongAm",
                table: "InvItems",
                sql: "\"Transferring\" >= 0 AND \"Shipping\" >= 0 AND \"Holding\" >= 0 AND \"Damaged\" >= 0 AND \"Warranty\" >= 0 AND \"WarrantyHolding\" >= 0 AND \"PreOrder\" >= 0 AND \"WaitingInbound\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvStockDocuments_TenantId_Huong_Status_OccurredAtUtc",
                table: "InvStockDocuments");

            migrationBuilder.DropIndex(
                name: "IX_InvStockDocuments_TenantId_IdempotencyKey",
                table: "InvStockDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvStockDocuments_Direction_HopLe",
                table: "InvStockDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvStockDocuments_Mode_HopLe",
                table: "InvStockDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvStockDocuments_Tien_KhongAm",
                table: "InvStockDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvStockDocumentLines_VatPercent_HopLe",
                table: "InvStockDocumentLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvItems_Chieu_KhongAm",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "CounterpartyId",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "CounterpartyType",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "RefId",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "RefType",
                table: "InvStockDocuments");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "InvStockDocumentLines");

            migrationBuilder.DropColumn(
                name: "ImeiSerials",
                table: "InvStockDocumentLines");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "InvStockDocumentLines");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                table: "InvStockDocumentLines");

            migrationBuilder.DropColumn(
                name: "Damaged",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "Holding",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "PreOrder",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "Shipping",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "Transferring",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "WaitingInbound",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "InvItems");

            migrationBuilder.DropColumn(
                name: "WarrantyHolding",
                table: "InvItems");
        }
    }
}
