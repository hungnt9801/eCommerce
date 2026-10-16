using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Rec001_XuLyLech : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DaXuLy",
                table: "ReconRunMismatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LyDoXuLy",
                table: "ReconRunMismatches",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiXuLy",
                table: "ReconRunMismatches",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "XuLyLucUtc",
                table: "ReconRunMismatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconRunMismatches_TenantId_Run_ChuaXuLy",
                table: "ReconRunMismatches",
                columns: new[] { "TenantId", "ReconRunId" },
                filter: "NOT \"DaXuLy\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReconRunMismatches_TenantId_Run_ChuaXuLy",
                table: "ReconRunMismatches");

            migrationBuilder.DropColumn(
                name: "DaXuLy",
                table: "ReconRunMismatches");

            migrationBuilder.DropColumn(
                name: "LyDoXuLy",
                table: "ReconRunMismatches");

            migrationBuilder.DropColumn(
                name: "NguoiXuLy",
                table: "ReconRunMismatches");

            migrationBuilder.DropColumn(
                name: "XuLyLucUtc",
                table: "ReconRunMismatches");
        }
    }
}
