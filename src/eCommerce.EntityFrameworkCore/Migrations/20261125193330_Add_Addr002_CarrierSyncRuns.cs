using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Addr002_CarrierSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddrCarrierSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CarrierCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SoMaNhan = table.Column<int>(type: "integer", nullable: false),
                    SoCapNhatTen = table.Column<int>(type: "integer", nullable: false),
                    SoDanhDauNghiNgo = table.Column<int>(type: "integer", nullable: false),
                    SoCanNguoiAnhXa = table.Column<int>(type: "integer", nullable: false),
                    MauCanNguoiAnhXa = table.Column<string[]>(type: "text[]", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddrCarrierSyncRuns", x => x.Id);
                    table.CheckConstraint("CK_AddrCarrierSyncRuns_Outcome", "\"Outcome\" in (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "AddrCarrierSyncRunUnmappedCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CarrierCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CarrierLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CarrierAddressCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CarrierAddressName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddrCarrierSyncRunUnmappedCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AddrCarrierSyncRunUnmappedCodes_AddrCarrierSyncRuns_SyncRun~",
                        column: x => x.SyncRunId,
                        principalTable: "AddrCarrierSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddrCarrierSyncRuns_Tenant_Carrier_Outcome_FinishedAt",
                table: "AddrCarrierSyncRuns",
                columns: new[] { "TenantId", "CarrierCode", "Outcome", "FinishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AddrCarrierSyncRuns_Tenant_Carrier_StartedAt",
                table: "AddrCarrierSyncRuns",
                columns: new[] { "TenantId", "CarrierCode", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AddrCarrierSyncRunUnmappedCodes_Sync_Level_Code",
                table: "AddrCarrierSyncRunUnmappedCodes",
                columns: new[] { "SyncRunId", "CarrierLevel", "CarrierAddressCode" });

            // 🔒 `D-88`/`D-92`: một mã một dòng con trong một lượt. `TenantId` NULLABLE (host luôn NULL) ⇒
            // EF **không** diễn đạt được `NULLS NOT DISTINCT` (PostgreSQL coi NULL là KHÁC NHAU ⇒ ràng buộc
            // mất tác dụng IM LẶNG) ⇒ SQL thô, khuôn `Fix_NullableTenantId_UniqueIndexes`.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UX_AddrCarrierSyncRunUnmappedCodes_Tenant_SyncRun_Code\" " +
                "ON \"AddrCarrierSyncRunUnmappedCodes\" (\"TenantId\", \"SyncRunId\", \"CarrierAddressCode\") " +
                "NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddrCarrierSyncRunUnmappedCodes");

            migrationBuilder.DropTable(
                name: "AddrCarrierSyncRuns");
        }
    }
}
