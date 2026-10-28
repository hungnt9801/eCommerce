using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Gd1S14_OrderReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                table: "OrdOrderLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OrdReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GoodsStatus = table.Column<int>(type: "integer", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReasonText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundMethod = table.Column<int>(type: "integer", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RefundCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RequestedByActorType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequestedByActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_OrdReturns", x => x.Id);
                    table.CheckConstraint("CK_OrdReturns_Dong_CoMoc", "\"Status\" <> 5 OR \"ClosedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_OrdReturns_NhanHang_CoMoc", "\"Status\" <> 4 OR \"ReceivedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_OrdReturns_RefundedAmount_KhongAm", "\"RefundedAmount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "OrdReturnHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_OrdReturnHistory", x => x.Id);
                    table.CheckConstraint("CK_OrdReturnHistory_PhaiDoiTrangThai", "\"FromStatus\" <> \"ToStatus\"");
                    table.ForeignKey(
                        name: "FK_OrdReturnHistory_OrdReturns_ReturnId",
                        column: x => x.ReturnId,
                        principalTable: "OrdReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrdReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmountSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxAmountSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_OrdReturnLines", x => x.Id);
                    table.CheckConstraint("CK_OrdReturnLines_Disposition_ChiSauKhiNhan", "\"Disposition\" = 0 OR \"ReceivedQuantity\" > 0");
                    table.CheckConstraint("CK_OrdReturnLines_Nhan_HopLe", "\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("CK_OrdReturnLines_SoLuong_HopLe", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_OrdReturnLines_OrdReturns_ReturnId",
                        column: x => x.ReturnId,
                        principalTable: "OrdReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrdOrderLines_ReturnedQuantity_HopLe",
                table: "OrdOrderLines",
                sql: "\"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" <= \"Quantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturnHistory_ReturnId",
                table: "OrdReturnHistory",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturnHistory_TenantId_ReturnId_Axis_VersionAfter",
                table: "OrdReturnHistory",
                columns: new[] { "TenantId", "ReturnId", "Axis", "VersionAfter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturnHistory_TenantId_ReturnId_OccurredAtUtc",
                table: "OrdReturnHistory",
                columns: new[] { "TenantId", "ReturnId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturnLines_ReturnId",
                table: "OrdReturnLines",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturnLines_TenantId_ReturnId_OrderLineId",
                table: "OrdReturnLines",
                columns: new[] { "TenantId", "ReturnId", "OrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturns_TenantId_IdempotencyKey",
                table: "OrdReturns",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturns_TenantId_OrderId_CreationTime",
                table: "OrdReturns",
                columns: new[] { "TenantId", "OrderId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturns_TenantId_ReturnNumber",
                table: "OrdReturns",
                columns: new[] { "TenantId", "ReturnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdReturns_TenantId_Status_GoodsStatus",
                table: "OrdReturns",
                columns: new[] { "TenantId", "Status", "GoodsStatus" });

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`SAL-006` §5.1 + `XC-004` BR-009)
            // EF **không** sinh được `NULLS NOT DISTINCT`; thiếu mệnh đề thì khi `TenantId = NULL`
            // (hiện chạy **1 tenant**) cả bốn index **mất tác dụng IM LẶNG**:
            //   • `OrdReturns.ReturnNumber` — hai phiếu **cùng mã** ⇒ tra cứu/đối soát trả lời sai phiếu nào;
            //   • `OrdReturns.IdempotencyKey` — gọi lại biên vào tạo **hai phiếu trả** cho **một** yêu cầu
            //     ⇒ **trả hàng hai lần** (hoàn tiền/kho gấp đôi) mà không lỗi nào;
            //   • `OrdReturnLines.(ReturnId, OrderLineId)` — hai dòng cho cùng một dòng đơn ⇒ số tiền hoàn
            //     của phiếu **cộng hai lần** cho một dòng;
            //   • `OrdReturnHistory.(ReturnId, Axis, VersionAfter)` — hai dòng lịch sử cho cùng một phiên bản
            //     ⇒ câu hỏi *"ai đổi trạng thái"* có hai câu trả lời.
            migrationBuilder.Sql("DROP INDEX \"IX_OrdReturns_TenantId_ReturnNumber\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_OrdReturns_TenantId_ReturnNumber\" ON \"OrdReturns\" " +
                "(\"TenantId\", \"ReturnNumber\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_OrdReturns_TenantId_IdempotencyKey\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_OrdReturns_TenantId_IdempotencyKey\" ON \"OrdReturns\" " +
                "(\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_OrdReturnLines_TenantId_ReturnId_OrderLineId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_OrdReturnLines_TenantId_ReturnId_OrderLineId\" ON \"OrdReturnLines\" " +
                "(\"TenantId\", \"ReturnId\", \"OrderLineId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_OrdReturnHistory_TenantId_ReturnId_Axis_VersionAfter\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_OrdReturnHistory_TenantId_ReturnId_Axis_VersionAfter\" ON " +
                "\"OrdReturnHistory\" (\"TenantId\", \"ReturnId\", \"Axis\", \"VersionAfter\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrdReturnHistory");

            migrationBuilder.DropTable(
                name: "OrdReturnLines");

            migrationBuilder.DropTable(
                name: "OrdReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrdOrderLines_ReturnedQuantity_HopLe",
                table: "OrdOrderLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "OrdOrderLines");
        }
    }
}
