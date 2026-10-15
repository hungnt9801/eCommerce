using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerce.Migrations
{
    /// <inheritdoc />
    public partial class Pricing_PriceList_And_ChannelPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrcChannelPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChannelShopId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    AmountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PrcChannelPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrcPriceLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ChannelShopId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PrcPriceLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrcPriceListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
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
                    table.PrimaryKey("PK_PrcPriceListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrcPriceListItems_PrcPriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalTable: "PrcPriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty",
                table: "PrcChannelPrices",
                columns: new[] { "TenantId", "ChannelCode", "ChannelShopId", "Sku", "MinQuantity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcChannelPrices_TenantId_Sku_ChannelCode",
                table: "PrcChannelPrices",
                columns: new[] { "TenantId", "Sku", "ChannelCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PrcPriceListItems_PriceListId",
                table: "PrcPriceListItems",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_PrcPriceListItems_TenantId_PriceListId_Sku",
                table: "PrcPriceListItems",
                columns: new[] { "TenantId", "PriceListId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcPriceLists_TenantId_Code_EffectiveFrom",
                table: "PrcPriceLists",
                columns: new[] { "TenantId", "Code", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrcPriceLists_TenantId_Status_Scope_ChannelCode",
                table: "PrcPriceLists",
                columns: new[] { "TenantId", "Status", "Scope", "ChannelCode" });

            // 🚨🚨 **SỬA TAY — BẮT BUỘC** (`PRC-001` **BR-011** + `XC-004` **BR-009**).
            // EF sinh `CREATE UNIQUE INDEX` **KHÔNG** kèm `NULLS NOT DISTINCT`. PostgreSQL coi **NULL là DISTINCT**
            // ⇒ khi `TenantId = NULL` (ĐÚNG tình trạng hiện nay — hệ thống **1 tenant**) thì **MỌI** unique index
            // dưới đây **mất tác dụng im lặng**:
            //   • hai bảng giá cùng `(Code, EffectiveFrom)` **lọt**,
            //   • hai dòng giá cùng SKU trong một bảng giá **lọt** ⇒ **resolve ra giá nào cũng "đúng"**
            //     (và rơi vào `Pricing:0001` — hoặc tệ hơn: nếu khác `Priority` thì **âm thầm chọn một cái**),
            //   • hai `ChannelPrice` cùng khoá **lọt** ⇒ **bán sai giá**.
            migrationBuilder.Sql("DROP INDEX \"IX_PrcPriceLists_TenantId_Code_EffectiveFrom\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcPriceLists_TenantId_Code_EffectiveFrom\" ON \"PrcPriceLists\" " +
                "(\"TenantId\", \"Code\", \"EffectiveFrom\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_PrcPriceListItems_TenantId_PriceListId_Sku\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcPriceListItems_TenantId_PriceListId_Sku\" ON \"PrcPriceListItems\" " +
                "(\"TenantId\", \"PriceListId\", \"Sku\") NULLS NOT DISTINCT;");

            migrationBuilder.Sql("DROP INDEX \"IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty\";");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_PrcChannelPrices_TenantId_Kenh_Shop_Sku_MinQty\" ON \"PrcChannelPrices\" " +
                "(\"TenantId\", \"ChannelCode\", \"ChannelShopId\", \"Sku\", \"MinQuantity\") NULLS NOT DISTINCT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrcChannelPrices");

            migrationBuilder.DropTable(
                name: "PrcPriceListItems");

            migrationBuilder.DropTable(
                name: "PrcPriceLists");
        }
    }
}
