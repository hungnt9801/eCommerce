using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Signing_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SigCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SoSerial = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChuSoHuu = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DonViPhatHanh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HieuLucTu = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HieuLucDen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SigCertificates", x => x.Id);
                    table.CheckConstraint("CK_SigCertificates_HieuLuc", "\"HieuLucDen\" > \"HieuLucTu\"");
                });

            migrationBuilder.CreateTable(
                name: "SigSigningRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaTaiLieu = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HashTaiLieu = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ThuatToanBam = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    ChungThuSoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChuKyDaKy = table.Column<string>(type: "text", nullable: true),
                    ThoiDiemKy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NguoiKy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LyDoHuy = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_SigSigningRequests", x => x.Id);
                    table.CheckConstraint("CK_SigSigningRequests_DaKyPhaiCoDuBangChung", "\"TrangThai\" <> 1 OR (\"ChuKyDaKy\" IS NOT NULL AND \"ThoiDiemKy\" IS NOT NULL AND \"NguoiKy\" IS NOT NULL AND \"ChungThuSoId\" IS NOT NULL)");
                });

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_SigCertificates_TenantId_SoSerial\" ON \"SigCertificates\" (\"TenantId\", \"SoSerial\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_SigSigningRequests_TenantId_MaTaiLieu\" ON \"SigSigningRequests\" (\"TenantId\", \"MaTaiLieu\") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_SigSigningRequests_TrangThai",
                table: "SigSigningRequests",
                column: "TrangThai");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SigCertificates");

            migrationBuilder.DropTable(
                name: "SigSigningRequests");
        }
    }
}
