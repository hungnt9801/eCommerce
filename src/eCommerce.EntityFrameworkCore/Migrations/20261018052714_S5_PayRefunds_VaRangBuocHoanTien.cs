using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class S5_PayRefunds_VaRangBuocHoanTien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundRequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderResponseCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProviderRefundTransactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_PayRefunds", x => x.Id);
                    table.CheckConstraint("CK_PayRefunds_Amount_Duong", "\"Amount\" > 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayTransactions_RefundedAmount_KhongAm",
                table: "PayTransactions",
                sql: "\"RefundedAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayTransactions_RefundedAmount_KhongVuotAmount",
                table: "PayTransactions",
                sql: "\"RefundedAmount\" <= \"Amount\"");

            migrationBuilder.CreateIndex(
                name: "IX_PayRefunds_PaymentTransactionId",
                table: "PayRefunds",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayRefunds_TenantId_PaymentTransactionId_RefundRequestId",
                table: "PayRefunds",
                columns: new[] { "TenantId", "PaymentTransactionId", "RefundRequestId" },
                unique: true);

            // ══════════════════════════════════════════════════════════════════════════════════════════════
            // 🚨 **BẮT BUỘC: SQL THÔ `NULLS NOT DISTINCT`** — luật bất di bất dịch #1 của dự án
            // (`handover` §4.1; `XC-004` F1). EF **KHÔNG** mô hình hoá thuộc tính này (`PAY-001` §5.4) nên
            // index do `CreateIndex` sinh ra ở trên **KHÔNG** có nó.
            //
            // **Nếu thiếu (lỗi đã chứng minh bằng `INSERT 0 2` trong migration `20261013024226`):** ở chế độ
            // `TenantId = NULL` (tình trạng hiện nay — 1 tenant), PostgreSQL mặc định coi **mỗi `NULL` là KHÁC
            // nhau** ⇒ unique index **mất tác dụng im lặng** ⇒ hai dòng hoàn tiền cùng
            // `(PaymentTransactionId, RefundRequestId)` **LỌT** ⇒ `RefundedAmount` bị cộng hai lần cho **một**
            // lần hoàn, hoặc kế toán thấy *"hoàn 2 lần"* cho một lệnh. 📌 Đây chính là lý do khoá này dùng
            // **số thứ tự** (`RF001`) chứ không phải `Guid`: index chỉ có giá trị khi nó **chặn được**.
            // ══════════════════════════════════════════════════════════════════════════════════════════════
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_PayRefunds_TenantId_PaymentTransactionId_RefundRequestId";
                CREATE UNIQUE INDEX "IX_PayRefunds_TenantId_PaymentTransactionId_RefundRequestId"
                    ON "PayRefunds" ("TenantId", "PaymentTransactionId", "RefundRequestId") NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayRefunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayTransactions_RefundedAmount_KhongAm",
                table: "PayTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayTransactions_RefundedAmount_KhongVuotAmount",
                table: "PayTransactions");
        }
    }
}
