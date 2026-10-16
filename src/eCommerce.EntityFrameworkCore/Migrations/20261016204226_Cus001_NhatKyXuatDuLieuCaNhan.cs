using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cus001_NhatKyXuatDuLieuCaNhan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CusDataExportLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    NguoiXuat = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    XuatLucUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SoDongHoSo = table.Column<int>(type: "integer", nullable: false),
                    SoDonHang = table.Column<int>(type: "integer", nullable: false),
                    DonHangBiCat = table.Column<bool>(type: "boolean", nullable: false),
                    LyDo = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CusDataExportLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CusDataExportLogs_TenantId_CustomerId",
                table: "CusDataExportLogs",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CusDataExportLogs_TenantId_XuatLucUtc",
                table: "CusDataExportLogs",
                columns: new[] { "TenantId", "XuatLucUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CusDataExportLogs");
        }
    }
}
