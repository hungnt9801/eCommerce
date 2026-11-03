using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp04c_Tien_PhieuKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "InvStockDocumentLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvStockDocumentLines_UnitPrice_KhongAm",
                table: "InvStockDocumentLines",
                sql: "\"UnitPrice\" IS NULL OR \"UnitPrice\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvStockDocumentLines_UnitPrice_KhongAm",
                table: "InvStockDocumentLines");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "InvStockDocumentLines");
        }
    }
}
