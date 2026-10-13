using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_EInvoice_Invoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EInvInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    KyHieuMauSo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SoHoaDon = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    NgayLap = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TaxAuthorityStatus = table.Column<int>(type: "integer", nullable: false),
                    MaCQT = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LyDo = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    HoaDonBiDieuChinhId = table.Column<Guid>(type: "uuid", nullable: true),
                    HoaDonBiThayTheId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoiCQT = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    XmlDaKy = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_EInvInvoices", x => x.Id);
                    table.CheckConstraint("CK_EInvInvoices_PhaiKyTruocKhiPhatHanh", "\"Status\" = 0 OR \"Status\" = 5 OR \"XmlDaKy\" IS NOT NULL");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EInvInvoices_HoaDonBiDieuChinhId",
                table: "EInvInvoices",
                column: "HoaDonBiDieuChinhId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvInvoices_HoaDonBiThayTheId",
                table: "EInvInvoices",
                column: "HoaDonBiThayTheId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvInvoices_Status_TaxAuthorityStatus",
                table: "EInvInvoices",
                columns: new[] { "Status", "TaxAuthorityStatus" });

            // 🚨 SQL THÔ — BẮT BUỘC (bài học C4/F1). Thiếu `NULLS NOT DISTINCT` thì khi `TenantId` NULL
            // ràng buộc MẤT TÁC DỤNG ⇒ hai hoá đơn CÙNG SỐ lọt vào DB ⇒ vi phạm quy định về số hoá đơn
            // (số phải duy nhất và tăng liên tục). Khoá phân tán là lớp 1; ĐÂY là hàng rào cuối.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_EInvInvoices_TenantId_KyHieuMauSo_SoHoaDon"
                    ON "EInvInvoices" ("TenantId", "KyHieuMauSo", "SoHoaDon")
                    NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EInvInvoices");
        }
    }
}
