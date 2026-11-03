using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp03_Catalog_Backfill_OrderLineProductId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "OrdOrderLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatVariantMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_CatVariantMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatVariantMaps_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrderLines_TenantId_ProductId",
                table: "OrdOrderLines",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatVariantMaps_ProductId",
                table: "CatVariantMaps",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatVariantMaps_TenantId_ProductId",
                table: "CatVariantMaps",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatVariantMaps_TenantId_Sku",
                table: "CatVariantMaps",
                columns: new[] { "TenantId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_CatVariantMaps_TenantId_VariantId",
                table: "CatVariantMaps",
                columns: new[] { "TenantId", "VariantId" },
                unique: true);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT` CHO UNIQUE INDEX CỦA BẢNG ĐỐI CHIẾU** (WP-03 nửa sau).
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống đang chạy **1 tenant** ⇒ `TenantId` **có thể `NULL`**; PostgreSQL
            // mặc định coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **MẤT TÁC DỤNG IM LẶNG** (lỗi C4/F1 đã trả giá,
            // `XC-004` Q6, `PostgreSqlSchemaTests.F1`). EF Core **không** sinh được mệnh đề này ⇒ viết SQL thô
            // (cùng cách migration 51 và `Cat003_ProductSlug_Unique`).
            //
            // ⚠️ Hệ quả nếu THIẾU: backfill chạy **hai lần** (hoặc chạy lại sau lỗi giữa đường) tạo **hai sản phẩm
            // con** cho cùng một biến thể ⇒ danh mục có hai SKU giống nhau, POS quét ra hai món, và bảng đối chiếu
            // trả lời sai câu *"biến thể này đã chuyển chưa"* — **không có lỗi nào nổi lên**.
            //
            // 🔒 Cách làm: xoá index EF vừa tạo → tạo lại **kèm `NULLS NOT DISTINCT`**, **giữ nguyên tên** để
            // `PostgreSqlSchemaTests` đối chiếu đích danh vẫn tìm thấy và `Down()` chỉ cần `DropTable`.
            migrationBuilder.Sql("DROP INDEX \"IX_CatVariantMaps_TenantId_VariantId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatVariantMaps_TenantId_VariantId\" ON \"CatVariantMaps\" " +
                "(\"TenantId\", \"VariantId\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatVariantMaps");

            migrationBuilder.DropIndex(
                name: "IX_OrdOrderLines_TenantId_ProductId",
                table: "OrdOrderLines");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "OrdOrderLines");
        }
    }
}
