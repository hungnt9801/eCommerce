using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Wp03_Catalog_NhanhModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "CatProducts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHot",
                table: "CatProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "CatProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "CatProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageTimeDays",
                table: "CatProducts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "CatProducts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "VatMode",
                table: "CatProducts",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                table: "CatProducts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyMonths",
                table: "CatProducts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeightGram",
                table: "CatProducts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatComboComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComboProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
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
                    table.PrimaryKey("PK_CatComboComponents", x => x.Id);
                    table.CheckConstraint("CK_CatComboComponents_Quantity_Duong", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CatComboComponents_CatProducts_ComboProductId",
                        column: x => x.ComboProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatComboComponents_CatProducts_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatPackageComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackageProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
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
                    table.PrimaryKey("PK_CatPackageComponents", x => x.Id);
                    table.CheckConstraint("CK_CatPackageComponents_Quantity_Duong", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CatPackageComponents_CatProducts_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatPackageComponents_CatProducts_PackageProductId",
                        column: x => x.PackageProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatProdBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ManufactureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PriorWarningDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CatProdBatches", x => x.Id);
                    table.CheckConstraint("CK_CatProdBatches_Ngay_Khoang", "\"ManufactureDate\" IS NULL OR \"ExpiredDate\" IS NULL OR \"ManufactureDate\" <= \"ExpiredDate\"");
                    table.ForeignKey(
                        name: "FK_CatProdBatches_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatProductTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_CatProductTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductTags_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatProductUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsBase = table.Column<bool>(type: "boolean", nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImportPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    WholesalePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OldPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_CatProductUnits", x => x.Id);
                    table.CheckConstraint("CK_CatProdUnits_ConversionFactor_Duong", "\"ConversionFactor\" > 0");
                    table.CheckConstraint("CK_CatProdUnits_SalePrice_KhongAm", "\"SalePrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_CatProductUnits_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatProductImeis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Serial = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    SoldBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarrantyUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_CatProductImeis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductImeis_CatProdBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "CatProdBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatProductImeis_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatProducts_ParentId",
                table: "CatProducts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProducts_TenantId_ExternalId",
                table: "CatProducts",
                columns: new[] { "TenantId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatProducts_TenantId_ParentId",
                table: "CatProducts",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CatProducts_VatPercent_Khoang",
                table: "CatProducts",
                sql: "\"VatPercent\" IS NULL OR (\"VatPercent\" >= -100 AND \"VatPercent\" <= 100)");

            migrationBuilder.CreateIndex(
                name: "IX_CatComboComponents_ComboProductId",
                table: "CatComboComponents",
                column: "ComboProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatComboComponents_ComponentProductId",
                table: "CatComboComponents",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatComboComponents_TenantId_Combo_Component",
                table: "CatComboComponents",
                columns: new[] { "TenantId", "ComboProductId", "ComponentProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatPackageComponents_ComponentProductId",
                table: "CatPackageComponents",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatPackageComponents_PackageProductId",
                table: "CatPackageComponents",
                column: "PackageProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatPackageComponents_TenantId_Package_Component",
                table: "CatPackageComponents",
                columns: new[] { "TenantId", "PackageProductId", "ComponentProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProdBatches_ProductId",
                table: "CatProdBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProdBatches_TenantId_ExpiredDate",
                table: "CatProdBatches",
                columns: new[] { "TenantId", "ExpiredDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CatProdBatches_TenantId_ProductId_Code",
                table: "CatProdBatches",
                columns: new[] { "TenantId", "ProductId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProdImeis_TenantId_ProductId_Status",
                table: "CatProductImeis",
                columns: new[] { "TenantId", "ProductId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CatProdImeis_TenantId_Serial",
                table: "CatProductImeis",
                columns: new[] { "TenantId", "Serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProductImeis_BatchId",
                table: "CatProductImeis",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductImeis_ProductId",
                table: "CatProductImeis",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductTags_ProductId",
                table: "CatProductTags",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductTags_TenantId_Name",
                table: "CatProductTags",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CatProductTags_TenantId_ProductId_Name",
                table: "CatProductTags",
                columns: new[] { "TenantId", "ProductId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProductUnits_ProductId",
                table: "CatProductUnits",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProdUnits_MotDonViGoc",
                table: "CatProductUnits",
                columns: new[] { "TenantId", "ProductId" },
                unique: true,
                filter: "\"IsBase\"");

            migrationBuilder.CreateIndex(
                name: "IX_CatProdUnits_TenantId_Barcode",
                table: "CatProductUnits",
                columns: new[] { "TenantId", "Barcode" },
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatProdUnits_TenantId_ProductId_Name",
                table: "CatProductUnits",
                columns: new[] { "TenantId", "ProductId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CatProducts_CatProducts_ParentId",
                table: "CatProducts",
                column: "ParentId",
                principalTable: "CatProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC: `NULLS NOT DISTINCT` CHO 8 UNIQUE INDEX MỚI** (WP-03).
            //
            // **VÌ SAO KHÔNG THỂ BỎ:** hệ thống chạy **1 tenant** ⇒ `TenantId` **có thể `NULL`**; PostgreSQL mặc
            // định coi hai `NULL` là **KHÁC NHAU** ⇒ unique index **MẤT TÁC DỤNG IM LẶNG** (lỗi C4/F1 đã trả giá —
            // `XC-004` Q6, `PostgreSqlSchemaTests.F1`). EF Core **không** sinh được mệnh đề này **kèm** bộ lọc
            // `WHERE` ⇒ phải viết SQL thô (cùng cách `Cat003_ProductSlug_Unique`).
            //
            // 🔒 Cách làm: xoá index EF vừa tạo → tạo lại **kèm** `NULLS NOT DISTINCT`, **giữ nguyên tên** để
            // `PostgreSqlSchemaTests` đối chiếu đích danh vẫn tìm thấy và `Down()` chỉ cần `DropIndex`.
            //
            // ⚠️ Hệ quả nếu THIẾU (lần lượt theo từng index):
            //   • `..._TenantId_Barcode` — hai ĐVT cùng barcode (tenant `NULL`) LỌT ⇒ quét mã ở POS bán **sai món**;
            //   • `..._ProductId_Name` — hai dòng giá cho CÙNG một ĐVT ⇒ hoá đơn lấy giá nào cũng "đúng";
            //   • `..._MotDonViGoc` — hai ĐVT gốc ⇒ quy đổi tồn kho theo **hai hệ số**;
            //   • `..._TenantId_Serial` — cùng IMEI nhập hai lần ⇒ bán một máy **hai lần**;
            //   • `..._ProductId_Code` — hai lô cùng mã ⇒ trừ tồn/hạn dùng sai lô;
            //   • `..._Combo_Component` / `..._Package_Component` — một thành phần khai hai lần ⇒ xuất tồn gấp đôi;
            //   • `..._ProductId_Name` (tag) — nhãn trùng ⇒ bộ lọc đếm sai.
            // 📌 Tất cả đều **KHÔNG có lỗi nào nổi lên** khi thiếu — đúng họ lỗi im lặng mà `PostgreSqlSchemaTests` sinh ra để chặn.

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdUnits_TenantId_ProductId_Name\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdUnits_TenantId_ProductId_Name\" ON \"CatProductUnits\" " +
                "(\"TenantId\", \"ProductId\", \"Name\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdUnits_TenantId_Barcode\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdUnits_TenantId_Barcode\" ON \"CatProductUnits\" " +
                "(\"TenantId\", \"Barcode\") NULLS NOT DISTINCT WHERE \"Barcode\" IS NOT NULL;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdUnits_MotDonViGoc\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdUnits_MotDonViGoc\" ON \"CatProductUnits\" " +
                "(\"TenantId\", \"ProductId\") NULLS NOT DISTINCT WHERE \"IsBase\";");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdImeis_TenantId_Serial\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdImeis_TenantId_Serial\" ON \"CatProductImeis\" " +
                "(\"TenantId\", \"Serial\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdBatches_TenantId_ProductId_Code\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdBatches_TenantId_ProductId_Code\" ON \"CatProdBatches\" " +
                "(\"TenantId\", \"ProductId\", \"Code\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatComboComponents_TenantId_Combo_Component\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatComboComponents_TenantId_Combo_Component\" ON \"CatComboComponents\" " +
                "(\"TenantId\", \"ComboProductId\", \"ComponentProductId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatPackageComponents_TenantId_Package_Component\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatPackageComponents_TenantId_Package_Component\" ON \"CatPackageComponents\" " +
                "(\"TenantId\", \"PackageProductId\", \"ComponentProductId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProductTags_TenantId_ProductId_Name\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProductTags_TenantId_ProductId_Name\" ON \"CatProductTags\" " +
                "(\"TenantId\", \"ProductId\", \"Name\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatProducts_CatProducts_ParentId",
                table: "CatProducts");

            migrationBuilder.DropTable(
                name: "CatComboComponents");

            migrationBuilder.DropTable(
                name: "CatPackageComponents");

            migrationBuilder.DropTable(
                name: "CatProductImeis");

            migrationBuilder.DropTable(
                name: "CatProductTags");

            migrationBuilder.DropTable(
                name: "CatProductUnits");

            migrationBuilder.DropTable(
                name: "CatProdBatches");

            migrationBuilder.DropIndex(
                name: "IX_CatProducts_ParentId",
                table: "CatProducts");

            migrationBuilder.DropIndex(
                name: "IX_CatProducts_TenantId_ExternalId",
                table: "CatProducts");

            migrationBuilder.DropIndex(
                name: "IX_CatProducts_TenantId_ParentId",
                table: "CatProducts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CatProducts_VatPercent_Khoang",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "IsHot",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "StorageTimeDays",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "VatMode",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "WarrantyMonths",
                table: "CatProducts");

            migrationBuilder.DropColumn(
                name: "WeightGram",
                table: "CatProducts");
        }
    }
}
