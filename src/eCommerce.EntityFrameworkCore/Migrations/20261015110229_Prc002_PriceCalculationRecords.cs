using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Prc002_PriceCalculationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrcPriceCalculationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelShopId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RoundingPolicyCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_PrcPriceCalculationRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrcPriceCalculationRecords_TenantId_CalculatedAt",
                table: "PrcPriceCalculationRecords",
                columns: new[] { "TenantId", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey",
                table: "PrcPriceCalculationRecords",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`PRC-001` **BR-011** / `XC-004` **BR-009**).
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`; PostgreSQL coi **NULL là DISTINCT**
            // ⇒ khi `TenantId = NULL` (hệ thống **1 tenant** hiện nay) **khoá idempotency MẤT TÁC DỤNG**:
            //   • gọi lại cùng `IdempotencyKey` **vẫn ghi thêm** bản ghi;
            //   • sổ giải trình có **NHIỀU kết quả khác nhau cho CÙNG một đơn**;
            //   • ⇒ **hết giá trị làm bằng chứng** — đúng thứ mà ADR-13 tồn tại để ngăn.
            // 📌 Lỗi này **KHÔNG** biểu hiện ở môi trường dev 1 tenant nếu chỉ nhìn "chạy được": phải **đếm bản ghi**
            // sau hai lần gọi cùng khoá mới thấy (xem `prc-002` §20.2).
            migrationBuilder.Sql("DROP INDEX \"IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcPriceCalculationRecords_TenantId_IdempotencyKey\" " +
                "ON \"PrcPriceCalculationRecords\" (\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrcPriceCalculationRecords");
        }
    }
}
