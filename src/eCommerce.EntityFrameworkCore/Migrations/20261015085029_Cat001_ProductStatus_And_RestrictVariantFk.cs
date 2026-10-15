using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <summary>
    /// `CAT-001` — **vòng đời sản phẩm** (`IsPublished` bool → `Status` enum có máy trạng thái) và
    /// **đổi FK biến thể từ `Cascade` sang `Restrict`**.
    ///
    /// 🚨🚨 **MIGRATION NÀY ĐÃ ĐƯỢC SỬA TAY CÓ CHỦ Ý — ĐỌC TRƯỚC KHI SỬA LẠI.**
    ///
    /// EF sinh ra theo thứ tự: `DROP COLUMN IsPublished` → `ADD COLUMN Status DEFAULT 0`.
    /// Nhưng `Status = 0` là **`Draft`** ⇒ **mọi sản phẩm ĐANG BÁN sẽ âm thầm thành Nháp**:
    ///   • mất trạng thái publish (**dữ liệu mất thật**, EF cũng đã cảnh báo *"may result in the loss of data"*),
    ///   • và **không có lỗi nào** — đúng họ lỗi im lặng mà dự án đã trả giá nhiều lần.
    ///
    /// ⇒ Đã **đổi thứ tự**: `ADD COLUMN Status` → **`UPDATE` chuyển dữ liệu** → mới `DROP COLUMN IsPublished`.
    /// `Down()` làm ngược lại tương ứng (để rollback **không** mất trạng thái).
    ///
    /// 📌 Ghi chú: bản ghi `IsPublished = false` (Draft cũ) giữ nguyên `Status = 0` — không cần chuyển.
    /// </summary>
    public partial class Cat001_ProductStatus_And_RestrictVariantFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatProductVariants_CatProducts_ProductId",
                table: "CatProductVariants");

            // (1) Thêm cột TRƯỚC.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CatProducts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // (2) 🚨 CHUYỂN DỮ LIỆU trước khi xoá cột cũ: sản phẩm đang bán giữ nguyên trạng thái bán.
            //     `ProductStatus.Published = 1` (xem `src/eCommerce.Domain.Shared/Enums.cs`).
            //     Dùng SQL thô vì `IsPublished` sắp bị xoá nên không thể truy cập qua model nữa.
            migrationBuilder.Sql(
                "UPDATE \"CatProducts\" SET \"Status\" = 1 WHERE \"IsPublished\" = true;");

            // (3) Giờ mới xoá cột cũ (đã chuyển xong dữ liệu).
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "CatProducts");

            // (4) Hàng rào chống phá lịch sử: xoá cứng `Product` không còn cascade xoá biến thể.
            migrationBuilder.AddForeignKey(
                name: "FK_CatProductVariants_CatProducts_ProductId",
                table: "CatProductVariants",
                column: "ProductId",
                principalTable: "CatProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatProductVariants_CatProducts_ProductId",
                table: "CatProductVariants");

            // (1) Thêm lại cột cũ TRƯỚC.
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "CatProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // (2) 🚨 Chuyển ngược dữ liệu — rollback cũng KHÔNG được làm mất trạng thái đang bán.
            migrationBuilder.Sql(
                "UPDATE \"CatProducts\" SET \"IsPublished\" = true WHERE \"Status\" = 1;");

            // (3) Xoá cột mới.
            migrationBuilder.DropColumn(
                name: "Status",
                table: "CatProducts");

            migrationBuilder.AddForeignKey(
                name: "FK_CatProductVariants_CatProducts_ProductId",
                table: "CatProductVariants",
                column: "ProductId",
                principalTable: "CatProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
