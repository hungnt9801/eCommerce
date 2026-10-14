using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_EInvoice_CqtRetryCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LanGuiCuoiUtc",
                table: "EInvInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoLanGuiCQT",
                table: "EInvInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LanGuiCuoiUtc",
                table: "EInvInvoices");

            migrationBuilder.DropColumn(
                name: "SoLanGuiCQT",
                table: "EInvInvoices");
        }
    }
}
