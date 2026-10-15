using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Inv001_Warehouse_StockDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvStockDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_InvStockDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvWarehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_InvWarehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvStockDocumentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_InvStockDocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvStockDocumentLines_InvStockDocuments_StockDocumentId",
                        column: x => x.StockDocumentId,
                        principalTable: "InvStockDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvStockDocumentLines_StockDocumentId",
                table: "InvStockDocumentLines",
                column: "StockDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InvStockDocumentLines_TenantId_Doc_Sku",
                table: "InvStockDocumentLines",
                columns: new[] { "TenantId", "StockDocumentId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvStockDocuments_TenantId_Number",
                table: "InvStockDocuments",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvStockDocuments_TenantId_Status_Type_OccurredAtUtc",
                table: "InvStockDocuments",
                columns: new[] { "TenantId", "Status", "Type", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InvWarehouses_TenantId_Code",
                table: "InvWarehouses",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`PRC-001` **BR-011** / `XC-004` **BR-009**).
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`; PostgreSQL coi **NULL là DISTINCT**
            // ⇒ khi `TenantId = NULL` (hệ thống **1 tenant** hiện nay) **cả HAI unique index MẤT TÁC DỤNG**:
            //   • `IX_InvWarehouses_TenantId_Code`: hai kho **cùng mã** ⇒ chứng từ trỏ `WarehouseId` **không biết**
            //     kho nào ⇒ tồn kho tách làm hai nơi mà báo cáo cộng gộp ⇒ **số liệu kho SAI**;
            //   • `IX_InvStockDocuments_TenantId_Number`: hai chứng từ **cùng số** ⇒ đối soát/kiểm toán
            //     **không phân biệt được** phiếu nào (đúng loại lỗi mà số chứng từ tồn tại để ngăn).
            migrationBuilder.Sql("DROP INDEX \"IX_InvWarehouses_TenantId_Code\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvWarehouses_TenantId_Code\" ON \"InvWarehouses\" " +
                "(\"TenantId\", \"Code\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_InvStockDocuments_TenantId_Number\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockDocuments_TenantId_Number\" ON \"InvStockDocuments\" " +
                "(\"TenantId\", \"Number\") NULLS NOT DISTINCT;");

            // Cùng lý do cho **dòng chứng từ** (unique theo `Doc + Sku`).
            migrationBuilder.Sql("DROP INDEX \"IX_InvStockDocumentLines_TenantId_Doc_Sku\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_InvStockDocumentLines_TenantId_Doc_Sku\" ON \"InvStockDocumentLines\" " +
                "(\"TenantId\", \"StockDocumentId\", \"Sku\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvStockDocumentLines");

            migrationBuilder.DropTable(
                name: "InvWarehouses");

            migrationBuilder.DropTable(
                name: "InvStockDocuments");
        }
    }
}
