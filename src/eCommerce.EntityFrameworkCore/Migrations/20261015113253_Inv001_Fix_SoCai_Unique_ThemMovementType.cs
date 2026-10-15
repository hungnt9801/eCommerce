using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Inv001_Fix_SoCai_Unique_ThemMovementType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvStockLedger_TenantId_ReferenceLineId",
                table: "InvStockLedger");

            migrationBuilder.CreateIndex(
                name: "IX_InvStockLedger_TenantId_ReferenceLineId",
                table: "InvStockLedger",
                columns: new[] { "TenantId", "ReferenceLineId", "MovementType" },
                unique: true,
                filter: "\"ReferenceLineId\" IS NOT NULL");

            // 🚨 **SỬA TAY — BẮT BUỘC**: EF không sinh được `NULLS NOT DISTINCT` **kèm** bộ lọc `WHERE`.
            // Thiếu nó thì khi `TenantId = NULL` (hệ thống **1 tenant**) PostgreSQL coi NULL là DISTINCT
            // ⇒ hàng rào chống **ghi sổ hai lần** **MẤT TÁC DỤNG** ⇒ **tồn kho tăng gấp đôi** mà không có lỗi nào.
            migrationBuilder.Sql("DROP INDEX \"IX_InvStockLedger_TenantId_ReferenceLineId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockLedger_TenantId_ReferenceLineId\" ON \"InvStockLedger\" " +
                "(\"TenantId\", \"ReferenceLineId\", \"MovementType\") NULLS NOT DISTINCT " +
                "WHERE \"ReferenceLineId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvStockLedger_TenantId_ReferenceLineId",
                table: "InvStockLedger");

            migrationBuilder.CreateIndex(
                name: "IX_InvStockLedger_TenantId_ReferenceLineId",
                table: "InvStockLedger",
                columns: new[] { "TenantId", "ReferenceLineId" },
                unique: true,
                filter: "\"ReferenceLineId\" IS NOT NULL");
        }
    }
}
