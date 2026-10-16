using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Sal005_Watermark_Intake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedPayloadHash",
                table: "SalMarketplaceOrderIntakes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppliedSourceUpdatedAtUtc",
                table: "SalMarketplaceOrderIntakes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AppliedSourceVersion",
                table: "SalMarketplaceOrderIntakes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GapSuspected",
                table: "SalMarketplaceOrderIntakes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReconciledAtUtc",
                table: "SalMarketplaceOrderIntakes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalMktIntakes_TenantId_GapSuspected",
                table: "SalMarketplaceOrderIntakes",
                columns: new[] { "TenantId", "GapSuspected" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalMktIntakes_TenantId_GapSuspected",
                table: "SalMarketplaceOrderIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedPayloadHash",
                table: "SalMarketplaceOrderIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedSourceUpdatedAtUtc",
                table: "SalMarketplaceOrderIntakes");

            migrationBuilder.DropColumn(
                name: "AppliedSourceVersion",
                table: "SalMarketplaceOrderIntakes");

            migrationBuilder.DropColumn(
                name: "GapSuspected",
                table: "SalMarketplaceOrderIntakes");

            migrationBuilder.DropColumn(
                name: "LastReconciledAtUtc",
                table: "SalMarketplaceOrderIntakes");
        }
    }
}
