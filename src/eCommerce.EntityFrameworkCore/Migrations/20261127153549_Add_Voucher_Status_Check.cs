using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Voucher_Status_Check : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ProVoucherCodes_Status",
                table: "ProVoucherCodes",
                sql: "\"Status\" IN ('Generated','Issued','Revoked')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProPromotionRedemptions_Status",
                table: "ProPromotionRedemptions",
                sql: "\"Status\" BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProVoucherCodes_Status",
                table: "ProVoucherCodes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProPromotionRedemptions_Status",
                table: "ProPromotionRedemptions");
        }
    }
}
