using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Addr004_SoDiaChi_Address_Va_LienKet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CusAddressLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressType = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_CusAddressLinks", x => x.Id);
                    table.CheckConstraint("CK_CusAddressLinks_AddressType", "\"AddressType\" in (0, 1)");
                    table.CheckConstraint("CK_CusAddressLinks_NhanDoDai", "char_length(\"Label\") <= 128");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CusAddressLinks_TenantId_AddressId",
                table: "CusAddressLinks",
                columns: new[] { "TenantId", "AddressId" });

            migrationBuilder.CreateIndex(
                name: "IX_CusAddressLinks_TenantId_CustomerId_IsDeleted_AddressType",
                table: "CusAddressLinks",
                columns: new[] { "TenantId", "CustomerId", "IsDeleted", "AddressType" });

            // ══════════════════════════════════════════════════════════════════════════════════════
            // 🚨 `ADDR-004` §11 — **HAI index unique phải viết SQL THÔ** (EF KHÔNG diễn đạt được
            //    `NULLS NOT DISTINCT`, mà PostgreSQL mặc định coi `NULL` là **KHÁC NHAU**).
            //
            // 🔒 VÌ SAO CHÍ MẠNG Ở ĐÂY: hệ thống đang chạy **1 tenant** (`TenantId = NULL`). Thiếu
            //    `NULLS NOT DISTINCT` thì:
            //      ① partial unique *"một mặc định"* **mất tác dụng hoàn toàn** ⇒ hai địa chỉ mặc định
            //         cùng loại **lọt** ⇒ khách giao hàng tới địa chỉ nào là **không xác định**;
            //      ② unique *"chống liên kết trùng"* mất tác dụng ⇒ cùng một địa chỉ vào sổ hai lần.
            //    📌 Cả hai đều **không lỗi nào nổi lên** — đúng lỗi C4/F1 đã chứng minh bằng chạy thật
            //    (`20261013024226_Fix_NullableTenantId_UniqueIndexes.cs`).
            //
            // ⚠️ `DROP INDEX IF EXISTS` KHÔNG phải cho đẹp: EF **đã** sinh 2 index này (không có mệnh đề
            //    `NULLS NOT DISTINCT`) ở phần `CreateIndex` ⇒ nếu không DROP trước thì `CREATE` thất bại
            //    `42P07`; 🔑 và nếu ai đó đổi thành `CREATE INDEX IF NOT EXISTS` thì index **CŨ (hỏng)**
            //    vẫn còn mà **không có lỗi nào**.
            // ══════════════════════════════════════════════════════════════════════════════════════

            // ① BẤT BIẾN *"tối đa MỘT mặc định cho mỗi (khách, loại)"* — BR-ADDR-004-006, ADR-06.
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh"";");

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh""
                    ON ""CusAddressLinks"" (""TenantId"", ""CustomerId"", ""AddressType"") NULLS NOT DISTINCT
                    WHERE ""IsDefault"" AND NOT ""IsDeleted"";");

            // ② CHỐNG LIÊN KẾT TRÙNG (lưới an toàn cho hai luồng cùng thêm — `TC-10`).
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_CusAddressLinks_TenantId_CustomerId_AddressId_AddressType"";");

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_CusAddressLinks_TenantId_CustomerId_AddressId_AddressType""
                    ON ""CusAddressLinks"" (""TenantId"", ""CustomerId"", ""AddressId"", ""AddressType"") NULLS NOT DISTINCT
                    WHERE NOT ""IsDeleted"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 🔒 Đối xứng: index tạo bằng SQL thô thì phải xoá tường minh (EF không tự sinh lệnh cho nó).
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_CusAddressLinks_TenantId_CustomerId_AddressType_MacDinh"";");

            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_CusAddressLinks_TenantId_CustomerId_AddressId_AddressType"";");

            migrationBuilder.DropTable(
                name: "CusAddressLinks");
        }
    }
}
