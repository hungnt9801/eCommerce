using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Rec001_PaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderCreateDateGmt7",
                table: "PayTransactions",
                type: "character(14)",
                fixedLength: true,
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderCreateDateUtc",
                table: "PayTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecPaymentReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LineTotal = table.Column<int>(type: "integer", nullable: false),
                    LineMatched = table.Column<int>(type: "integer", nullable: false),
                    LineSkipped = table.Column<int>(type: "integer", nullable: false),
                    DiscrepancyCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_RecPaymentReconciliationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecPaymentDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MerchantReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    LocalAmountMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    ProviderAmountMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IgnoreReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_RecPaymentDiscrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecPaymentDiscrepancies_RecPaymentReconciliationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "RecPaymentReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecPaymentReconciliationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MerchantReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SkipReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LocalStatus = table.Column<int>(type: "integer", nullable: false),
                    LocalAmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    ProviderResponseCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ProviderTransactionStatus = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ProviderAmountMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    ProviderTransactionNo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProviderPayDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderPayDateGmt7 = table.Column<string>(type: "character(14)", fixedLength: true, maxLength: 14, nullable: true),
                    ProviderTransactionType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DiscrepancyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_RecPaymentReconciliationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecPaymentReconciliationLines_RecPaymentReconciliationRuns_~",
                        column: x => x.RunId,
                        principalTable: "RecPaymentReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecPayDiscrepancies_TenantId_Run",
                table: "RecPaymentDiscrepancies",
                columns: new[] { "TenantId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecPayDiscrepancies_TenantId_Status_Severity",
                table: "RecPaymentDiscrepancies",
                columns: new[] { "TenantId", "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_RecPaymentDiscrepancies_RunId",
                table: "RecPaymentDiscrepancies",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RecPayLines_TenantId_Run_GiaoDich",
                table: "RecPaymentReconciliationLines",
                columns: new[] { "TenantId", "RunId", "PaymentTransactionId" },
                unique: true);

            // 🚨 `NULLS NOT DISTINCT` (PostgreSQL 15+): EF không khai báo được. Thiếu nó thì
            // `TenantId = NULL` làm BR-05 ("mỗi giao dịch một dòng/lượt") mất tác dụng im lặng.
            migrationBuilder.DropIndex(name: "IX_RecPayLines_TenantId_Run_GiaoDich", table: "RecPaymentReconciliationLines");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""IX_RecPayLines_TenantId_Run_GiaoDich""
ON ""RecPaymentReconciliationLines"" (""TenantId"", ""RunId"", ""PaymentTransactionId"") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_RecPayLines_TenantId_Run_Status",
                table: "RecPaymentReconciliationLines",
                columns: new[] { "TenantId", "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecPaymentReconciliationLines_RunId",
                table: "RecPaymentReconciliationLines",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RecPayRuns_TenantId_PeriodFrom",
                table: "RecPaymentReconciliationRuns",
                columns: new[] { "TenantId", "PeriodFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecPayRuns_TenantId_Status_Started",
                table: "RecPaymentReconciliationRuns",
                columns: new[] { "TenantId", "Status", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecPaymentDiscrepancies");

            migrationBuilder.DropTable(
                name: "RecPaymentReconciliationLines");

            migrationBuilder.DropTable(
                name: "RecPaymentReconciliationRuns");

            migrationBuilder.DropColumn(
                name: "ProviderCreateDateGmt7",
                table: "PayTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderCreateDateUtc",
                table: "PayTransactions");
        }
    }
}
