using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cat003_ProductMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatProductMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    KhoaLuuTru = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DinhDangDaKiem = table.Column<int>(type: "integer", nullable: false),
                    KichThuocByte = table.Column<long>(type: "bigint", nullable: false),
                    Alt = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ThuTu = table.Column<int>(type: "integer", nullable: false),
                    LaAnhChinh = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CatProductMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductMedia_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT` CHO `IX_CatProductMedia_MotAnhChinh`**
            // (`CAT-003` **ADR-12**, lỗi C4/F1 — đã trả giá nhiều lần).
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống chạy **1 tenant** ⇒ `TenantId` là **`NULL`**. PostgreSQL mặc
            // định coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **MẤT TÁC DỤNG IM LẶNG**: nhiều dòng
            // `(NULL, ProductId)` với `LaAnhChinh = true` **cùng tồn tại** ⇒ **sản phẩm có NHIỀU ảnh chính**
            // ⇒ mọi chỗ đọc *"ảnh chính là ảnh nào"* trả **kết quả khác nhau tuỳ truy vấn** (thẻ chia sẻ
            // mạng xã hội dùng một ảnh, danh sách sản phẩm dùng ảnh khác) — và **không có lỗi nào** nổi lên.
            // 📌 EF Core 10 **không** có tuỳ chọn nào cho mệnh đề này ⇒ phải viết **SQL thô**.
            //
            // 🔒 **Cách làm:** tạo index EF đã sinh ở trên, **XOÁ nó**, rồi tạo lại **kèm** `NULLS NOT DISTINCT`
            // — giữ **nguyên tên** index để `PostgreSqlSchemaTests` đối chiếu **đích danh** vẫn tìm thấy,
            // và `Down()` chỉ cần `DropTable`.
            // ⚠️ Thứ tự `NULLS NOT DISTINCT` và `WHERE` **phải đúng như dưới**: mệnh đề `NULLS NOT DISTINCT`
            // đứng **trước** `WHERE` trong cú pháp `CREATE UNIQUE INDEX` của PostgreSQL.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_CatProductMedia_MotAnhChinh\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProductMedia_MotAnhChinh\" ON \"CatProductMedia\" " +
                "(\"TenantId\", \"ProductId\") NULLS NOT DISTINCT WHERE \"LaAnhChinh\";");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductMedia_ProductId",
                table: "CatProductMedia",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductMedia_TenantId_ProductId_ThuTu",
                table: "CatProductMedia",
                columns: new[] { "TenantId", "ProductId", "ThuTu" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatProductMedia");
        }
    }
}
