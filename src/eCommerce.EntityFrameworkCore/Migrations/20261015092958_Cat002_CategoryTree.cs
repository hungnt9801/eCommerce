using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cat002_CategoryTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxVariantCombinations = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_CatCategories", x => x.Id);
                    table.CheckConstraint("CK_CatCategories_Level_Khoang", "\"Level\" >= 0 AND \"Level\" <= 5");
                    table.CheckConstraint("CK_CatCategories_Path_DinhDang", "\"Path\" LIKE '/%/'");
                    table.ForeignKey(
                        name: "FK_CatCategories_CatCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CatCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatCategories_ParentId",
                table: "CatCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CatCategories_TenantId_Code",
                table: "CatCategories",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatCategories_TenantId_ParentId",
                table: "CatCategories",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatCategories_TenantId_Path",
                table: "CatCategories",
                columns: new[] { "TenantId", "Path" });

            // 🚨🚨 **SỬA TAY — BẮT BUỘC, ĐỌC TRƯỚC KHI SỬA LẠI.**
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`. PostgreSQL coi **NULL là
            // DISTINCT** ⇒ khi `TenantId` là `NULL` (ĐÚNG tình trạng hiện nay: hệ thống chạy **1 tenant**,
            // `AbpTenantManagement` chưa cài — `XC-004` §5.2) thì **hai danh mục cùng `Code` đều LỌT VÀO DB**.
            // ⇒ Unique index **mất tác dụng đúng ở môi trường đang chạy**, và **KHÔNG có lỗi nào**.
            // Đây là họ lỗi C4/F1 đã gặp thật; `XC-004` **BR-009** yêu cầu `NULLS NOT DISTINCT` cho MỌI
            // unique index có `TenantId`. Test `F1_du_..._index_deu_dung_NULLS_NOT_DISTINCT` canh việc này.
            migrationBuilder.Sql("DROP INDEX \"IX_CatCategories_TenantId_Code\";");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatCategories_TenantId_Code\" ON \"CatCategories\" (\"TenantId\", \"Code\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatCategories");
        }
    }
}
