using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Address_AdministrativeUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddrAdministrativeUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ParentCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LegalBasis = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_AddrAdministrativeUnits", x => x.Id);
                    table.CheckConstraint("CK_AddrAdministrativeUnits_SchemaVersion", "\"SchemaVersion\" in (1, 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddrAdministrativeUnits_SchemaVersion_Level_ParentCode",
                table: "AddrAdministrativeUnits",
                columns: new[] { "SchemaVersion", "Level", "ParentCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AddrAdministrativeUnits_SchemaVersion_Name",
                table: "AddrAdministrativeUnits",
                columns: new[] { "SchemaVersion", "Name" });

            // 🚨 SQL THÔ — BẮT BUỘC (bài học C4/F1, đã chứng minh trên PostgreSQL thật).
            //
            // PostgreSQL coi NULL là **KHÁC NHAU** trong ràng buộc unique ⇒ với `TenantId` NULL, index do
            // EF sinh **KHÔNG chặn được bản ghi trùng** ⇒ danh mục ĐVHC có thể có **hai bản ghi cùng
            // (phiên bản, cấp, mã)**. Hệ quả im lặng: phép tra "lấy đơn vị theo mã" trả về bản ghi
            // **ngẫu nhiên** (tuỳ thứ tự) ⇒ **địa chỉ sai** đi vào vận đơn và hoá đơn.
            //
            // `NULLS NOT DISTINCT` (PostgreSQL 15+) làm NULL được coi là BẰNG NHAU ⇒ ràng buộc có tác dụng.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_AddrAdministrativeUnits_TenantId_SchemaVersion_Level_Code"
                    ON "AddrAdministrativeUnits" ("TenantId", "SchemaVersion", "Level", "Code")
                    NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddrAdministrativeUnits");
        }
    }
}
