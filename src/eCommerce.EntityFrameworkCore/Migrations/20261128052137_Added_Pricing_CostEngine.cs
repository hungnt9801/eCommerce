using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Added_Pricing_CostEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ComputedAtUtc",
                table: "PrcCostPrices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PrcCostPrices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NguonTinh",
                table: "PrcCostPrices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RunId",
                table: "PrcCostPrices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrcCostEngineRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowsUpserted = table.Column<int>(type: "integer", nullable: false),
                    RowsSkippedApproved = table.Column<int>(type: "integer", nullable: false),
                    RowsFlaggedMissing = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_PrcCostEngineRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrcCostPrices_TenantId_IdempotencyKey",
                table: "PrcCostPrices",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PrcCostEngineRuns_TenantId_RunKey",
                table: "PrcCostEngineRuns",
                columns: new[] { "TenantId", "RunKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcCostEngineRuns_TenantId_StartedAtUtc",
                table: "PrcCostEngineRuns",
                columns: new[] { "TenantId", "StartedAtUtc" });

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 SQL THÔ — `NULLS NOT DISTINCT` cho HAI unique index của `W17-2` (`PRC-001-arch` §4.2).
            // EF Core KHÔNG mô hình hoá `NULLS NOT DISTINCT`; để nguyên index EF sinh thì PostgreSQL coi
            // `NULL` là khác nhau ⇒ khi `TenantId = NULL` (hiện chạy 1 tenant) ràng buộc MẤT TÁC DỤNG IM LẶNG:
            //   • `IX_PrcCostEngineRuns_TenantId_RunKey` ⇒ hai lượt cùng `RunKey` LỌT ⇒ tính lại chồng;
            //   • `IX_PrcCostPrices_TenantId_IdempotencyKey` (filtered) ⇒ hai dòng cùng khoá LỌT ⇒ tạo trùng.
            // Khuôn: `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs` + `20261126224627_Add_Promotion_Module`.
            // ⚠️ Nếu sau này EF sinh LẠI index (đổi model), nó sẽ MẤT thuộc tính NND — phải bổ sung lại.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_PrcCostEngineRuns_TenantId_RunKey";
                CREATE UNIQUE INDEX "IX_PrcCostEngineRuns_TenantId_RunKey"
                    ON "PrcCostEngineRuns" ("TenantId", "RunKey") NULLS NOT DISTINCT;

                DROP INDEX IF EXISTS "IX_PrcCostPrices_TenantId_IdempotencyKey";
                CREATE UNIQUE INDEX "IX_PrcCostPrices_TenantId_IdempotencyKey"
                    ON "PrcCostPrices" ("TenantId", "IdempotencyKey")
                    NULLS NOT DISTINCT
                    WHERE "IdempotencyKey" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrcCostEngineRuns");

            migrationBuilder.DropIndex(
                name: "IX_PrcCostPrices_TenantId_IdempotencyKey",
                table: "PrcCostPrices");

            migrationBuilder.DropColumn(
                name: "ComputedAtUtc",
                table: "PrcCostPrices");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PrcCostPrices");

            migrationBuilder.DropColumn(
                name: "NguonTinh",
                table: "PrcCostPrices");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "PrcCostPrices");
        }
    }
}
