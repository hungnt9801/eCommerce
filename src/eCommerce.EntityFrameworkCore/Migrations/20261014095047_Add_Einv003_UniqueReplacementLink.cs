using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Einv003_UniqueReplacementLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_EInvInvoices_TenantId_HoaDonBiDieuChinhId\" ON \"EInvInvoices\" (\"TenantId\", \"HoaDonBiDieuChinhId\") NULLS NOT DISTINCT WHERE \"HoaDonBiDieuChinhId\" IS NOT NULL AND NOT \"IsDeleted\";");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_EInvInvoices_TenantId_HoaDonBiThayTheId\" ON \"EInvInvoices\" (\"TenantId\", \"HoaDonBiThayTheId\") NULLS NOT DISTINCT WHERE \"HoaDonBiThayTheId\" IS NOT NULL AND NOT \"IsDeleted\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EInvInvoices_TenantId_HoaDonBiDieuChinhId",
                table: "EInvInvoices");

            migrationBuilder.DropIndex(
                name: "IX_EInvInvoices_TenantId_HoaDonBiThayTheId",
                table: "EInvInvoices");
        }
    }
}
