using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class S7_XacNhanVaRetentionDoiSoat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmNote",
                table: "ReconRuns",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAtUtc",
                table: "ReconRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmedByUserId",
                table: "ReconRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReconRuns_XacNhan_DuBo",
                table: "ReconRuns",
                sql: "(\"ConfirmedAtUtc\" IS NULL) = (\"ConfirmedByUserId\" IS NULL) AND (\"ConfirmedAtUtc\" IS NULL OR \"ConfirmNote\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ReconRunMismatches_TenantId_DaXuLy_CreationTime",
                table: "ReconRunMismatches",
                columns: new[] { "TenantId", "DaXuLy", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReconRuns_XacNhan_DuBo",
                table: "ReconRuns");

            migrationBuilder.DropIndex(
                name: "IX_ReconRunMismatches_TenantId_DaXuLy_CreationTime",
                table: "ReconRunMismatches");

            migrationBuilder.DropColumn(
                name: "ConfirmNote",
                table: "ReconRuns");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "ReconRuns");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "ReconRuns");
        }
    }
}
