using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Add_Tax_Module : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseRateCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReductionApplicable = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    LegalBasisNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_TaxClassifications", x => x.Id);
                    table.CheckConstraint("CK_TaxClassifications_EffectiveRange", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                });

            migrationBuilder.CreateTable(
                name: "TaxRateSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ReducedFromCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    LegalBasis = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_TaxRateSchedules", x => x.Id);
                    table.CheckConstraint("CK_TaxRateSchedules_EffectiveRange", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                    table.CheckConstraint("CK_TaxRateSchedules_Rate", "\"Rate\" >= 0 AND \"Rate\" <= 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxClassifications_TenantId_TargetType_TargetId_EffectiveFr~",
                table: "TaxClassifications",
                columns: new[] { "TenantId", "TargetType", "TargetId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxClassifications_TenantId_TargetType_TargetId_Status",
                table: "TaxClassifications",
                columns: new[] { "TenantId", "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateSchedules_TenantId_Code",
                table: "TaxRateSchedules",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateSchedules_TenantId_EffectiveFrom_EffectiveTo",
                table: "TaxRateSchedules",
                columns: new[] { "TenantId", "EffectiveFrom", "EffectiveTo" });

            // ---------------------------------------------------------------------------------
            // 🚨 CHỐNG CHỒNG KHUNG HIỆU LỰC Ở TẦNG DB — BR-XC-006-009 (TC-09 của XC-006)
            //
            // Vì sao phải là ràng buộc DB chứ không chỉ kiểm tra trong code:
            //   index unique ở trên CHỈ chặn được 2 bản ghi cùng EffectiveFrom. Hai khung GIAO NHAU
            //   (ví dụ 01/01–30/06 và 01/03–31/12) vẫn lọt ⇒ phân giải thuế sẽ phụ thuộc thứ tự đọc.
            //   PostgreSQL có `EXCLUDE USING gist` đúng cho bài toán này; EF Core KHÔNG có API khai báo
            //   ⇒ phải viết SQL thô (và SQL thô thì KHÔNG test được nếu không có PostgreSQL thật).
            //
            // `btree_gist` cần thiết để dùng toán tử `=` cho uuid/int/bool bên trong GiST index.
            //
            // Hai chi tiết đã cân nhắc:
            //   1. TenantId nullable ⇒ dùng COALESCE về Guid rỗng, nếu không thì `=` với NULL không
            //      bao giờ khớp và các bản ghi cấp host sẽ KHÔNG được bảo vệ.
            //   2. ABP soft-delete: bản ghi đã xoá vẫn chiếm khung. Thêm "IsDeleted" WITH = vào khoá
            //      để bản ghi đã xoá chỉ xung đột với bản ghi đã xoá khác, không chặn bản ghi mới.
            //      (EXCLUDE không hỗ trợ mệnh đề WHERE nên đây là cách xử lý khả thi.)
            // ---------------------------------------------------------------------------------
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql("""
                ALTER TABLE "TaxClassifications"
                    ADD CONSTRAINT "EX_TaxClassifications_ChongChongKhung"
                    EXCLUDE USING gist (
                        (COALESCE("TenantId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =,
                        "TargetType" WITH =,
                        "TargetId" WITH =,
                        "IsDeleted" WITH =,
                        daterange("EffectiveFrom", COALESCE("EffectiveTo", 'infinity'::date), '[]') WITH &&
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxClassifications");

            migrationBuilder.DropTable(
                name: "TaxRateSchedules");
        }
    }
}
