using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Brand_CatBrands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CatBrands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatProducts_BrandId",
                table: "CatProducts",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_CatBrands_TenantId_Code",
                table: "CatBrands",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            // 🚨 `NULLS NOT DISTINCT` (PostgreSQL 15+): EF KHÔNG khai báo được. Thiếu nó thì
            // `TenantId = NULL` làm unique index mất tác dụng **im lặng**.
            migrationBuilder.DropIndex(name: "IX_CatBrands_TenantId_Code", table: "CatBrands");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_CatBrands_TenantId_Code""
ON ""CatBrands"" (""TenantId"", ""Code"") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_CatBrands_TenantId_HoatDong_ThuTu",
                table: "CatBrands",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CatBrands_TenantId_Slug",
                table: "CatBrands",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.DropIndex(name: "IX_CatBrands_TenantId_Slug", table: "CatBrands");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_CatBrands_TenantId_Slug""
ON ""CatBrands"" (""TenantId"", ""Slug"") NULLS NOT DISTINCT;");

            migrationBuilder.AddForeignKey(
                name: "FK_CatProducts_CatBrands_BrandId",
                table: "CatProducts",
                column: "BrandId",
                principalTable: "CatBrands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatProducts_CatBrands_BrandId",
                table: "CatProducts");

            migrationBuilder.DropTable(
                name: "CatBrands");

            migrationBuilder.DropIndex(
                name: "IX_CatProducts_BrandId",
                table: "CatProducts");
        }
    }
}