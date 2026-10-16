using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Xc002_DeadLetterQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MsgDeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenSuKien = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    KhoaNghiepVu = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LyDo = table.Column<int>(type: "integer", nullable: false),
                    MoTaLyDo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Loi = table.Column<string>(type: "text", nullable: true),
                    VaoDlqLucUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DaXuLy = table.Column<bool>(type: "boolean", nullable: false),
                    XuLyLucUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NguoiXuLy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LyDoXuLy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_MsgDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MsgDeadLetters_TenantId_ChuaXuLy",
                table: "MsgDeadLetters",
                columns: new[] { "TenantId", "VaoDlqLucUtc" },
                filter: "NOT \"DaXuLy\"");

            migrationBuilder.CreateIndex(
                name: "IX_MsgDeadLetters_TenantId_Khoa",
                table: "MsgDeadLetters",
                columns: new[] { "TenantId", "KhoaNghiepVu" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MsgDeadLetters");
        }
    }
}
