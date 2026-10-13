using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Messaging_Idempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MsgIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadHashVersion = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_MsgIdempotencyRecords", x => x.Id);
                    table.CheckConstraint("CK_MsgIdempotencyRecords_Attempts", "\"Attempts\" >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MsgIdempotencyRecords_ExpiresAtUtc",
                table: "MsgIdempotencyRecords",
                column: "ExpiresAtUtc");

            // 🚨 SQL THÔ — BẮT BUỘC, KHÔNG được để EF sinh index này.
            //
            // PostgreSQL coi NULL là **KHÁC NHAU** trong ràng buộc unique. Với `TenantId` NULL (dữ liệu
            // của host), index do EF sinh **KHÔNG chặn được bản ghi trùng** ⇒ chống trùng ở biên vào
            // **mất tác dụng** mà không có lỗi nào.
            //
            // Đây là lỗi C4/F1 đã được CHỨNG MINH bằng chạy thật: trước khi sửa thì `INSERT 0 2`
            // (hai dòng cùng khoá đều lọt); sau khi sửa thì PostgreSQL báo:
            //   duplicate key value violates unique constraint ... (null, ...) already exists
            //
            // `NULLS NOT DISTINCT` (PostgreSQL 15+) làm NULL được coi là BẰNG NHAU ⇒ ràng buộc có tác dụng.
            // 🔒 Đây là **hàng rào CUỐI** chống trùng khi khoá phân tán (Redis) hỏng hoặc bị mất.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_MsgIdempotencyRecords_TenantId_Scope_KeyValue"
                    ON "MsgIdempotencyRecords" ("TenantId", "Scope", "KeyValue")
                    NULLS NOT DISTINCT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MsgIdempotencyRecords");
        }
    }
}
