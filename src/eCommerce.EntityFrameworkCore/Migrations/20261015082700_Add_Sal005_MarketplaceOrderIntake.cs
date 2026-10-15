using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Sal005_MarketplaceOrderIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalMarketplaceOrderIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalShopId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarketplacePlacedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SkipReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_SalMarketplaceOrderIntakes", x => x.Id);
                });

            // 🚨 `NULLS NOT DISTINCT` — EF **KHÔNG** mô hình hoá được mệnh đề này (PostgreSQL 15+),
            // nên phải viết SQL thô. ĐÂY LÀ LÝ DO DUY NHẤT phải sửa migration do EF sinh.
            //
            // ⚠️ VÌ SAO BẮT BUỘC: PostgreSQL coi `NULL` là **KHÁC NHAU** trong unique index ⇒ khi
            // `TenantId = NULL` (chế độ 1 tenant — tình trạng hiện tại) thì index **MẤT TÁC DỤNG IM LẶNG**
            // ⇒ cùng một đơn sàn ghi được **hai lần** ⇒ **2 đơn cho 1 đơn thật** (lỗi C4/F1).
            //
            // ⚠️ NẾU sau này có migration do EF sinh lại động vào index này ⇒ mệnh đề dưới đây sẽ MẤT.
            //    Lưới an toàn: `PostgreSqlSchemaTests` liệt kê **đích danh** tên index và khẳng định nó
            //    có `NULLS NOT DISTINCT` (đếm tổng là **KHÔNG đủ** — thêm index mới + hỏng index cũ thì
            //    tổng không đổi ⇒ test vẫn xanh vô nghĩa).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SalMktIntakes_TenantId_ChannelCode_ExternalOrderId\" " +
                "ON \"SalMarketplaceOrderIntakes\" (\"TenantId\", \"ChannelCode\", \"ExternalOrderId\") " +
                "NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalMarketplaceOrderIntakes");
        }
    }
}
