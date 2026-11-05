using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Sal003_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrdOrders_StatusVersions_KhongAm",
                table: "OrdOrders");

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "OrdOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrdOrders_StatusVersions_KhongAm",
                table: "OrdOrders",
                sql: "\"FulfillmentVersion\" >= 0 AND \"PaymentVersion\" >= 0 AND \"ReconciliationVersion\" >= 0 AND \"ClosureVersion\" >= 0 AND \"RowVersion\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrdOrders_StatusVersions_KhongAm",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OrdOrders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrdOrders_StatusVersions_KhongAm",
                table: "OrdOrders",
                sql: "\"FulfillmentVersion\" >= 0 AND \"PaymentVersion\" >= 0 AND \"ReconciliationVersion\" >= 0 AND \"ClosureVersion\" >= 0");
        }
    }
}
