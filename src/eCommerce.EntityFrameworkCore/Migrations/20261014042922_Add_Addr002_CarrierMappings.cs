using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Addr002_CarrierMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddrCarrierMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CarrierCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CarrierLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CarrierAddressCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CarrierAddressName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OurAdministrativeUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchMethod = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AddrCarrierMappings", x => x.Id);
                    table.CheckConstraint("CK_AddrCarrierMappings_Status", "\"Status\" in (0, 1, 2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddrCarrierMappings_Status_CarrierCode",
                table: "AddrCarrierMappings",
                columns: new[] { "Status", "CarrierCode" });

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_AddrCarrierMappings_TenantId_CarrierCode_CarrierAddressCode\" ON \"AddrCarrierMappings\" (\"TenantId\", \"CarrierCode\", \"CarrierAddressCode\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddrCarrierMappings");
        }
    }
}
