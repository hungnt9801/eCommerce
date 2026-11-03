using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp05_DonHang_Nhanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarrierCode",
                table: "OrdOrders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CodAmount",
                table: "OrdOrders",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PackerId",
                table: "OrdOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReasonCode",
                table: "OrdOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SaleStaffId",
                table: "OrdOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFeeActual",
                table: "OrdOrders",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFeeCustomer",
                table: "OrdOrders",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "OrdOrders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "OrdOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "OrdOrders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "OrdOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeightGram",
                table: "OrdOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_TrackingNumber",
                table: "OrdOrders",
                columns: new[] { "TenantId", "TrackingNumber" },
                filter: "\"TrackingNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_WarehouseId_CreationTime",
                table: "OrdOrders",
                columns: new[] { "TenantId", "WarehouseId", "CreationTime" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrdOrders_TienVaKhoiLuong_KhongAm",
                table: "OrdOrders",
                sql: "(\"CodAmount\" IS NULL OR \"CodAmount\" >= 0) AND (\"ShippingFeeCustomer\" IS NULL OR \"ShippingFeeCustomer\" >= 0) AND (\"ShippingFeeActual\" IS NULL OR \"ShippingFeeActual\" >= 0) AND (\"WeightGram\" IS NULL OR \"WeightGram\" >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_TrackingNumber",
                table: "OrdOrders");

            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_WarehouseId_CreationTime",
                table: "OrdOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrdOrders_TienVaKhoiLuong_KhongAm",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "CarrierCode",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "CodAmount",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "PackerId",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "SaleStaffId",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ShippingFeeActual",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ShippingFeeCustomer",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "WeightGram",
                table: "OrdOrders");
        }
    }
}
