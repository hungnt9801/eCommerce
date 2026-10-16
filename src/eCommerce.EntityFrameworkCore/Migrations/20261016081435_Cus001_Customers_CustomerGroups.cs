using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cus001_Customers_CustomerGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CusCustomers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IdentityUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    LifetimeValueAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LifetimeValueCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_CusCustomers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CusGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_CusGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CusCustomers_TenantId_IdempotencyKey",
                table: "CusCustomers",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CusCustomers_TenantId_IdentityUserId",
                table: "CusCustomers",
                columns: new[] { "TenantId", "IdentityUserId" },
                unique: true,
                filter: "\"IdentityUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CusCustomers_TenantId_Status",
                table: "CusCustomers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CusGroups_TenantId_Code",
                table: "CusGroups",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT` CHO 3 UNIQUE INDEX MỚI** (`CUS-001`).
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống chạy **1 tenant** ⇒ `TenantId` **có thể `NULL`**; PostgreSQL
            // mặc định coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **MẤT TÁC DỤNG IM LẶNG** (lỗi C4/F1 đã trả
            // giá — `XC-004` Q6, `PostgreSqlSchemaTests.F1`). EF Core **không** sinh được mệnh đề này
            // **kèm** bộ lọc `WHERE`.
            //
            // ① `IX_CusGroups_TenantId_Code` — mã nhóm khách (`RETAIL`/`VIP`…) là **đầu vào của bảng giá**
            //    (`PRC-001` Q5). Thiếu mệnh đề ⇒ **HAI nhóm `VIP`** tồn tại ⇒ bảng giá nhận hai "nhóm VIP"
            //    khác nhau ⇒ giá bán **không xác định** mà **không lỗi nào**.
            //
            // ② `IX_CusCustomers_TenantId_IdentityUserId` — 🔑 **bất biến `ADR-07`: MỘT tài khoản ⇒ tối đa MỘT
            //    hồ sơ khách.** Thiếu mệnh đề ⇒ hai hồ sơ cùng trỏ một `IdentityUserId` ⇒ *"khách này là ai"*
            //    có **HAI câu trả lời**, lịch sử mua/điểm thưởng **phân mảnh** theo hồ sơ nào đọc trước.
            //
            // ③ `IX_CusCustomers_TenantId_IdempotencyKey` — chống **tạo hai khách cho một lần gọi** khi retry
            //    (`ADR-05`). Thiếu mệnh đề ⇒ retry của webhook tạo khách thứ hai ⇒ **trùng khách**, và bất biến
            //    "liên hệ duy nhất" cũng thành vô nghĩa (mỗi bản ghi một lần).
            //
            // 📌 Bước 1 xoá index EF vừa tạo, bước 2 tạo lại **kèm** `NULLS NOT DISTINCT` — giữ nguyên **tên**
            // index để `PostgreSqlSchemaTests` đối chiếu **đích danh** vẫn tìm thấy, và `Down()` chỉ cần `DropTable`.
            migrationBuilder.Sql("DROP INDEX \"IX_CusGroups_TenantId_Code\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CusGroups_TenantId_Code\" ON \"CusGroups\" (\"TenantId\", \"Code\") " +
                "NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CusCustomers_TenantId_IdentityUserId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CusCustomers_TenantId_IdentityUserId\" ON \"CusCustomers\" " +
                "(\"TenantId\", \"IdentityUserId\") NULLS NOT DISTINCT WHERE \"IdentityUserId\" IS NOT NULL;");

            migrationBuilder.Sql("DROP INDEX \"IX_CusCustomers_TenantId_IdempotencyKey\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CusCustomers_TenantId_IdempotencyKey\" ON \"CusCustomers\" " +
                "(\"TenantId\", \"IdempotencyKey\") NULLS NOT DISTINCT WHERE \"IdempotencyKey\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CusCustomers");

            migrationBuilder.DropTable(
                name: "CusGroups");
        }
    }
}
