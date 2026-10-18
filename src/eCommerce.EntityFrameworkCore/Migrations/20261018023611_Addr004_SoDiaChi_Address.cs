using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Addr004_SoDiaChi_Address : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddrAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrangThai = table.Column<int>(type: "integer", nullable: false),
                    NguonTao = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    TinhCode = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    HuyenCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    XaCode = table.Column<string>(type: "character(5)", fixedLength: true, maxLength: 5, nullable: true),
                    SoNhaDuong = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DiaChiDayDu = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    NormalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    XacMinhLucUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AddrAddresses", x => x.Id);
                    table.CheckConstraint("CK_AddrAddresses_ContentHashHex", "\"ContentHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_AddrAddresses_DaChuanHoaCoMa", "(\"TrangThai\" = 0) OR (\"TinhCode\" IS NOT NULL AND \"XaCode\" IS NOT NULL AND \"NormalizedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_AddrAddresses_DinhDangMa", "(\"TinhCode\" IS NULL OR \"TinhCode\" ~ '^[0-9]{2}$') AND (\"XaCode\" IS NULL OR \"XaCode\" ~ '^[0-9]{5}$') AND (\"HuyenCode\" IS NULL OR \"HuyenCode\" ~ '^[0-9]{3}$')");
                    table.CheckConstraint("CK_AddrAddresses_SchemaVersion", "\"SchemaVersion\" in (1, 2)");
                    table.CheckConstraint("CK_AddrAddresses_ThoKhongCoMa", "(\"TrangThai\" <> 0) OR (\"TinhCode\" IS NULL AND \"HuyenCode\" IS NULL AND \"XaCode\" IS NULL AND \"NormalizedAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_AddrAddresses_TrangThai", "\"TrangThai\" in (0, 1, 2, 3)");
                    table.CheckConstraint("CK_AddrAddresses_V2KhongCoHuyen", "\"SchemaVersion\" <> 2 OR \"HuyenCode\" IS NULL");
                });

            // 🔒 ADDR-004 (ADR-04): KHOÁ TỰ NHIÊN của địa chỉ — một NỘI DUNG ⇒ một bản ghi.
            //
            // 🚨 VÌ SAO PHẢI VIẾT SQL THÔ: EF KHÔNG diễn đạt được `NULLS NOT DISTINCT`, mà PostgreSQL
            //    mặc định coi `NULL` là **KHÁC NHAU**. Hệ thống đang chạy **1 tenant** (`TenantId = NULL`)
            //    ⇒ thiếu mệnh đề này thì index MẤT TÁC DỤNG HOÀN TOÀN: hai bản ghi cùng `ContentHash`
            //    lọt vào DB **không có lỗi nào nổi lên** (đúng lỗi C4/F1 đã chứng minh bằng chạy thật —
            //    `20261013024226_Fix_NullableTenantId_UniqueIndexes.cs:14-18`).
            //
            // 📌 Bộ lọc `WHERE NOT "IsDeleted"` để địa chỉ đã xoá mềm KHÔNG chiếm khoá tự nhiên: tạo lại
            //    cùng nội dung phải tạo được bản ghi mới (ADR-11 giữ dòng cũ để không phá vết lịch sử).
            //
            // ⚠️ `DROP INDEX IF EXISTS` KHÔNG phải cho đẹp: EF **đã** sinh index này (không có mệnh đề
            //    `NULLS NOT DISTINCT`) ở trên ⇒ không DROP trước thì CREATE thất bại `42P07`; và nếu ai đó
            //    đổi thành `CREATE INDEX IF NOT EXISTS` thì index CŨ (hỏng) vẫn còn mà KHÔNG có lỗi nào.
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AddrAddresses_TenantId_ContentHash"";");

            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_AddrAddresses_TenantId_ContentHash""
                    ON ""AddrAddresses"" (""TenantId"", ""ContentHash"") NULLS NOT DISTINCT
                    WHERE NOT ""IsDeleted"";");

            migrationBuilder.CreateIndex(
                name: "IX_AddrAddresses_TenantId_TinhCode_XaCode",
                table: "AddrAddresses",
                columns: new[] { "TenantId", "TinhCode", "XaCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AddrAddresses_TenantId_TrangThai",
                table: "AddrAddresses",
                columns: new[] { "TenantId", "TrangThai" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Đối xứng: index tạo bằng SQL thô thì phải xoá tường minh (EF không tự sinh cho nó).
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_AddrAddresses_TenantId_ContentHash"";");

            migrationBuilder.DropTable(
                name: "AddrAddresses");
        }
    }
}
