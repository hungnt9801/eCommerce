using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Sal005_MarketplaceEventLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalMarketplaceEventLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChannelShopId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    EventKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    MarketplaceStatusRaw = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsFromWebhook = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalMarketplaceEventLogs", x => x.Id);
                    table.CheckConstraint("CK_SalMktEventLogs_PayloadHash_Hex", "\"PayloadHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_SalMktEventLogs_SourceVersion", "\"SourceVersion\" IS NULL OR \"SourceVersion\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalMktEventLogs_TenantId_Channel_Order_Received",
                table: "SalMarketplaceEventLogs",
                columns: new[] { "TenantId", "ChannelCode", "ExternalOrderId", "ReceivedAtUtc" });

            // 🚨 `NULLS NOT DISTINCT` — EF **KHÔNG** mô hình hoá được mệnh đề này (PostgreSQL 15+), nên
            // unique index dưới đây phải viết **SQL thô**. ĐÂY LÀ LÝ DO DUY NHẤT phải sửa migration do EF sinh.
            //
            // ⚠️ VÌ SAO BẮT BUỘC: PostgreSQL coi `NULL` là **KHÁC NHAU** trong unique index ⇒ khi
            // `TenantId = NULL` (chế độ 1 tenant — tình trạng hiện tại) thì index **MẤT TÁC DỤNG IM LẶNG**
            // ⇒ cùng một kết cục của cùng một sự kiện ghi được **HAI dòng** ⇒ nhật ký đếm SAI ⇒ mọi kết luận
            // *"sàn gửi lại bao nhiêu lần / có dual-path không"* đều **sai**, mà **không có lỗi nào**
            // (`SAL-005` **D7**).
            //
            // 📌 Khoá **có cả `Outcome`**: một sự kiện **giao lại** (cùng phiên bản + cùng hash) cần ghi được
            // **một** dòng `Duplicate` — nếu thiếu `Outcome` thì dòng đó trùng toàn bộ khoá với dòng `Applied`
            // ⇒ **không bao giờ** ghi được ⇒ `Outcome = Duplicate` thành **giá trị chết** (phát hiện ở `S6`,
            // `sal-005` §20.5 #1).
            //
            // ⚠️ NẾU sau này có migration do EF sinh lại động vào index này ⇒ mệnh đề dưới đây sẽ MẤT.
            //    Lưới an toàn: `PostgreSqlSchemaTests` liệt kê **đích danh** tên index (đếm tổng là **KHÔNG đủ**
            //    — thêm index mới + hỏng index cũ thì tổng không đổi ⇒ test vẫn xanh vô nghĩa).
            //
            // 📌 `DROP INDEX IF EXISTS` trước: để migration **chạy lại được** trên DB mà EF đã từng tạo index
            //    (đúng khuôn `Fix_NullableTenantId_UniqueIndexes`).
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_SalMktEventLogs_TenantId_Channel_Order_Ver_Hash_Outcome\";");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SalMktEventLogs_TenantId_Channel_Order_Ver_Hash_Outcome\" " +
                "ON \"SalMarketplaceEventLogs\" " +
                "(\"TenantId\", \"ChannelCode\", \"ExternalOrderId\", \"SourceVersion\", \"PayloadHash\", \"Outcome\") " +
                "NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_SalMktEventLogs_TenantId_Outcome_Received",
                table: "SalMarketplaceEventLogs",
                columns: new[] { "TenantId", "Outcome", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalMarketplaceEventLogs");
        }
    }
}
