using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Addr001_CatalogSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddrCatalogSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SoThemMoi = table.Column<int>(type: "integer", nullable: false),
                    SoDoiTen = table.Column<int>(type: "integer", nullable: false),
                    SoMoLai = table.Column<int>(type: "integer", nullable: false),
                    SoDong = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddrCatalogSyncRuns", x => x.Id);
                    table.CheckConstraint("CK_AddrCatalogSyncRuns_Outcome", "\"Outcome\" in (0, 1, 2)");
                    table.CheckConstraint("CK_AddrCatalogSyncRuns_SchemaVersion", "\"SchemaVersion\" in (1, 2)");
                    table.CheckConstraint("CK_AddrCatalogSyncRuns_SourceHashHex", "\"SourceHash\" IS NULL OR \"SourceHash\" ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddrCatalogSyncRuns_TenantId_SchemaVersion_ImportedAtUtc",
                table: "AddrCatalogSyncRuns",
                columns: new[] { "TenantId", "SchemaVersion", "ImportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AddrCatalogSyncRuns_TenantId_SchemaVersion_Outcome_ImportedAtUtc",
                table: "AddrCatalogSyncRuns",
                columns: new[] { "TenantId", "SchemaVersion", "Outcome", "ImportedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddrCatalogSyncRuns");
        }
    }
}
