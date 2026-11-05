using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Sal003_OrderStateMachine_History : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClosureVersion",
                table: "OrdOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentStatus",
                table: "OrdOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "FulfillmentVersion",
                table: "OrdOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "OrdOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PaymentVersion",
                table: "OrdOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ReconciliationStatus",
                table: "OrdOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ReconciliationVersion",
                table: "OrdOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ReturnStatus",
                table: "OrdOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OrdStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Axis = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReasonText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionAfter = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_OrdStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_OrdStatusHistory_From_Khac_To", "\"FromStatus\" <> \"ToStatus\"");
                    table.ForeignKey(
                        name: "FK_OrdStatusHistory_OrdOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "OrdOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_FulfillmentStatus_CreationTime",
                table: "OrdOrders",
                columns: new[] { "TenantId", "FulfillmentStatus", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_PaymentStatus_CreationTime",
                table: "OrdOrders",
                columns: new[] { "TenantId", "PaymentStatus", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_ReconciliationStatus_CreationTime",
                table: "OrdOrders",
                columns: new[] { "TenantId", "ReconciliationStatus", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdOrders_TenantId_ReturnStatus_CreationTime",
                table: "OrdOrders",
                columns: new[] { "TenantId", "ReturnStatus", "CreationTime" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrdOrders_StatusVersions_KhongAm",
                table: "OrdOrders",
                sql: "\"FulfillmentVersion\" >= 0 AND \"PaymentVersion\" >= 0 AND \"ReconciliationVersion\" >= 0 AND \"ClosureVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrdStatusHistory_OrderId",
                table: "OrdStatusHistory",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdStatusHistory_TenantId_ActorType_OccurredAtUtc",
                table: "OrdStatusHistory",
                columns: new[] { "TenantId", "ActorType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdStatusHistory_TenantId_Axis_OccurredAtUtc",
                table: "OrdStatusHistory",
                columns: new[] { "TenantId", "Axis", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdStatusHistory_TenantId_OrderId_Axis_VersionAfter",
                table: "OrdStatusHistory",
                columns: new[] { "TenantId", "OrderId", "Axis", "VersionAfter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdStatusHistory_TenantId_OrderId_OccurredAtUtc",
                table: "OrdStatusHistory",
                columns: new[] { "TenantId", "OrderId", "OccurredAtUtc" });

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 BẮT BUỘC: `NULLS NOT DISTINCT` cho UNIQUE của nhật ký trạng thái.
            // EF Core KHÔNG khai báo được; nếu thiếu, ở chế độ 1 tenant (`TenantId = NULL`) hai dòng
            // "cùng một phiên bản trục" sẽ **KHÔNG** bị chặn ⇒ hàng rào cuối mất tác dụng.
            // 📌 Cùng khuôn 53 index trước (`XC-004` Q6 / F1). Có test `F1_du_54_index...` canh.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_OrdStatusHistory_TenantId_OrderId_Axis_VersionAfter"";");

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_OrdStatusHistory_TenantId_OrderId_Axis_VersionAfter""
                  ON ""OrdStatusHistory"" (""TenantId"", ""OrderId"", ""Axis"", ""VersionAfter"")
                  NULLS NOT DISTINCT;");

            // ══════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 BACKFILL (`SAL-003` §5.3 + §11) — **KHÔNG MẤT DỮ LIỆU**, **KHÔNG BỊA LỊCH SỬ**.
            //
            // ① Ánh xạ enum cũ → 4 trục theo bảng §5.3 (giữ nguyên ý nghĩa từng giá trị).
            // ② Mọi đơn ĐÃ CÓ trạng thái khác mặc định **phải** có một dòng lịch sử — nếu không, bất biến
            //    *"mọi chuyển trạng thái đều có lịch sử"* bị vi phạm ngay từ dòng dữ liệu đầu tiên.
            // ③ Version của trục tương ứng đặt `1` để khớp `VersionAfter = 1` của dòng backfill.
            // 📌 `ReconciliationStatus = 0 (NotRequired)` cho dữ liệu cũ: GĐ1 chưa có đối soát theo đơn
            //    (`REC-002`/`REC-003` là GĐ2) ⇒ **không** bịa trạng thái "chờ đối soát" cho đơn cũ.
            // ══════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                @"UPDATE ""OrdOrders"" SET
                    ""FulfillmentStatus"" = CASE ""Status""
                        WHEN 0 THEN 0 WHEN 1 THEN 1 WHEN 2 THEN 2 WHEN 3 THEN 4
                        WHEN 4 THEN 5 WHEN 5 THEN 0 WHEN 6 THEN 8 ELSE 0 END,
                    ""ReturnStatus"" = CASE ""Status"" WHEN 5 THEN 5 WHEN 6 THEN 4 ELSE 0 END,
                    ""IsCancelled"" = (""Status"" = 5),
                    ""ReconciliationStatus"" = CASE WHEN ""GrandTotal"" > 0 THEN 1 ELSE 0 END;");

            migrationBuilder.Sql(
                @"INSERT INTO ""OrdStatusHistory""
                    (""Id"", ""TenantId"", ""OrderId"", ""Axis"", ""FromStatus"", ""ToStatus"",
                     ""ActorType"", ""ActorId"", ""ReasonCode"", ""ReasonText"", ""CorrelationId"",
                     ""EventId"", ""VersionAfter"", ""OccurredAtUtc"", ""CreationTime"", ""IsDeleted"")
                  SELECT gen_random_uuid(), o.""TenantId"", o.""Id"", 'Fulfillment', 0, o.""FulfillmentStatus"",
                         'SystemJob', NULL, 'BackfillMigration', 'Backfill SAL-003', 'migration-sal003',
                         gen_random_uuid(), 1, now(), now(), false
                  FROM ""OrdOrders"" o WHERE o.""FulfillmentStatus"" <> 0;");

            migrationBuilder.Sql(
                @"INSERT INTO ""OrdStatusHistory""
                    (""Id"", ""TenantId"", ""OrderId"", ""Axis"", ""FromStatus"", ""ToStatus"",
                     ""ActorType"", ""ActorId"", ""ReasonCode"", ""ReasonText"", ""CorrelationId"",
                     ""EventId"", ""VersionAfter"", ""OccurredAtUtc"", ""CreationTime"", ""IsDeleted"")
                  SELECT gen_random_uuid(), o.""TenantId"", o.""Id"", 'Closure', 0, o.""ReturnStatus"",
                         'SystemJob', NULL, 'BackfillMigration', 'Backfill SAL-003', 'migration-sal003',
                         gen_random_uuid(), 1, now(), now(), false
                  FROM ""OrdOrders"" o WHERE o.""ReturnStatus"" IN (4, 5);");

            migrationBuilder.Sql(
                @"INSERT INTO ""OrdStatusHistory""
                    (""Id"", ""TenantId"", ""OrderId"", ""Axis"", ""FromStatus"", ""ToStatus"",
                     ""ActorType"", ""ActorId"", ""ReasonCode"", ""ReasonText"", ""CorrelationId"",
                     ""EventId"", ""VersionAfter"", ""OccurredAtUtc"", ""CreationTime"", ""IsDeleted"")
                  SELECT gen_random_uuid(), o.""TenantId"", o.""Id"", 'Payment', 0, o.""PaymentStatus"",
                         'SystemJob', NULL, 'BackfillMigration', 'Backfill SAL-003', 'migration-sal003',
                         gen_random_uuid(), 1, now(), now(), false
                  FROM ""OrdOrders"" o WHERE o.""PaymentStatus"" <> 0;");

            migrationBuilder.Sql(@"UPDATE ""OrdOrders"" SET ""FulfillmentVersion"" = 1 WHERE ""FulfillmentStatus"" <> 0;");
            migrationBuilder.Sql(@"UPDATE ""OrdOrders"" SET ""ClosureVersion"" = 1 WHERE ""ReturnStatus"" IN (4, 5);");
            migrationBuilder.Sql(@"UPDATE ""OrdOrders"" SET ""PaymentVersion"" = 1 WHERE ""PaymentStatus"" <> 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_FulfillmentStatus_CreationTime",
                table: "OrdOrders");

            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_PaymentStatus_CreationTime",
                table: "OrdOrders");

            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_ReconciliationStatus_CreationTime",
                table: "OrdOrders");

            migrationBuilder.DropIndex(
                name: "IX_OrdOrders_TenantId_ReturnStatus_CreationTime",
                table: "OrdOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrdOrders_StatusVersions_KhongAm",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ClosureVersion",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "FulfillmentVersion",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "PaymentVersion",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ReconciliationStatus",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ReconciliationVersion",
                table: "OrdOrders");

            migrationBuilder.DropColumn(
                name: "ReturnStatus",
                table: "OrdOrders");
        }
    }
}
