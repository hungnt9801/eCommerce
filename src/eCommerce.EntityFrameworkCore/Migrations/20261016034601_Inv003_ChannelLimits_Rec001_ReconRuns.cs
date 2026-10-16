using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Inv003_ChannelLimits_Rec001_ReconRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvChannelLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    GiaTri = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    HanhDong = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_InvChannelLimits", x => x.Id);
                    table.CheckConstraint("CK_InvChannelLimits_GiaTri_KhongAm", "\"GiaTri\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ReconRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatDauUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KetThucUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SoCapDaDo = table.Column<int>(type: "integer", nullable: false),
                    SoLech = table.Column<int>(type: "integer", nullable: false),
                    DonViNguong = table.Column<int>(type: "integer", nullable: false),
                    GiaTriNguong = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    HanhDong = table.Column<int>(type: "integer", nullable: false),
                    NguongHopLe = table.Column<bool>(type: "boolean", nullable: false),
                    SoLechVuotNguong = table.Column<int>(type: "integer", nullable: false),
                    CoVuotNguong = table.Column<bool>(type: "boolean", nullable: false),
                    KetLuan = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ReconRuns", x => x.Id);
                    table.CheckConstraint("CK_ReconRuns_MocThoiGian_HopLe", "\"KetThucUtc\" >= \"BatDauUtc\"");
                    table.CheckConstraint("CK_ReconRuns_SoCapDaDo_KhongAm", "\"SoCapDaDo\" >= 0");
                    table.CheckConstraint("CK_ReconRuns_SoLechVuot_KhongVuotTong", "\"SoLechVuotNguong\" >= 0 AND \"SoLech\" >= 0 AND \"SoLechVuotNguong\" <= \"SoLech\"");
                });

            migrationBuilder.CreateTable(
                name: "ReconRunMismatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReconRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    TongSoCai = table.Column<int>(type: "integer", nullable: false),
                    ChenhLech = table.Column<int>(type: "integer", nullable: false),
                    VuotNguong = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ReconRunMismatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconRunMismatches_ReconRuns_ReconRunId",
                        column: x => x.ReconRunId,
                        principalTable: "ReconRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId",
                table: "InvChannelLimits",
                columns: new[] { "TenantId", "ChannelCode", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconRunMismatches_ReconRunId",
                table: "ReconRunMismatches",
                column: "ReconRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconRunMismatches_TenantId_Run_Kho_Sku",
                table: "ReconRunMismatches",
                columns: new[] { "TenantId", "ReconRunId", "WarehouseId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconRuns_TenantId_BatDauUtc",
                table: "ReconRuns",
                columns: new[] { "TenantId", "BatDauUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconRuns_TenantId_CoVuotNguong_BatDauUtc",
                table: "ReconRuns",
                columns: new[] { "TenantId", "CoVuotNguong", "BatDauUtc" });

            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT` CHO HAI UNIQUE INDEX MỚI.**
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống chạy **1 tenant** ⇒ `TenantId` **có thể `NULL`**; PostgreSQL
            // mặc định coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **mất tác dụng IM LẶNG** (đúng lỗi C4/F1
            // đã trả giá, xem `XC-004` Q6 + `PostgreSqlSchemaTests.F1`). EF Core **không** sinh được mệnh đề này
            // ⇒ phải viết SQL thô (đúng khuôn `Inv001_Fix_SoCai_Unique_ThemMovementType`).
            //
            // ① `InvChannelLimits` — khoá nghiệp vụ **`(TenantId, ChannelCode, WarehouseId)`** với `WarehouseId`
            //    **NULLABLE** (`null` = *"hạn mức CẢ KÊNH"*). Đây là ca **nặng nhất** của lỗi này: chính cột
            //    phân biệt *"cả kênh"* với *"một kho"* lại là cột NULL ⇒ thiếu `NULLS NOT DISTINCT` thì
            //    người vận hành khai **HAI** dòng *"hạn mức cả kênh"* cho cùng một kênh mà **KHÔNG gì chặn**
            //    ⇒ **hai hạn mức cho một kênh** ⇒ hành vi phụ thuộc thứ tự đọc (không tất định, không lỗi).
            //
            // ② `ReconRunMismatches` — `(TenantId, ReconRunId, WarehouseId, Sku)` chống **ghi trùng** một cặp
            //    `(kho, SKU)` trong cùng lượt (job retry / hai luồng cùng ghi). `TenantId = NULL` ⇒ thiếu mệnh đề
            //    là **mất** hàng rào ⇒ lịch sử đối soát có dòng trùng ⇒ *"lệch này xuất hiện bao nhiêu lần"* đếm **sai**.
            //
            // 📌 Bước 1 xoá index EF vừa tạo, bước 2 tạo lại **kèm** `NULLS NOT DISTINCT` — giữ nguyên **tên** index
            // (để `PostgreSqlSchemaTests` đối chiếu **đích danh** vẫn tìm thấy, và để `Down()` chỉ cần `DropTable`).
            migrationBuilder.Sql("DROP INDEX \"IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvChannelLimits_TenantId_ChannelCode_WarehouseId\" ON \"InvChannelLimits\" " +
                "(\"TenantId\", \"ChannelCode\", \"WarehouseId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_ReconRunMismatches_TenantId_Run_Kho_Sku\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_ReconRunMismatches_TenantId_Run_Kho_Sku\" ON \"ReconRunMismatches\" " +
                "(\"TenantId\", \"ReconRunId\", \"WarehouseId\", \"Sku\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvChannelLimits");

            migrationBuilder.DropTable(
                name: "ReconRunMismatches");

            migrationBuilder.DropTable(
                name: "ReconRuns");
        }
    }
}
