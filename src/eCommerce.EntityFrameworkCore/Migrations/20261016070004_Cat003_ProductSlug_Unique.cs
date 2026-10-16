using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cat003_ProductSlug_Unique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CatProducts_TenantId_Slug",
                table: "CatProducts",
                columns: new[] { "TenantId", "Slug" },
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT`** (`CAT-003` ADR-03).
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống chạy **1 tenant** ⇒ `TenantId` **có thể `NULL`**; PostgreSQL
            // mặc định coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **MẤT TÁC DỤNG IM LẶNG** (lỗi C4/F1 đã
            // trả giá — `XC-004` Q6, `PostgreSqlSchemaTests.F1`). EF Core **không** sinh được mệnh đề này
            // **kèm** bộ lọc `WHERE` ⇒ phải viết SQL thô.
            //
            // ⚠️ Hệ quả nếu THIẾU: hai sản phẩm cùng slug (tenant `NULL`) **LỌT** ⇒ **hai sản phẩm tranh một URL
            // công khai**: một trong hai **không bao giờ** truy cập được, và máy tìm kiếm **đánh chỉ mục sai
            // sản phẩm** — không lỗi, không log, không metric.
            //
            // 📌 Bước 1 xoá index EF vừa tạo, bước 2 tạo lại **kèm** `NULLS NOT DISTINCT` — giữ nguyên **tên**
            // index để `PostgreSqlSchemaTests` đối chiếu **đích danh** vẫn tìm thấy, và `Down()` chỉ cần `DropIndex`.
            migrationBuilder.Sql("DROP INDEX \"IX_CatProducts_TenantId_Slug\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProducts_TenantId_Slug\" ON \"CatProducts\" (\"TenantId\", \"Slug\") " +
                "NULLS NOT DISTINCT WHERE \"Slug\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatProducts_TenantId_Slug",
                table: "CatProducts");
        }
    }
}
