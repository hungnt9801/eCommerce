using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Cat002c_AttributesAndVariantGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributeHash",
                table: "CatProductVariants",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Usage = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CatAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatAttributeValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CatAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatAttributeValues_CatAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "CatAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatProductAttributeSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CatProductAttributeSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductAttributeSelections_CatAttributeValues_AttributeV~",
                        column: x => x.AttributeValueId,
                        principalTable: "CatAttributeValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatProductAttributeSelections_CatAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "CatAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatProductAttributeSelections_CatProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "CatProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatProductVariantAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CatProductVariantAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatProductVariantAttributes_CatAttributeValues_AttributeVal~",
                        column: x => x.AttributeValueId,
                        principalTable: "CatAttributeValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatProductVariantAttributes_CatAttributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "CatAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatProductVariantAttributes_CatProductVariants_ProductVaria~",
                        column: x => x.ProductVariantId,
                        principalTable: "CatProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatProdVariants_TenantId_ProductId_AttributeHash",
                table: "CatProductVariants",
                columns: new[] { "TenantId", "ProductId", "AttributeHash" },
                unique: true,
                filter: "\"AttributeHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatAttributes_TenantId_Code",
                table: "CatAttributes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatAttributeValues_AttributeId",
                table: "CatAttributeValues",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_CatAttrValues_TenantId_AttributeId_Value",
                table: "CatAttributeValues",
                columns: new[] { "TenantId", "AttributeId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId",
                table: "CatProductAttributeSelections",
                columns: new[] { "TenantId", "ProductId", "AttributeId", "AttributeValueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatProductAttributeSelections_AttributeId",
                table: "CatProductAttributeSelections",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductAttributeSelections_AttributeValueId",
                table: "CatProductAttributeSelections",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductAttributeSelections_ProductId",
                table: "CatProductAttributeSelections",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductVariantAttributes_AttributeId",
                table: "CatProductVariantAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductVariantAttributes_AttributeValueId",
                table: "CatProductVariantAttributes",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProductVariantAttributes_ProductVariantId",
                table: "CatProductVariantAttributes",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_CatProdVarAttr_TenantId_VariantId_AttributeId",
                table: "CatProductVariantAttributes",
                columns: new[] { "TenantId", "ProductVariantId", "AttributeId" },
                unique: true);

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`CAT-002` lát cắt 4). EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm
            // `NULLS NOT DISTINCT`. PostgreSQL coi **NULL là DISTINCT** ⇒ khi `TenantId = NULL` (ĐÚNG tình trạng
            // hiện nay — hệ thống **1 tenant**) thì **MỌI** unique index dưới đây **mất tác dụng im lặng**
            // (`XC-004` BR-009). Riêng index `AttributeHash` còn là **hàng rào cuối** cho bất biến
            // "tổ hợp duy nhất trên mỗi sản phẩm" (D7/BR-010): mất nó ⇒ sinh lại tổ hợp tạo **biến thể trùng**.
            // 📌 PostgreSQL 15+ cho phép `NULLS NOT DISTINCT` **kết hợp** `WHERE` (index một phần).
            migrationBuilder.Sql("DROP INDEX \"IX_CatAttributes_TenantId_Code\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatAttributes_TenantId_Code\" ON \"CatAttributes\" " +
                "(\"TenantId\", \"Code\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatAttrValues_TenantId_AttributeId_Value\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatAttrValues_TenantId_AttributeId_Value\" ON \"CatAttributeValues\" " +
                "(\"TenantId\", \"AttributeId\", \"Value\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdAttrSel_TenantId_ProductId_AttributeId_ValueId\" ON \"CatProductAttributeSelections\" " +
                "(\"TenantId\", \"ProductId\", \"AttributeId\", \"AttributeValueId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdVarAttr_TenantId_VariantId_AttributeId\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdVarAttr_TenantId_VariantId_AttributeId\" ON \"CatProductVariantAttributes\" " +
                "(\"TenantId\", \"ProductVariantId\", \"AttributeId\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_CatProdVariants_TenantId_ProductId_AttributeHash\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_CatProdVariants_TenantId_ProductId_AttributeHash\" ON \"CatProductVariants\" " +
                "(\"TenantId\", \"ProductId\", \"AttributeHash\") NULLS NOT DISTINCT WHERE \"AttributeHash\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatProductAttributeSelections");

            migrationBuilder.DropTable(
                name: "CatProductVariantAttributes");

            migrationBuilder.DropTable(
                name: "CatAttributeValues");

            migrationBuilder.DropTable(
                name: "CatAttributes");

            migrationBuilder.DropIndex(
                name: "IX_CatProdVariants_TenantId_ProductId_AttributeHash",
                table: "CatProductVariants");

            migrationBuilder.DropColumn(
                name: "AttributeHash",
                table: "CatProductVariants");
        }
    }
}
