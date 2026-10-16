using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cat003_ProductChannelVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatProductChannelVisibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    LyDo = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_CatProductChannelVisibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductChannelVisibilities_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatProductChannelVisibilities_ProductId",
                table: "CatProductChannelVisibilities",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductChannelVisibilities_TenantId_Kenh_TrangThai",
                table: "CatProductChannelVisibilities",
                columns: new[] { "TenantId", "ChannelCode", "TrangThai" });

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT` CHO
            // `IX_CatProductChannelVisibilities_TenantId_SanPham_Kenh`** (`CAT-003` **ADR-08**).
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống chạy **1 tenant** ⇒ `TenantId` là **`NULL`**. PostgreSQL mặc định
            // coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **MẤT TÁC DỤNG IM LẶNG**: nhiều dòng
            // `(NULL, ProductId, ChannelCode)` **cùng tồn tại** ⇒ **HAI cấu hình hiển thị** cho cùng cặp
            // `(sản phẩm, kênh)` ⇒ mọi truy vấn hiển thị trả **kết quả khác nhau tuỳ truy vấn** (kênh đọc cái
            // này, báo cáo đọc cái kia) — và **không có lỗi nào nổi lên**.
            // 📌 EF Core 10 **không** có tuỳ chọn nào cho mệnh đề này ⇒ phải viết **SQL thô**.
            //
            // 🔒 **Cách làm:** tạo index EF đã sinh ở trên, **XOÁ nó** (`IF EXISTS` — ⚠️ **đã trả giá ở
            // `Cat003_ProductMedia`**: viết `DROP INDEX` trần ⇒ **`42704: index does not exist`** ⇒ migration
            // **thất bại**; `IF EXISTS` là bắt buộc), rồi tạo lại **kèm** `NULLS NOT DISTINCT`.
            // ⚠️ Thứ tự `NULLS NOT DISTINCT` **trước** `WHERE` — ở đây **không** có `WHERE` (ràng buộc áp cho
            // **mọi** dòng), nên chỉ cần mệnh đề đó.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_CatProductChannelVisibilities_TenantId_SanPham_Kenh\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProductChannelVisibilities_TenantId_SanPham_Kenh\" " +
                "ON \"CatProductChannelVisibilities\" (\"TenantId\", \"ProductId\", \"ChannelCode\") " +
                "NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatProductChannelVisibilities");
        }
    }
}
