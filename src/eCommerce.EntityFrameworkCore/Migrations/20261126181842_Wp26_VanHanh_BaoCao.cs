using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp26_VanHanh_BaoCao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepConsistencyChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Projection = table.Column<short>(type: "smallint", nullable: false),
                    MismatchKind = table.Column<short>(type: "smallint", nullable: false),
                    FactKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpectedValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ActualValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Delta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AutoCorrected = table.Column<bool>(type: "boolean", nullable: false),
                    DetectedByRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepConsistencyChecks", x => x.Id);
                    table.CheckConstraint("CK_RepConsistencyChecks_Delta_Khop", "(\"Delta\" IS NULL) = (\"ExpectedValue\" IS NULL OR \"ActualValue\" IS NULL)");
                    table.CheckConstraint("CK_RepConsistencyChecks_FactKey_KhongRong", "length(btrim(\"FactKey\")) > 0");
                    table.CheckConstraint("CK_RepConsistencyChecks_MismatchKind_HopLe", "\"MismatchKind\" IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_RepConsistencyChecks_Projection_HopLe", "\"Projection\" IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "RepPeriodCloses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ReopenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepPeriodCloses", x => x.Id);
                    table.CheckConstraint("CK_RepPeriodCloses_CloseReason_ToiThieu", "length(btrim(\"CloseReason\")) >= 10");
                    table.CheckConstraint("CK_RepPeriodCloses_DaChot_DuChungTu", "\"Status\" <> 2 OR (\"SnapshotHash\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"ClosedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_RepPeriodCloses_Khoang_HopLe", "\"ToDate\" >= \"FromDate\"");
                    table.CheckConstraint("CK_RepPeriodCloses_ReopenReason_ToiThieu", "\"ReopenReason\" IS NULL OR length(btrim(\"ReopenReason\")) >= 20");
                    table.CheckConstraint("CK_RepPeriodCloses_Status_HopLe", "\"Status\" IN (0, 1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "RepRunLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Projection = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WindowsTotal = table.Column<int>(type: "integer", nullable: false),
                    WindowsDone = table.Column<int>(type: "integer", nullable: false),
                    CheckpointDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowsWritten = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HostName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepRunLogs", x => x.Id);
                    table.CheckConstraint("CK_RepRunLogs_Checkpoint_Khoang", "\"CheckpointDate\" IS NULL OR (\"CheckpointDate\" >= \"FromDate\" AND \"CheckpointDate\" <= \"ToDate\")");
                    table.CheckConstraint("CK_RepRunLogs_JobName_HopLe", "\"JobName\" IN ('Rebuild', 'Reconcile', 'Aggregate')");
                    table.CheckConstraint("CK_RepRunLogs_Khoang_HopLe", "\"ToDate\" >= \"FromDate\"");
                    table.CheckConstraint("CK_RepRunLogs_Projection_HopLe", "\"Projection\" IN (0, 1)");
                    table.CheckConstraint("CK_RepRunLogs_Reason_ToiThieu", "length(btrim(\"Reason\")) >= 10");
                    table.CheckConstraint("CK_RepRunLogs_RowsWritten_KhongAm", "\"RowsWritten\" >= 0");
                    table.CheckConstraint("CK_RepRunLogs_Status_HopLe", "\"Status\" IN (0, 1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_RepRunLogs_Windows_KhongAm", "\"WindowsTotal\" >= 0 AND \"WindowsDone\" >= 0 AND \"WindowsDone\" <= \"WindowsTotal\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepConsistencyChecks_TenantId_ChuaXuLy",
                table: "RepConsistencyChecks",
                columns: new[] { "TenantId", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RepConsistencyChecks_TenantId_Ngay",
                table: "RepConsistencyChecks",
                columns: new[] { "TenantId", "FactDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RepPeriodCloses_TenantId_TrangThai",
                table: "RepPeriodCloses",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RepRunLogs_TenantId_ThoiDiem",
                table: "RepRunLogs",
                columns: new[] { "TenantId", "CreationTime" });

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 4 UNIQUE INDEX `NULLS NOT DISTINCT` — EF KHÔNG sinh được NND + partial ⇒ VIẾT TAY
            // (`REP-001` §5.2/§5.3/§5.4; khuôn `Wp08_BaoCao_Reporting`). Thiếu NND ⇒ với `TenantId = NULL`
            // (đang chạy một tenant) index MẤT TÁC DỤNG IM LẶNG.
            // ══════════════════════════════════════════════════════════════════════════════════════════

            // Dòng rebuild do NGƯỜI DÙNG gọi lại cùng khoảng+lý do ⇒ 1 lần chạy. Dòng hệ thống
            // (`RequestedBy IS NULL`) dùng khoá theo `runId` (unique riêng mỗi lượt) nên KHÔNG vướng.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RepRunLogs_TenantId_KhoaIdempotency\" "
                + "ON \"RepRunLogs\" (\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT "
                + "WHERE \"RequestedBy\" IS NOT NULL;");

            // Một lần chạy HOẠT ĐỘNG /(job, projection) — hàng rào DB, không phụ thuộc Redis.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UX_RepRunLogs_TenantId_Job_Projection_DangChay\" "
                + "ON \"RepRunLogs\" (\"TenantId\", \"JobName\", \"Projection\") NULLS NOT DISTINCT "
                + "WHERE \"Status\" IN (0, 1);");

            // Chống trùng dòng lệch ĐANG MỞ khi đối chiếu chạy lặp mỗi nhịp (idempotent, "không lộ hai lần").
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UX_RepConsistencyChecks_DangMo\" "
                + "ON \"RepConsistencyChecks\" (\"TenantId\", \"Projection\", \"FactDate\", \"MismatchKind\", \"FactKey\") "
                + "NULLS NOT DISTINCT WHERE \"ResolvedAtUtc\" IS NULL;");

            // Chốt kỳ idempotent theo (tenant, from, to) — mọi dòng đều do người yêu cầu ⇒ unique ĐẦY ĐỦ.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_RepPeriodCloses_TenantId_KhoaIdempotency\" "
                + "ON \"RepPeriodCloses\" (\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT;");

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // `EXCLUDE USING gist` — CHỈ chặn hai kỳ `DaChot` chồng nhau (`D-171`). Khoá thứ hai
            // `CASE WHEN Status = 2 THEN zero ELSE Id END` ⇒ mọi trạng thái khác có khoá = `Id` riêng nên
            // KHÔNG bao giờ chặn (hai kỳ `Mo` chồng nhau vẫn hợp lệ). `TenantId` nullable ⇒ COALESCE.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql("""
                ALTER TABLE "RepPeriodCloses"
                    ADD CONSTRAINT "EX_RepPeriodCloses_KyDaChot_KhongChong"
                    EXCLUDE USING gist (
                        (COALESCE("TenantId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
                        (CASE WHEN "Status" = 2 THEN '00000000-0000-0000-0000-000000000000'::uuid
                              ELSE "Id" END) WITH =,
                        daterange("FromDate", "ToDate", '[]') WITH &&
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepConsistencyChecks");

            migrationBuilder.DropTable(
                name: "RepPeriodCloses");

            migrationBuilder.DropTable(
                name: "RepRunLogs");
        }
    }
}
