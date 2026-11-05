using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp07_KhachHang_NhanVien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatedAmount",
                table: "CusCustomers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "Birthday",
                table: "CusCustomers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DebtBalance",
                table: "CusCustomers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DebtLimit",
                table: "CusCustomers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "LevelId",
                table: "CusCustomers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "CusCustomers",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferrerId",
                table: "CusCustomers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SaleStaffId",
                table: "CusCustomers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "CusCustomers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "CusCustomers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CusCares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextFollowUpAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CusCares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CusLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MinAccumulatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_CusLevels", x => x.Id);
                    table.CheckConstraint("CK_CusLevels_ChietKhau_0_100", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
                    table.CheckConstraint("CK_CusLevels_Nguong_KhongAm", "\"MinAccumulatedAmount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SetDepartments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SetDepartments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetRoleDataScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SetRoleDataScopes", x => x.Id);
                    table.CheckConstraint("CK_SetRoleDataScopes_PhamVi_HopLe", "\"Scope\" in (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "SetStaffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SetStaffs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetStores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddressText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_SetStores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CusCustomers_TenantId_LevelId",
                table: "CusCustomers",
                columns: new[] { "TenantId", "LevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_CusCustomers_TenantId_Type",
                table: "CusCustomers",
                columns: new[] { "TenantId", "Type" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CusCustomers_SoDu_KhongAm",
                table: "CusCustomers",
                sql: "\"Points\" >= 0 AND \"AccumulatedAmount\" >= 0 AND \"DebtBalance\" >= 0 AND \"DebtLimit\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CusCares_TenantId_CustomerId_OccurredAtUtc",
                table: "CusCares",
                columns: new[] { "TenantId", "CustomerId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CusCares_TenantId_NextFollowUpAtUtc",
                table: "CusCares",
                columns: new[] { "TenantId", "NextFollowUpAtUtc" },
                filter: "\"NextFollowUpAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CusLevels_TenantId_MinAccumulatedAmount",
                table: "CusLevels",
                columns: new[] { "TenantId", "MinAccumulatedAmount" });

            // 🚨 **`NULLS NOT DISTINCT` — VIẾT TAY, EF KHÔNG SINH ĐƯỢC** (`WP-07`, `XC-003` F1).
            // 📌 PostgreSQL coi hai `NULL` là **KHÁC nhau** ⇒ với `TenantId = NULL` (đang chạy MỘT doanh
            // nghiệp) unique index **mất tác dụng IM LẶNG** ⇒ hai cấp khách cùng `Code` LỌT.
            // 🔒 Sáu index dưới đây **đều** phải có NND: `CusLevels.Code` · `SetDepartments.Code` ·
            // `SetRoleDataScopes.RoleName` · `SetStaffs.Code` · `SetStaffs.IdentityUserId` · `SetStores.Code`.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CusLevels_TenantId_Code\" "
                + "ON \"CusLevels\" (\"TenantId\", \"Code\") NULLS NOT DISTINCT;");

            migrationBuilder.CreateIndex(
                name: "IX_SetDepartments_TenantId_StoreId",
                table: "SetDepartments",
                columns: new[] { "TenantId", "StoreId" });

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SetDepartments_TenantId_Code\" "
                + "ON \"SetDepartments\" (\"TenantId\", \"Code\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SetRoleDataScopes_TenantId_RoleName\" "
                + "ON \"SetRoleDataScopes\" (\"TenantId\", \"RoleName\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SetStaffs_TenantId_Code\" "
                + "ON \"SetStaffs\" (\"TenantId\", \"Code\") NULLS NOT DISTINCT;");

            // 🚨 Unique **có bộ lọc** + `NULLS NOT DISTINCT`: EF KHÔNG sinh được **cả hai** cùng lúc ⇒ SQL tay.
            // 📌 Bộ lọc `IS NOT NULL` BẮT BUỘC: nhân viên **chưa cấp tài khoản** là **đa số** lúc nhập danh mục.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SetStaffs_TenantId_IdentityUserId\" "
                + "ON \"SetStaffs\" (\"TenantId\", \"IdentityUserId\") NULLS NOT DISTINCT "
                + "WHERE \"IdentityUserId\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_SetStaffs_TenantId_StoreId",
                table: "SetStaffs",
                columns: new[] { "TenantId", "StoreId" });

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_SetStores_TenantId_Code\" "
                + "ON \"SetStores\" (\"TenantId\", \"Code\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CusCares");

            migrationBuilder.DropTable(
                name: "CusLevels");

            migrationBuilder.DropTable(
                name: "SetDepartments");

            migrationBuilder.DropTable(
                name: "SetRoleDataScopes");

            migrationBuilder.DropTable(
                name: "SetStaffs");

            migrationBuilder.DropTable(
                name: "SetStores");

            migrationBuilder.DropIndex(
                name: "IX_CusCustomers_TenantId_LevelId",
                table: "CusCustomers");

            migrationBuilder.DropIndex(
                name: "IX_CusCustomers_TenantId_Type",
                table: "CusCustomers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CusCustomers_SoDu_KhongAm",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "AccumulatedAmount",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "Birthday",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "DebtBalance",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "DebtLimit",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "ReferrerId",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "SaleStaffId",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "CusCustomers");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "CusCustomers");
        }
    }
}
