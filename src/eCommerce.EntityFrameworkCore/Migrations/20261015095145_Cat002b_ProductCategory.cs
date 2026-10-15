using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cat002b_ProductCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatProductCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CatProductCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductCategories_CatCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CatCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatProductCategories_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatProdCats_TenantId_CategoryId",
                table: "CatProductCategories",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatProdCats_TenantId_ProductId_CategoryId",
                table: "CatProductCategories",
                columns: new[] { "TenantId", "ProductId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProdCats_TenantId_ProductId_ChiMotChinh",
                table: "CatProductCategories",
                columns: new[] { "TenantId", "ProductId" },
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductCategories_CategoryId",
                table: "CatProductCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductCategories_ProductId",
                table: "CatProductCategories",
                column: "ProductId");

            // 🚨🚨 **SỬA TAY — BẮT BUỘC, ĐỌC TRƯỚC KHI SỬA LẠI.**
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`. PostgreSQL coi **NULL là
            // DISTINCT** ⇒ khi `TenantId` là `NULL` (ĐÚNG tình trạng hiện nay — hệ thống **1 tenant**,
            // `AbpTenantManagement` chưa cài) thì **cả hai** unique index dưới đây **MẤT TÁC DỤNG**:
            //   • gán trùng (sản phẩm, danh mục) **lọt**,
            //   • và tệ hơn: **HAI danh mục chính** cho cùng sản phẩm **lọt** ⇒ hai breadcrumb/URL SEO
            //     cho một sản phẩm, **không có lỗi nào**.
            // `XC-004` **BR-009** yêu cầu `NULLS NOT DISTINCT` cho MỌI unique index có `TenantId`.
            // 📌 PostgreSQL 15+ cho phép `NULLS NOT DISTINCT` **kết hợp** với `WHERE` (index một phần).
            migrationBuilder.Sql("DROP INDEX \"IX_CatProdCats_TenantId_ProductId_CategoryId\";");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdCats_TenantId_ProductId_CategoryId\" ON \"CatProductCategories\" " +
                "(\"TenantId\", \"ProductId\", \"CategoryId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdCats_TenantId_ProductId_ChiMotChinh\";");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdCats_TenantId_ProductId_ChiMotChinh\" ON \"CatProductCategories\" " +
                "(\"TenantId\", \"ProductId\") NULLS NOT DISTINCT WHERE \"IsPrimary\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatProductCategories");
        }
    }
}
