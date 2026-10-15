using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Sal001_Order_CheckoutKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutIdempotencyKey",
                table: "OrdOrders",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_CheckoutIdempotencyKey",
                table: "OrdOrders",
                columns: new[] { "TenantId", "CheckoutIdempotencyKey" },
                unique: true,
                filter: "\"CheckoutIdempotencyKey\" IS NOT NULL");

            // 🚨 **SỬA TAY — BẮT BUỘC**: EF không sinh được `NULLS NOT DISTINCT` **kèm** bộ lọc `WHERE`.
            // Thiếu nó thì khi `TenantId = NULL` (hệ thống **1 tenant**) PostgreSQL coi NULL là DISTINCT
            // ⇒ index **MẤT TÁC DỤNG** ⇒ **HAI ĐƠN cho cùng một lần checkout** (khách bấm hai lần / client retry)
            // ⇒ **giao hàng hai lần** hoặc **thu tiền hai lần**.
            migrationBuilder.Sql("DROP INDEX \"IX_OrdOrders_TenantId_CheckoutIdempotencyKey\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_OrdOrders_TenantId_CheckoutIdempotencyKey\" ON \"OrdOrders\" " +
                "(\"TenantId\", \"CheckoutIdempotencyKey\") NULLS NOT DISTINCT " +
                "WHERE \"CheckoutIdempotencyKey\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_CheckoutIdempotencyKey",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "CheckoutIdempotencyKey",
                table: "OrdOrders");
        }
    }
}
